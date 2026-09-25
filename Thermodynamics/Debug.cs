using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics
{
    public static class Debug
    {
        public static void ShowDebugInfo()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;
            if (Settings.Instance == null || !Settings.Instance.DebugTextOnScreen) return;

            Crosshair.Target target;
            if (!Crosshair.Resolve(out target)) return;

            ThermalGrid thermals = target.Thermals;
            Vector3I cell = target.Cell;
            ThermalNode node = target.Block.Node;
            BlockThermalProperties thermal = node.Thermal;
            ThermalSimulation simulation = thermals.Simulation;

            MyAPIGateway.Utilities.ShowNotification(
                "[Block] " + node.Block.Name + " " + node.Block.Position +
                " T: " + node.Temperature.ToString("n3") +
                " dT: " + node.LastDeltaTemperature.ToString("n4") +
                " exposed: " + node.TotalExposedFaces +
                " (" + node.ExposedArea.ToString("n1") + " m2)", 1, "White");

            MyAPIGateway.Utilities.ShowNotification(
                "[Watts] conduction: " + node.LastConductionWatts.ToString("n1") +
                " radiation: " + node.LastRadiationWatts.ToString("n1") +
                " convection: " + node.LastConvectionWatts.ToString("n1") +
                " solar: " + node.LastSolarWatts.ToString("n1") +
                " friction: " + node.LastFrictionWatts.ToString("n1") +
                " generated: " + node.HeatGenerationWatts.ToString("n1"), 1, "White");

            MyAPIGateway.Utilities.ShowNotification(
                "[Block calc] mass: " + node.Block.Mass.ToString("n0") +
                " thermal mass: " + node.ThermalMass.ToString("n0") +
                " k: " + thermal.Conductivity.ToString("n2") +
                " sh: " + thermal.SpecificHeat.ToString("n2") +
                " em: " + thermal.Emissivity.ToString("n3") +
                " produced: " + node.Block.PowerProducedWatts.ToString("n0") + "W" +
                " consumed: " + (node.Block.PowerConsumedWatts + node.Block.ThrustWatts).ToString("n0") + "W", 1, "White");

            EnvironmentState state = thermals.LastState;
            MyAPIGateway.Utilities.ShowNotification(
                "[Env] ambient: " + state.AmbientTemperature.ToString("n1") + "K" +
                " air: " + state.AirDensity.ToString("n3") +
                " atmos: " + state.AtmosphereFactor.ToString("n3") +
                " wind: " + state.WindSpeed.ToString("n1") + "m/s" +
                " solar: " + state.SolarEnergy.ToString("n0") + "W/m2" +
                (state.IsSolarOccluded ? " (occluded)" : ""), 1, "White");

            ThermalSolver solver = simulation.Solver;
            MyAPIGateway.Utilities.ShowNotification(
                "[Grid] nodes: " + solver.Nodes.Count +
                " links: " + solver.LinkCount +
                " loops: " + solver.Loops.Count +
                " rooms: " + simulation.Rooms.Map.RoomCount +
                " mapper queue: " + simulation.Rooms.PendingCells +
                " substeps: " + solver.LastSubsteps + (solver.LastStepWasClamped ? " (clamped)" : "") +
                " steps: " + thermals.StepsRun, 1, "White");

            MyAPIGateway.Utilities.ShowNotification(
                "[Surface] " + CellSurface.Describe(simulation.Surfaces.GetState(cell)), 1, "White");

            RoomMap map = simulation.Rooms.Map;
            MyAPIGateway.Utilities.ShowNotification(
                "[Room] cell: " + Classify(map, cell) +
                " sealed by state: " + (node.Block.IsSealedByDoorState ? "yes" : "no (door open)") +
                " neighbours: " + Neighbours(map, simulation.Surfaces, cell), 1,
                map.IsExternal(cell) ? "Red" : "White");

            MyAPIGateway.Utilities.ShowNotification(
                "[Faces] " + Exposure(simulation, node.Block), 1, "White");

            MyAPIGateway.Utilities.ShowNotification("[Aero] " + Aero(simulation, node, state), 1, "White");
        }

        private static string Aero(ThermalSimulation simulation, ThermalNode node, EnvironmentState state)
        {
            Vector3 normal = ShapeNormal.Of(simulation.Grid.Occupancy(), node.Block);

            string text = "friction: " + node.LastFrictionWatts.ToString("n1") + "W"
                + "  wind(local): " + Compact(state.WindDirectionLocal * state.WindSpeed) + "m/s";

            return normal == Vector3.Zero
                ? text + "  surface normal: none (no open neighbourhood)"
                : text + "  surface normal: " + Compact(normal);
        }

        private static string Compact(Vector3 v)
        {
            return "(" + v.X.ToString("n1") + " " + v.Y.ToString("n1") + " " + v.Z.ToString("n1") + ")";
        }

        private static string Exposure(ThermalSimulation simulation, BlockInstance block)
        {
            SurfaceAudit.Explain(simulation.Surfaces, block, simulation.Rooms.Map, ExposureScratch);

            string text = "";
            for (int face = 0; face < Face.Count; face++)
            {
                FaceExposure result = ExposureScratch[face];
                if (result.Cells == 0) continue;

                text += Face.Name(face) + " " + result.Exposed + "/" + result.Cells;

                if (result.Sealed > 0) text += " sealed:" + result.Sealed;
                if (result.Mounted > 0) text += " bolted:" + result.Mounted;
                if (result.Interior > 0) text += " indoors:" + result.Interior;

                text += "  ";
            }

            return text;
        }

        private static readonly FaceExposure[] ExposureScratch = new FaceExposure[Face.Count];

        private static string Classify(RoomMap map, Vector3I cell)
        {
            if (map.IsSolid(cell)) return "structure";

            int room = map.RoomIndexOf(cell);
            if (room >= 0) return "room " + room;

            return "external";
        }

        private static string Neighbours(RoomMap map, SurfaceMap surfaces, Vector3I cell)
        {
            string text = "";
            for (int face = 0; face < Face.Count; face++)
            {
                if (text.Length > 0) text += " ";
                text += Face.Name(face).Substring(0, 1) + ":" +
                    (surfaces.IsFaceSealed(cell, face) ? "|" : "o") +
                    Classify(map, cell + Face.Offsets[face]).Substring(0, 1);
            }
            return text;
        }
    }
}
