using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Generated;
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
        private const int MaxModelTriangles = ThermalVisionMeshBuild.ModelLimit;
        private const int MaxCachedTriangles = 262144;
        private const int MaxFrameTriangles = 98304;
        private const int MaxCachedModels = 128;
        private const int MaxParts = 32;
        private static readonly MyStringId Material = MyStringId.GetOrCompute("GaugeThermalVisionProbe");

        private static readonly ThermalVisionState State = new ThermalVisionState();

        private static readonly ThermalVisionMeshCache<IMyModel> Geometry = new ThermalVisionMeshCache<IMyModel>(MaxCachedModels, MaxCachedTriangles);

        private static readonly ThermalVisionMesh Empty = new ThermalVisionMesh(new Triangle[0], new ThermalVisionMeshBatch[0]);
        private static readonly int[] VisibleBatches = new int[(MaxModelTriangles + 63) / 64];
        private static int batchRejectedTriangles, batchesTested;
        private sealed class PendingGeometry
        {
            public IMyModel Model;
            public ThermalVisionMeshBuild Build;
        }
        private static PendingGeometry pendingGeometry;
        private static LabelBox panel;
        private static ThermalBlock targetBlock;
        private static int drawn;
        private static int examined;
        private static int backfaces, degenerate;
        private static int parts;
        private static bool incomplete;
        private static int frames;

        private static readonly Stopwatch ProbeClock = new Stopwatch();
        private static bool capture, cacheMiss;
        private static bool compositeMode;
        private static float lowKelvin = ThermalVisionPalette.LowKelvin, highKelvin = ThermalVisionPalette.HighKelvin;
        private static string windowIdentity = "223.15..323.15 K";
        private static float sampleKelvin;
        private static string rowIdentity, lastOutcome, rowKey;


        private static void RecordEvent(string message, bool important = true)
        {
            if (Telemetry.Enabled) Telemetry.Vision.Event(Telemetry.SessionSeconds, message, important);
        }


        private static void Outcome(string value)
        {
            if (!capture) return;
            if (rowKey == null || value != lastOutcome)
            {
                lastOutcome = value;
                rowKey = State.Current + " | " + (depthMode ? "DISTANCE ONLY 5000 m" : (sceneMode || regionMode) && automaticRange ? "AUTO" : windowIdentity) + " | " + (rowIdentity ?? "no target") + " | " + value;
            }
        }

        private sealed class ModelSource : IThermalVisionMeshSource
        {
            private readonly IMyModel model;

            public ModelSource(IMyModel model) { this.model = model; }
            public int TriangleCount { get { return model.GetTrianglesCount(); } }

            public bool TryRead(int index, out Triangle triangle)
            {

                triangle = new Triangle();
                if (model.GetDrawTechnique(index) != MyMeshDrawTechnique.MESH) return false;
                var indices = model.GetTriangle(index);
                triangle.A = model.GetVertex(indices.I0);
                triangle.B = model.GetVertex(indices.I1);
                triangle.C = model.GetVertex(indices.I2);
                return true;
            }
        }

        private static int visionUiFlags = -1;
        private static bool renderFailureReported;


        public static void Build()
        {
            if (panel != null) return;

            panel = new LabelBox(HudMain.HighDpiRoot)
            {
                ParentAlignment = ParentAlignments.Top | ParentAlignments.Left
                    | ParentAlignments.InnerV | ParentAlignments.InnerH,

                Offset = new Vector2(24f, -130f),
                AutoResize = true,
                BuilderMode = TextBuilderModes.Lined,

                TextPadding = new Vector2(14f, 10f),

                Color = new Color(16, 25, 34, 235),

                Format = new GlyphFormat(new Color(220, 235, 242), TextAlignment.Left, 0.85f),
                Visible = false,
                UseCursor = false,
            };
            panel.textElement.UseCursor = false;
            BuildVisionLegend();
        }


        public static void ResetHud()
        {
            if(panel!=null) panel.Visible=false;
            panel=null;
            HideVisionLegend(); LegendElements.Clear(); visionLegend=null; visionLegendRamp=null; visionLegendTitle=null; legendTextKey=null;
            if(surveyImage!=null) surveyImage.Visible=false;
            surveyImage=null;
        }


        public static void Reset()
        {
            RecordEvent("HUD/world reset");
            Stop();
            ClearIndependentFleet();
            panel = null;
            HideVisionLegend(); LegendElements.Clear(); visionLegend=null; visionLegendRamp=null; visionLegendTitle=null; legendTextKey=null;
            surveyImage = null;
            lowKelvin = ThermalVisionPalette.LowKelvin; highKelvin = ThermalVisionPalette.HighKelvin;
            windowIdentity = "223.15..323.15 K";
            regionMode = false; compositeMode = false; sceneMode = false; surveyMode = false; depthMode = false; automaticRange = true; SceneRange.Reset();
        }


        private static void Stop()
        {
            HideVisionLegend();
            PauseIndependentFleet();

            gradientLab = false; smoothFleet = false; smoothCorners = new Dictionary<ThermalVisionRegionPartition.Region,ThermalVisionSurfaceField>(); gradientUvs.Clear(); gradientPositions.Clear(); gradientTemperatures.Clear(); gradientSeen.Clear();
            StopSurvey();
            StopRegions();
            State.Disable();
            visionUiFlags = -1;
            if (panel != null)
            {
                panel.Visible = false;
                HideVisionLegend();
                panel.ParentAlignment = ParentAlignments.Top | ParentAlignments.Left | ParentAlignments.InnerV | ParentAlignments.InnerH;

                panel.Offset = new Vector2(24f, -130f);
            }
            Geometry.Clear();
            pendingGeometry = null;
            targetBlock = null;
            frames = 0;
            SceneCandidates.Clear();
            discoveryAge = 10;
            ExposureClock.Reset();
            rowIdentity = lastOutcome = rowKey = null;
        }

        /// <summary>Controls the thermal-vision display.</summary>
        [ChatCommand("vision")]
        internal static void VisionCommand(string argument = "") => ThermalChatCommands.Reply(Run(argument));

        public static string Run(string argument)
        {
            if (argument != null && argument.Trim().StartsWith("bind ", StringComparison.OrdinalIgnoreCase)) return BindVisionKey(argument.Trim().Substring(5));
            string lowered = argument.ToLowerInvariant();
            if (depthMode && State.Current != ThermalVisionState.Mode.Off && lowered.StartsWith("range "))
                return "depth diagnostic shows distance, not temperature; thermal range does not apply";
            if (lowered == "detail" || lowered == "quick")
            {
                if (!surveyMode || State.Current == ThermalVisionState.Mode.Off)
                    return "enable /thermal vision survey colour or grey first";
                if (!State.Validate(EligibleViewpoint())) return "survey paused: eligible viewpoint required";
                SurveyScan.Invalidate();
                SurveyWidth = lowered == "detail" ? 128 : 64;
                SurveyHeight = SurveyWidth * 9 / 16;

                SurveyScan = new ThermalVisionRayScan<SurveySample>(SurveyWidth * SurveyHeight, 1, 128);
                StartSurvey();
                return "survey " + SurveyWidth + "x" + SurveyHeight + " acquiring; hold steady";
            }
            if (lowered == "scan")
            {
                if (!surveyMode || State.Current == ThermalVisionState.Mode.Off)
                    return "enable /thermal vision survey colour or grey first";
                if (!State.Validate(EligibleViewpoint())) return "survey paused: eligible viewpoint required";
                StartSurvey();
                return "survey acquiring; hold view steady";
            }
            if (lowered.StartsWith("note "))
            {
                if (!Telemetry.Enabled) return "telemetry is off; /thermal telemetry on first";
                RecordEvent("tester note: " + argument.Substring(5));
                return "test note added to thermal vision telemetry";
            }
            if (lowered == "range auto")
            {
                automaticRange = true;
                SceneRange.Reset();
                discoveryAge = 10;
                if (surveyMode && State.Current != ThermalVisionState.Mode.Off)
                {
                    if (!State.Validate(EligibleViewpoint())) return "survey paused: eligible viewpoint required";
                    StartSurvey();
                    return "survey acquiring with automatic range; hold view steady";
                }
                RecordEvent("scene automatic range reset");
                return "adaptive scene range enabled; fast expansion, gradual contraction";
            }
            if (lowered.StartsWith("range "))
            {
                string[] values = lowered.Substring(6).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float low, high;
                if (values.Length != 2
                    || !float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out low)
                    || !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out high)
                    || float.IsNaN(low) || float.IsNaN(high) || float.IsInfinity(low) || float.IsInfinity(high)
                    || low < -273.15f || high - low < 1f || high > 100000f)
                    return "range requires low high in C (at least -273.15, at most 100000, span at least 1 C)";
                automaticRange = false;
                lowKelvin = low + 273.15f;
                highKelvin = high + 273.15f;
                windowIdentity = lowKelvin.ToString("R", CultureInfo.InvariantCulture) + ".."
                    + highKelvin.ToString("R", CultureInfo.InvariantCulture) + " K";
                rowKey = null;
                frames = 0;
                RecordEvent("range locked " + windowIdentity);
                if (surveyMode && State.Current != ThermalVisionState.Mode.Off)
                {
                    if (!State.Validate(EligibleViewpoint())) return "survey paused: eligible viewpoint required";
                    StartSurvey();
                    return "survey acquiring with locked range; hold view steady";
                }
                return "surface probe range locked: " + low + " to " + high + " C";
            }
            if (lowered.StartsWith("lab ")) lowered = lowered.Substring(4).Trim();
            bool blockPrefix = lowered.StartsWith("blocks ");
            bool requestedGradient = lowered.StartsWith("gradient ");
            bool requestedTarget = lowered.StartsWith("target ");
            bool requestedBlocks = requestedGradient || blockPrefix || lowered == "colour" || lowered == "color" || lowered == "grey" || lowered == "gray";
            bool requestedRegions = requestedBlocks || lowered.StartsWith("regions ");
            bool requestedComposite = lowered.StartsWith("composite ");
            bool requestedScene = requestedComposite || lowered.StartsWith("scene ");
            bool requestedSurvey = lowered.StartsWith("survey ");
            bool requestedDepth = lowered.StartsWith("depth ");
            argument = requestedGradient ? lowered.Substring(9).Trim() : requestedTarget ? lowered.Substring(7).Trim() : blockPrefix ? lowered.Substring(7).Trim() : requestedRegions && !requestedBlocks ? lowered.Substring(8).Trim() : requestedComposite ? lowered.Substring(10).Trim() : requestedDepth ? lowered.Substring(6).Trim() : requestedSurvey ? lowered.Substring(7).Trim() : requestedScene ? lowered.Substring(6).Trim() : lowered;
            if (argument == "off")
            {
                RecordEvent("manual off");
                Stop();
                return "thermal vision OFF";
            }
            ThermalVisionState.Mode mode;
            if (argument == "colour" || argument == "color") mode = ThermalVisionState.Mode.Cividis;
            else if (argument == "grey" || argument == "gray") mode = ThermalVisionState.Mode.WhiteHot;
            else return "vision [lab blocks|lab gradient|lab target|lab regions|regions|scene|survey|depth|composite] colour | grey | scan | detail | quick | off | range auto | range <low C> <high C> | note <text>";

            bool newScene = requestedScene && !sceneMode;
            Stop();
            smoothFleet = requestedGradient || (!blockPrefix && requestedBlocks);
            gradientLab = false;
            regionMode = requestedRegions;
            blockLabMode = requestedBlocks;
            compositeMode = requestedComposite;
            sceneMode = requestedScene;
            surveyMode = requestedSurvey;
            depthMode = requestedDepth;
            if (newScene || requestedRegions) { automaticRange = true; SceneRange.Reset(); }
            if (panel == null)
            {
                RecordEvent("activation rejected: Rich HUD unavailable");
                return "surface probe needs Rich HUD to show coverage and range limits";
            }

            long view = EligibleViewpoint();
            if (!State.Enable(mode, view))
            {
                RecordEvent("activation rejected: ineligible viewpoint");
                return "surface probe requires an on-foot first-person suit or a working local camera view";
            }
            RecordEvent("enabled " + mode + (gradientLab ? " gradient UV lab" : regionMode ? " region field experiment" : compositeMode ? " composite experiment" : depthMode ? " depth diagnostic" : surveyMode ? " survey" : sceneMode ? " scene" : " target") + " viewpoint=" + view
                + " type=" + (MyAPIGateway.Session.CameraController.Entity is IMyCameraBlock ? "camera" : "suit"));
            if (gradientLab) return "GRADIENT LAB: aim within 1000 m; aimed model only, nearby measured heat, 6000 examined triangle cap; lock a useful /thermal vision range <low C> <high C>";
            if (smoothFleet) return "thermal vision STRESS TEST ON: no mod billboard quota; viewport grid gradients, automatic range; /thermal vision off closes";
            if (blockLabMode) return "thermal vision ON: adaptive block/group temperatures at game view distance; /thermal vision off closes";
            if (regionMode) return "REGIONAL LAB ON: archived research renderer, not final thermal vision; /thermal vision off closes";
            if (compositeMode) return "composite experiment ON: 5 km neutral silhouettes, bounded measured surfaces; dark means unmeasured, not cold";
            if (depthMode) return "native-depth experiment ON: distance shading only, NOT TEMPERATURE; 48 layers, 5000 m";
            if (surveyMode)
            {
                StartSurvey();
                return "survey scope ON: hold view steady; collision snapshot within 150 m, hatched surfaces have unknown temperature";
            }
            return sceneMode ? "scene probe ON: simulated grids within 100 m; bounded coverage, terrain/characters unsupported" : "surface probe ON: aim at a modelled block within 15 m; scenery is not temperature-mapped";
        }


        private static long EligibleViewpoint()
        {
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated
                || MyAPIGateway.Session == null) return 0;
            var session = MyAPIGateway.Session;
            var controller = session == null ? null : session.CameraController;
            var controllerEntity = controller == null ? null : controller.Entity;
            var camera = controllerEntity as IMyCameraBlock;
            var player = session == null ? null : session.Player;
            var character = player == null ? null : player.Character;
            var controlled = session == null ? null : session.ControlledObject;
            return new ThermalVisionViewpoint
            {
                IsClient = MyAPIGateway.Utilities != null && !MyAPIGateway.Utilities.IsDedicated,
                HasSessionCamera = session != null && session.Camera != null,
                HasLocalPlayer = player != null,
                PlayerCharacterDead = character != null && character.IsDead,
                ControllerEntityId = controllerEntity == null ? 0 : controllerEntity.EntityId,
                ControllerIsCamera = camera != null,
                CameraActiveLocal = camera != null && camera.IsActiveLocal,
                CameraWorking = camera != null && camera.IsWorking,
                CameraClosing = camera != null && camera.MarkedForClose,
                CharacterEntityId = character == null ? 0 : character.EntityId,
                CharacterClosing = character != null && character.MarkedForClose,
                FirstPerson = controller != null && controller.IsInFirstPersonView,
                ControlledEntityId = controlled == null || controlled.Entity == null ? 0 : controlled.Entity.EntityId,
            }.EligibleEntityId;
        }


        public static void Draw()
        {
            if (State.Current == ThermalVisionState.Mode.Off) return;

            long view = EligibleViewpoint();
            if (!State.Validate(view))
            {
                if(panel!=null) panel.Visible=false;
                HideVisionLegend();
                if(surveyImage!=null) surveyImage.Visible=false;
                return;
            }
            if (panel == null) return;
            var gui = MyAPIGateway.Gui;
            bool gameChat = gui != null && gui.ChatEntryVisible;
            bool frameworkChat = BindManager.IsChatOpen;
            bool cursor = gui != null && gui.IsCursorVisible;
            int uiFlags = (gameChat ? 1 : 0) | (frameworkChat ? 2 : 0) | (cursor ? 4 : 0) | (gui == null ? 8 : 0);
            var blacklist = Telemetry.Enabled ? BindManager.BlacklistMode : SeBlacklistModes.None;
            uiFlags |= (int)blacklist << 4;
            uiFlags |= (int)HudMain.InputMode << 8;
            if (uiFlags != visionUiFlags)
            {
                visionUiFlags = uiFlags;
                RecordEvent("vision UI: game-chat=" + gameChat + " framework-chat=" + frameworkChat + " cursor=" + cursor
                    + " framework-input=" + HudMain.InputMode + " client-blacklist=" + blacklist);
            }
            if (ThermalVisionViewPolicy.SuppressForUi(gui != null, gameChat, frameworkChat, cursor))
            {
                if (Telemetry.Enabled) Telemetry.Vision.Suppress();
                panel.Visible = false;
                HideVisionLegend();
                if (surveyImage != null) surveyImage.Visible = false;
                if (surveyScanning && !surveyWaitingForUi)
                {
                    SurveyScan.Invalidate();
                    surveyScanning = false;
                    surveyStatus = "CAPTURE CANCELLED / /thermal vision scan to retry";
                }
                return;
            }

            capture = Telemetry.Enabled;
            cacheMiss = false;
            sampleKelvin = float.NaN;
            drawn = examined = backfaces = degenerate = batchRejectedTriangles = batchesTested = 0;
            incomplete = false;
            if (capture || sceneMode || regionMode) ProbeClock.Restart();
            try
            {
                if (regionMode) DrawRegions();

                else if (compositeMode) { DrawCompositeContext(); DrawScene(); }
                else if (depthMode) DrawDepthLayers(); else if (surveyMode) DrawSurvey(); else if (sceneMode) DrawScene(); else DrawTarget();
                UpdateVisionLegend();
                renderFailureReported=false;
            }
            catch (Exception exception)
            {
                if(!renderFailureReported) Telemetry.Exception("ThermalVisionProbe.Draw", exception);
                if (capture)
                {
                    Telemetry.Vision.Failure();
                    Outcome("render-error");
                    if(!renderFailureReported) RecordEvent("render-error: " + exception.Message);
                }
                panel.Visible = false;
                HideVisionLegend();
                if(!renderFailureReported)
                {
                    MyLog.Default.WriteLineAndConsole("[Thermodynamics] Thermal surface probe frame failed: " + exception);
                    MyAPIGateway.Utilities.ShowNotification("Thermal frame failed; retrying; see game log", 4000);
                    renderFailureReported=true;
                }
            }
            finally
            {
                if (capture || sceneMode || regionMode) ProbeClock.Stop();
                if (capture)
                {
                    Telemetry.Vision.Frame(rowKey ?? "render-error before target", sampleKelvin, compositeMode ? Math.Max(0, drawn - 4) : drawn,
                        Math.Min(examined, MaxFrameTriangles), incomplete, cacheMiss, (float)ProbeClock.Elapsed.TotalMilliseconds, backfaces, degenerate);
                }
                if (State.Current == ThermalVisionState.Mode.Off) Stop();
            }
        }


        private static void DrawTarget()
        {
            Crosshair.Target target;
            string targetReason;
            bool found = Crosshair.Resolve(gradientLab ? 1000 : Crosshair.ReachMetres, out target, out targetReason);
            ThermalBlock next = found ? target.Block : null;
            if (next != targetBlock)
            {
                targetBlock = next;
                rowIdentity = lastOutcome = rowKey = null;
                frames = 0;
            }
            drawn = 0;
            examined = 0;
            parts = 0;
            incomplete = false;
            if (capture && rowIdentity == null && found)
            {
                rowIdentity = target.Block.Block.BlockDefinition.Id.ToString();
                RecordEvent("target " + rowIdentity + " grid=" + target.Thermals.Grid.EntityId
                    + " cell=" + target.Block.Block.Position, false);
            }
            if (!found) Outcome(gradientLab ? "gradient-" + targetReason + "-1000m" : "no-raycast-target");
            bool publish = frames++ % 6 == 0;
            string status = gradientLab ? "Aim at a simulated block within 1000 m | " + targetReason : "Aim at a simulated block within 15 m";
            if (found)
            {
                Vector3 srgb;
                if (!ThermalVisionPalette.TrySample(target.Block.Node.Temperature, State.Current, lowKelvin, highKelvin, out srgb))
                {
                    status = "NO DATA: invalid temperature";
                    Outcome("invalid-temperature");
                }
                else
                {
                    sampleKelvin = target.Block.Node.Temperature;
                    if (gradientLab) PrepareGradient(target);
                    var entity = target.Block.Block.FatBlock as MyEntity;
                    Vector3 linear = ThermalVisionPalette.ToLinear(srgb);
                    if (entity != null)
                        DrawEntity(entity, new Vector4(linear, 1f), target.Camera.Translation, 0);
                    string geometryStatus = entity == null

                        ? DrawArmour(target, new Vector4(linear, 1f)) : null;
                    Outcome(geometryStatus ?? (drawn == 0 ? "zero-submitted"
                        : incomplete ? "partial" : "submitted"));
                    if (publish)
                    {
                        status = (target.Block.Node.Temperature - 273.15f).ToString("0") + " C | " + drawn + " triangles";
                        if (gradientLab) status += " | " + gradientPositions.Count + " heat samples | " + gradientUvs.Count + " cached vertex UVs";
                        if (drawn == 0) status += " | " + (geometryStatus ?? "NO SUPPORTED VISIBLE MESH");
                        if (incomplete) status += " | PARTIAL COVERAGE";
                        if (target.Block.Node.Temperature < lowKelvin) status += " | BELOW SCALE";
                        if (target.Block.Node.Temperature > highKelvin) status += " | ABOVE SCALE";
                        if (target.Block.Node.Temperature > target.Block.Instance.Thermal.CriticalTemperature)
                            status += " | OVER LIMIT";
                    }
                }
            }
            if (publish)
            {
                string palette = State.Current == ThermalVisionState.Mode.Cividis ? "CIVIDIS" : "WHITE HOT";

                panel.Text = new RichText((gradientLab ? "GRADIENT LAB / " : "SURFACE PROBE 3 / ") + palette + " / " + (lowKelvin - 273.15f).ToString("0.##")
                    + "–" + (highKelvin - 273.15f).ToString("0.##") + " C LOCK\n"
                    + status + "\nAimed block only; scenery is not temperature-mapped");
            }
            panel.Visible = true;
        }


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
#pragma warning disable CS0618
                    MyTransparentGeometry.AddTriangleBillboard(projected.A, projected.B, projected.C,
                        n, n, n, Vector2.Zero, Vector2.UnitX, Vector2.UnitY, compositeMode ? CompositeSurfaceMaterial : Material, 0,
                        (projected.A + projected.B + projected.C) / 3, colour, compositeMode ? MyBillboard.BlendTypeEnum.PostPP : MyBillboard.BlendTypeEnum.Standard);
#pragma warning restore CS0618
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
