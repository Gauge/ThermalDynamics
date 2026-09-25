using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static VRageRender.MyBillboard;

namespace Thermodynamics
{
    public static class AeroOverlay
    {
        private const double BandScale = 0.02d;

        private const double PickRange = 300d;

        private static readonly MyStringId LineMaterial = MyStringId.GetOrCompute("Square");

        private static readonly List<IMyCubeGrid> GroupGrids = new List<IMyCubeGrid>();

        public static void Draw()
        {
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated) return;
            if (Settings.Instance == null || !Settings.Instance.DebugAeroOverlay) return;
            if (MyAPIGateway.Session == null || MyAPIGateway.Session.Camera == null) return;

            ThermalGrid thermals = Target();
            if (thermals == null || thermals.Grid == null || thermals.Simulation == null) return;

            Vector3D centre;
            float mass;
            Vector3 worldWind;
            SumGroup(thermals, out centre, out mass, out worldWind);
            if (mass <= 0f) return;

            Vector3D eye = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            double radius = thermals.Grid.PositionComp.WorldVolume.Radius;

            DrawCentreOfMass(ref centre, ref eye, thermals.Grid);
            DrawWind(ref centre, ref eye, radius, ref worldWind);

            Vector3D heatingCentre;
            bool hasHeatingCentre = CentreOfHeating(out heatingCentre);
            if (hasHeatingCentre) DrawCentreOfHeating(ref heatingCentre, ref centre, ref eye, thermals.Grid);

            Report(thermals, ref worldWind, mass,
                hasHeatingCentre ? Vector3D.Distance(heatingCentre, centre) : -1d);
        }

        private static bool CentreOfHeating(out Vector3D centre)
        {
            centre = Vector3D.Zero;
            Vector3D weighted = Vector3D.Zero;
            double total = 0d;

            for (int g = 0; g < GroupGrids.Count; g++)
            {
                IMyCubeGrid grid = GroupGrids[g];
                if (grid == null || grid.GameLogic == null) continue;

                ThermalGrid thermals = grid.GameLogic.GetAs<ThermalGrid>();
                if (thermals == null || thermals.Simulation == null) continue;

                ThermalSolver solver = thermals.Simulation.Solver;
                float gridSize = grid.GridSize;
                MatrixD world = grid.WorldMatrix;

                for (int i = 0; i < solver.Nodes.Count; i++)
                {
                    ThermalNode node = solver.Nodes[i];
                    float watts = node.LastFrictionWatts;
                    if (watts <= 0f) continue;

                    Vector3 centreCells =
                        ((Vector3)node.Block.Min + (Vector3)node.Block.MaxExclusive) * 0.5f;
                    Vector3D local = (Vector3D)((centreCells - new Vector3(0.5f)) * gridSize);

                    weighted += Vector3D.Transform(local, world) * watts;
                    total += watts;
                }
            }

            if (total <= 0d) return false;
            centre = weighted / total;
            return true;
        }

        private static void DrawCentreOfHeating(ref Vector3D pressure, ref Vector3D mass,
            ref Vector3D eye, IMyCubeGrid grid)
        {
            const double armMetres = 1.5d;
            Vector4 orange = new Color(255, 160, 40).ToVector4();

            MatrixD world = grid.WorldMatrix;
            XRayLine(pressure - (world.Right * armMetres), pressure + (world.Right * armMetres), ref eye, ref orange, 0.06d);
            XRayLine(pressure - (world.Up * armMetres), pressure + (world.Up * armMetres), ref eye, ref orange, 0.06d);
            XRayLine(pressure - (world.Forward * armMetres), pressure + (world.Forward * armMetres), ref eye, ref orange, 0.06d);

            if (Vector3D.DistanceSquared(pressure, mass) > 0.01d)
            {
                XRayLine(mass, pressure, ref eye, ref orange, 0.04d);
            }
        }

        private static ThermalGrid Target()
        {
            IMyCubeBlock seat = MyAPIGateway.Session.ControlledObject as IMyCubeBlock;
            if (seat != null && seat.CubeGrid != null)
            {
                ThermalGrid flown = seat.CubeGrid.GameLogic == null
                    ? null : seat.CubeGrid.GameLogic.GetAs<ThermalGrid>();
                if (flown != null) return flown;
            }

            MatrixD camera = MyAPIGateway.Session.Camera.WorldMatrix;
            IHitInfo hit;
            MyAPIGateway.Physics.CastRay(
                camera.Translation, camera.Translation + (camera.Forward * PickRange), out hit);

            MyCubeGrid grid = hit == null ? null : hit.HitEntity as MyCubeGrid;
            if (grid == null || grid.GameLogic == null) return null;
            return grid.GameLogic.GetAs<ThermalGrid>();
        }

        private static void SumGroup(ThermalGrid leader, out Vector3D centre, out float mass,
            out Vector3 worldWind)
        {
            IMyGridGroupData group = leader.Grid.GetGridGroup(GridLinkTypeEnum.Physical);

            GroupGrids.Clear();
            if (group != null) group.GetGrids(GroupGrids);
            if (GroupGrids.Count == 0) GroupGrids.Add(leader.Grid);

            EnvironmentState state = leader.LastState;
            worldWind = Vector3.TransformNormal(
                state.WindDirectionLocal * state.WindSpeed, leader.Grid.WorldMatrix);

            centre = Vector3D.Zero;
            mass = 0f;
            for (int i = 0; i < GroupGrids.Count; i++)
            {
                IMyCubeGrid grid = GroupGrids[i];
                if (grid == null || grid.Physics == null || grid.Physics.Mass <= 0f) continue;

                centre += grid.Physics.CenterOfMassWorld * grid.Physics.Mass;
                mass += grid.Physics.Mass;
            }

            if (mass > 0f) centre /= mass;
        }

        private static void DrawCentreOfMass(ref Vector3D centre, ref Vector3D eye, IMyCubeGrid grid)
        {
            const double armMetres = 2.5d;
            Vector4 colour = Color.Yellow.ToVector4();

            MatrixD world = grid.WorldMatrix;
            XRayLine(centre - (world.Right * armMetres), centre + (world.Right * armMetres), ref eye, ref colour, 0.08d);
            XRayLine(centre - (world.Up * armMetres), centre + (world.Up * armMetres), ref eye, ref colour, 0.08d);
            XRayLine(centre - (world.Forward * armMetres), centre + (world.Forward * armMetres), ref eye, ref colour, 0.08d);
        }

        private static void DrawWind(ref Vector3D centre, ref Vector3D eye, double radius,
            ref Vector3 worldWind)
        {
            if (worldWind.LengthSquared() <= 1e-4f) return;

            double reach = Math.Max(4d, radius * 0.75d);
            Vector4 blue = new Color(90, 160, 255).ToVector4();
            Arrow(ref centre, Vector3D.Normalize((Vector3D)worldWind), reach * 0.6d, ref eye, ref blue);
        }

        private static void Arrow(ref Vector3D from, Vector3D direction, double length,
            ref Vector3D eye, ref Vector4 colour)
        {
            if (length < 1d) length = 1d;
            Vector3D tip = from + (direction * length);

            XRayLine(from, tip, ref eye, ref colour, 0.12d);

            Vector3D toEye = eye - from;
            Vector3D sweep = Vector3D.Cross(direction, toEye);
            if (sweep.LengthSquared() < 1e-12d) return;
            sweep = Vector3D.Normalize(sweep);

            double head = length * 0.25d;
            Vector3D back = tip - (direction * head);
            XRayLine(tip, back + (sweep * head * 0.5d), ref eye, ref colour, 0.12d);
            XRayLine(tip, back - (sweep * head * 0.5d), ref eye, ref colour, 0.12d);
        }

        private static void XRayLine(Vector3D from, Vector3D to, ref Vector3D eye,
            ref Vector4 colour, double thickness)
        {
            Vector3D a = eye + ((from - eye) * BandScale);
            Vector3D b = eye + ((to - eye) * BandScale);

            Vector3D delta = b - a;
            double length = delta.Length();
            if (length <= 0d) return;

            MyTransparentGeometry.AddLineBillboard(
                LineMaterial, colour, a, (Vector3)(delta / length), (float)length,
                (float)(thickness * BandScale), BlendTypeEnum.PostPP);
        }

        private static void Report(ThermalGrid thermals, ref Vector3 worldWind, float mass,
            double heatingArm)
        {
            EnvironmentState state = thermals.LastState;
            float friction = thermals.Simulation.FrictionWatts;

            MyAPIGateway.Utilities.ShowNotification(
                "[Aero] wind: " + worldWind.Length().ToString("n1") + " m/s"
                + "  air: " + state.AirDensity.ToString("n3")
                + "  friction: " + (friction / 1000f).ToString("n1") + " kW"
                + "  mass: " + mass.ToString("n0") + " kg"
                + (heatingArm >= 0d
                    ? "  heating centre: " + heatingArm.ToString("n1") + " m from the centre of mass"
                    : "")
                + "  (no aerodynamic force is applied while it is rebuilt)", 1, "White");

            string blocked = Gates(thermals, ref worldWind);
            if (blocked.Length > 0)
            {
                MyAPIGateway.Utilities.ShowNotification("[Aero gates] " + blocked, 1, "Red");
            }

            ReportCrossover(ref worldWind, friction);
            ReportSpeedLimit(thermals.Grid);
        }

        private static void ReportSpeedLimit(IMyCubeGrid grid)
        {
            float cruise;
            float ceiling;
            string line;

            if (RelativeTopSpeedLink.TryLimits(grid, out cruise, out ceiling))
            {
                line = "[Speed] RelativeTopSpeed: cruise " + cruise.ToString("n0")
                    + " m/s, ceiling " + ceiling.ToString("n0") + " m/s";
            }
            else
            {
                float limit = RelativeTopSpeedLink.WorldSpeedLimit(grid.GridSizeEnum == MyCubeSize.Large);
                line = RelativeTopSpeedLink.IsReady
                    ? "[Speed] not governed by RelativeTopSpeed; world limit " + limit.ToString("n0") + " m/s"
                    : "[Speed] the engine's cap, " + limit.ToString("n0") + " m/s (RelativeTopSpeed not installed)";
            }

            MyAPIGateway.Utilities.ShowNotification(line, 1, "White");
        }

        private static void ReportCrossover(ref Vector3 worldWind, float friction)
        {
            float speed = worldWind.Length();
            if (speed <= 0.5f || friction <= 0f)
            {
                return;
            }

            double removing = 0d;
            double adding = 0d;
            for (int g = 0; g < GroupGrids.Count; g++)
            {
                IMyCubeGrid grid = GroupGrids[g];
                if (grid == null || grid.GameLogic == null) continue;

                ThermalGrid thermals = grid.GameLogic.GetAs<ThermalGrid>();
                if (thermals == null || thermals.Simulation == null) continue;

                adding += thermals.Simulation.FrictionWatts;
                IList<ThermalNode> nodes = thermals.Simulation.Solver.Nodes;
                for (int i = 0; i < nodes.Count; i++) removing -= nodes[i].LastConvectionWatts;
            }

            friction = (float)adding;
            double net = friction - removing;
            string line = "[Aero net] air is "
                + (net >= 0d ? "heating" : "cooling")
                + " the hull by " + (Math.Abs(net) / 1000d).ToString("n1") + " kW";

            if (removing > 0d)
            {
                double crossover = CrossoverSpeed(friction, speed, (float)removing);
                line += crossover > 0d
                    ? "; flips at ~" + crossover.ToString("n0") + " m/s at this hull temperature"
                    : "; net heating at every speed at this hull temperature";
            }
            else
            {
                line += "; at ambient, so any airspeed heats";
            }

            MyAPIGateway.Utilities.ShowNotification(line, 1, "White");
        }

        private static double CrossoverSpeed(float friction, float speed, float removing)
        {
            double k = friction / ((double)speed * speed * speed);

            double s = EnvironmentSolver.WindConvectionScale;
            double baseRate = removing / (1d + (s * Math.Sqrt(speed)));

            Func<double, double> netAt = v => (k * v * v * v) - (baseRate * (1d + (s * Math.Sqrt(v))));

            if (netAt(0.1d) >= 0d) return 0d;

            double low = 0.1d, high = 2000d;
            if (netAt(high) < 0d) return 0d;

            for (int i = 0; i < 40; i++)
            {
                double mid = (low + high) * 0.5d;
                if (netAt(mid) < 0d) low = mid; else high = mid;
            }

            return (low + high) * 0.5d;
        }

        private static string Gates(ThermalGrid thermals, ref Vector3 worldWind)
        {
            Settings settings = Settings.Instance;
            string blocked = "";

            if (!settings.EnableFriction) blocked += "EnableFriction off; ";
            if (thermals.LastState.AirDensity <= 0.01f) blocked += "no atmosphere; ";

            float speed = worldWind.Length();
            if (speed <= 0f)
            {
                blocked += "no relative airspeed; ";
            }
            else if (settings.FrictionAtSpeedsAbove > 0f && speed <= settings.FrictionAtSpeedsAbove)
            {
                blocked += "airspeed " + speed.ToString("n0") + " <= FrictionAtSpeedsAbove floor "
                    + settings.FrictionAtSpeedsAbove.ToString("n0") + "; ";
            }

            if (settings.FrictionScale <= 0f) blocked += "FrictionScale 0; ";

            return blocked.TrimEnd(' ', ';');
        }
    }
}
