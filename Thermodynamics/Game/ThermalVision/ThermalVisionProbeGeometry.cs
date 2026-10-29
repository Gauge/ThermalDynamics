using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using Sandbox.Game.Entities;
using Thermodynamics.Core;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Import;
using Triangle = Thermodynamics.Presentation.ThermalVisionTriangle;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private static void DrawEntity(MyEntity entity, Vector4 colour, Vector3D eye, int depth)
        {
            if (entity == null || entity.MarkedForClose || !((VRage.ModAPI.IMyEntity)entity).Visible) return;
            if (++parts > MaxParts || depth > 4) { incomplete = true; return; }
            IMyModel model = ((VRage.ModAPI.IMyEntity)entity).Model;
            if (model != null)
            {
                DrawModel(model, entity.WorldMatrix, colour, eye);
            }

            if (entity.Subparts == null) return;
            foreach (var subpart in entity.Subparts.Values)
            {
                if (parts >= MaxParts || examined >= MaxFrameTriangles) { incomplete = true; break; }
                DrawEntity(subpart, colour, eye, depth + 1);
            }
        }


        private static string DrawArmour(Crosshair.Target target, Vector4 colour)
        {
            if (target.Block.Block.HasDeformation)
            {
                incomplete = true;
                return "armour-deformation-unsupported";
            }
            MyCube cube;
            if (!target.Thermals.Grid.TryGetCube(target.Cell, out cube) || cube == null || cube.Parts == null)
                return "no-grid-parts";
            foreach (var part in cube.Parts)
            {
                if (++parts > MaxParts) { incomplete = true; break; }
                if (part == null || part.Model == null) { incomplete = true; continue; }
                DrawModel((IMyModel)part.Model,
                    part.InstanceData.LocalMatrix * target.Thermals.Grid.WorldMatrix,
                    colour, target.Camera.Translation);
                if (examined >= MaxFrameTriangles) { incomplete = true; break; }
            }
            return drawn == 0 ? "armour-zero-submitted" : incomplete ? "armour-partial" : "armour-submitted";
        }


        private static void DrawModel(IMyModel model, MatrixD matrix, Vector4 colour, Vector3D eye)
        {
            double started = capture ? ProbeClock.Elapsed.TotalMilliseconds : 0;
            int before = drawn;

            try { DrawModelCore(model, matrix, colour, eye); }
            finally
            {
                if (capture)
                {
                    double elapsed = ProbeClock.Elapsed.TotalMilliseconds - started;
                    if (elapsed >= 8) RecordEvent("slow model " + model.AssetName + " ms=" + elapsed.ToString("0.00")
                        + " submitted=" + (drawn - before) + " (elapsed time; cause unclassified)", false);
                }
            }
        }


        private static void DrawModelCore(IMyModel model, MatrixD matrix, Vector4 colour, Vector3D eye)
        {
            if (sceneMode && (ProbeClock.Elapsed.TotalMilliseconds >= sceneDeadline || examined >= sceneTriangleLimit))
            {
                sceneLimits |= sceneTriangleLimit < SceneTriangles ? ThermalVisionSceneLimit.ArmourQuota
                    : examined >= sceneTriangleLimit ? ThermalVisionSceneLimit.Triangles : ThermalVisionSceneLimit.Time;
                incomplete = true; return;
            }

            ThermalVisionMesh mesh = GetGeometry(model);
            Triangle[] triangles = mesh.Triangles;
            Vector3D localEye;
            bool localCull = ThermalVisionGeometry.TryGetLocalEye(matrix, eye, out localEye);
            int visibleBatchCount = 0, candidateTriangles = 0;
            for (int group = 0; group < mesh.Batches.Length; group++)
            {
                if (sceneMode && group % 16 == 0 && ProbeClock.Elapsed.TotalMilliseconds >= sceneDeadline)
                { sceneLimits |= ThermalVisionSceneLimit.Time; incomplete = true; return; }
                ThermalVisionMeshBatch batch = mesh.Batches[group];
                batchesTested++;
                if (localCull && batch.IsEntirelyBackFacing(localEye))
                { batchRejectedTriangles += batch.Count; backfaces += batch.Count; continue; }
                VisibleBatches[visibleBatchCount++] = group;
                candidateTriangles += batch.Count;
            }
            if (sceneMode && candidateTriangles > sceneTriangleLimit - examined)
            {
                sceneLimits |= sceneTriangleLimit < SceneTriangles ? ThermalVisionSceneLimit.ArmourQuota : ThermalVisionSceneLimit.Triangles;
                incomplete = true; return;
            }
            for (int group = 0; group < visibleBatchCount; group++)
            {
                ThermalVisionMeshBatch batch = mesh.Batches[VisibleBatches[group]];
                for (int i = batch.Start; i < batch.Start + batch.Count; i++)
                {
                    if (sceneMode && i % 64 == 0 && ProbeClock.Elapsed.TotalMilliseconds >= sceneDeadline)
                    {
                        sceneLimits |= sceneTriangleLimit < SceneTriangles ? ThermalVisionSceneLimit.ArmourQuota : ThermalVisionSceneLimit.Time;
                        incomplete = true; return;
                    }
                    if (examined++ >= (gradientLab ? GradientLimit : MaxFrameTriangles)) { incomplete = true; return; }
                    ThermalVisionWorldTriangle projected;
                    ThermalVisionProjection projection = ThermalVisionGeometry.Project(triangles[i], matrix, eye, localCull, localEye, out projected);
                    if (projection == ThermalVisionProjection.Backface) { backfaces++; continue; }
                    if (projection == ThermalVisionProjection.Degenerate) { degenerate++; continue; }
                    Vector3 n = projected.Normal;
                    if (gradientLab) { DrawGradient(projected); drawn++; continue; }
                    MyTransparentGeometry.AddTriangleBillboard(projected.A, projected.B, projected.C,
                        n, n, n, Vector2.Zero, Vector2.UnitX, Vector2.UnitY, compositeMode ? CompositeSurfaceMaterial : Material, uint.MaxValue,
                        (projected.A + projected.B + projected.C) / 3, colour, compositeMode ? MyBillboard.BlendTypeEnum.PostPP : MyBillboard.BlendTypeEnum.Standard);
                    drawn++;
                }
            }
        }


        private static void AdvanceGeometryBuild()
        {
            if (pendingGeometry == null) return;
            var job = pendingGeometry;
            cacheMiss = true;
            double deadline = Math.Min(SceneMilliseconds, ProbeClock.Elapsed.TotalMilliseconds + 1);
            while (!job.Build.Complete && sceneBuiltTriangles < SceneBuildTriangles)
            {
                if (sceneBuiltTriangles % 64 == 0 && ProbeClock.Elapsed.TotalMilliseconds >= deadline) break;
                sceneBuiltTriangles += job.Build.Advance(Math.Min(64, SceneBuildTriangles - sceneBuiltTriangles));
            }
            if (!job.Build.Complete)
            {
                sceneLimits |= ThermalVisionSceneLimit.ModelBuild;
                return;
            }
            ThermalVisionMesh triangles = job.Build.Mesh;
            Geometry.Add(job.Model, triangles, job.Build.Unsupported > 0);
            RecordEvent("progressive model complete " + job.Model.AssetName + " source=" + job.Build.Total
                + " retained=" + triangles.Triangles.Length + " unsupported=" + job.Build.Unsupported, false);
            pendingGeometry = null;
        }


        private static ThermalVisionMesh GetGeometry(IMyModel model)
        {
            ThermalVisionMesh triangles;
            bool partial;
            if (Geometry.TryGetValue(model, out triangles, out partial))
            {
                if (triangles.Triangles.Length == 0 || partial) incomplete = true;
                return triangles;
            }
            int count = model.GetTrianglesCount();
            if (sceneMode && count <= MaxModelTriangles)
            {
                if (pendingGeometry == null)

                    pendingGeometry = new PendingGeometry { Model = model, Build = new ThermalVisionMeshBuild(new ModelSource(model)) };
                incomplete = true;
                sceneLimits |= ThermalVisionSceneLimit.ModelBuild;
                return Empty;
            }
            cacheMiss = true;
            if (count > MaxModelTriangles)
            {
                incomplete = true;
                Geometry.Add(model, Empty, true);
                if (capture) RecordEvent("model budget rejected: " + model.AssetName + " triangles=" + count, false);
                return Empty;
            }

            var build = new ThermalVisionMeshBuild(new ModelSource(model));
            build.Advance(MaxModelTriangles);
            int unsupported = build.Unsupported;
            if (unsupported > 0) incomplete = true;
            triangles = build.Mesh;
            Geometry.Add(model, triangles, unsupported > 0);
            if (capture) RecordEvent("model " + model.AssetName + " source=" + count
                + " retained=" + triangles.Triangles.Length + " unsupported-technique=" + unsupported, false);
            return triangles;
        }
    }
}
