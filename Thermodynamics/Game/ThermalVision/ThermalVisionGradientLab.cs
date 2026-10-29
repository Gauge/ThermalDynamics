using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Utils;
using VRage.Game;
using VRageMath;
using VRageRender;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private static bool gradientLab;
        private static readonly MyStringId GradientColour = MyStringId.GetOrCompute("GaugeThermalRampColour");
        private static readonly MyStringId GradientGrey = MyStringId.GetOrCompute("GaugeThermalRampGrey");
        private static readonly List<Vector3D> gradientPositions = new List<Vector3D>();
        private static readonly List<float> gradientTemperatures = new List<float>();
        private static readonly HashSet<ThermalBlock> gradientSeen = new HashSet<ThermalBlock>();
        private static readonly Dictionary<Vector3D, Vector2> gradientUvs = new Dictionary<Vector3D, Vector2>();
        private static MatrixD gradientInverse;
        private static double gradientCell;
        private const int GradientLimit = 6000;

        private static void PrepareGradient(Crosshair.Target target)
        {
            gradientPositions.Clear(); gradientTemperatures.Clear(); gradientSeen.Clear(); gradientUvs.Clear();
            gradientCell = target.Thermals.Grid.GridSize;
            gradientInverse = MatrixD.Invert(target.Thermals.Grid.WorldMatrix);
            AddGradientSample(target.Block);
            for (int x = -2; x <= 2; x++)
                for (int y = -2; y <= 2; y++)
                    for (int z = -2; z <= 2; z++)
                        AddGradientSample(target.Thermals.GetAtCell(target.Cell + new Vector3I(x, y, z)));
        }

        private static void AddGradientSample(ThermalBlock block)
        {
            if (block == null || block.Node == null || !gradientSeen.Add(block)) return;
            float value = block.Node.Temperature;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 100000) return;
            gradientPositions.Add(((Vector3D)block.Block.Min + (Vector3D)block.Block.Max) * .5);
            gradientTemperatures.Add(value);
        }

        private static Vector2 GradientUv(Vector3D world)
        {
            Vector2 uv;
            if (gradientUvs.TryGetValue(world, out uv)) return uv;
            Vector3D p = Vector3D.Transform(world, gradientInverse) / gradientCell;
            double sum = 0, weight = 0, nearestDistance = double.MaxValue;
            float nearest = sampleKelvin;
            for (int i = 0; i < gradientPositions.Count; i++)
            {
                double d = Vector3D.DistanceSquared(p, gradientPositions[i]);
                if (d < nearestDistance) { nearestDistance = d; nearest = gradientTemperatures[i]; }
                double w = Math.Exp(-d / 2);
                sum += w * gradientTemperatures[i]; weight += w;
            }
            double temperature = weight > 1e-12 ? sum / weight : nearest;
            double t = Math.Max(0, Math.Min(1, (temperature - lowKelvin) / Math.Max(1, highKelvin - lowKelvin)));
            uv = new Vector2((float)((.5 + 255 * t) / 256), .5f);
            gradientUvs[world] = uv;
            return uv;
        }

        private static void DrawGradient(ThermalVisionWorldTriangle triangle)
        {
            Vector3 normal = triangle.Normal;
            MyTransparentGeometry.AddTriangleBillboard(triangle.A, triangle.B, triangle.C,
                normal, normal, normal, GradientUv(triangle.A), GradientUv(triangle.B), GradientUv(triangle.C),
                State.Current == ThermalVisionState.Mode.Cividis ? GradientColour : GradientGrey,
                uint.MaxValue, (triangle.A + triangle.B + triangle.C) / 3, Vector4.One, MyBillboard.BlendTypeEnum.PostPP);
        }
    }
}
