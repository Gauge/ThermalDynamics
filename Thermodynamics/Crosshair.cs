using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Thermodynamics
{
    public static class Crosshair
    {
        public const double ReachMetres = 15;

        public const float IntoFaceMetres = 0.005f;

        public struct Target
        {
            public ThermalGrid Thermals;

            public ThermalBlock Block;

            public Vector3I Cell;

            public MatrixD Camera;
        }

        public static bool Resolve(out Target target)
        {
            string reason;
            return Resolve(ReachMetres, out target, out reason);
        }

        public static bool Resolve(double reachMetres, out Target target, out string reason)
        {
            reason = "no-hit";
            target = new Target();
            target.Camera = MyAPIGateway.Session.Camera.WorldMatrix;

            Vector3D start = target.Camera.Translation;
            Vector3D end = start + (target.Camera.Forward * reachMetres);

            IHitInfo hit;
            MyAPIGateway.Physics.CastRay(start, end, out hit);
            MyCubeGrid grid = hit == null ? null : hit.HitEntity as MyCubeGrid;
            if (grid == null) { reason = hit == null ? "no-hit" : "hit-not-grid"; return false; }

            ThermalGrid thermals = grid.GameLogic.GetAs<ThermalGrid>();
            if (thermals == null || thermals.Simulation == null) { reason = "grid-not-simulated"; return false; }

            Vector3I cell = HitCell(grid, hit, target.Camera.Forward);
            ThermalBlock bound = thermals.GetAtCell(cell);
            if (bound == null || bound.Node == null) { reason = "hit-cell-no-temperature"; return false; }

            target.Thermals = thermals;
            target.Block = bound;
            target.Cell = cell;
            reason = "target-found";
            return true;
        }

        /// <summary>The cell a ray hit, nudged along the ray so a hit on a face lands in the block behind it.</summary>
        public static Vector3I HitCell(MyCubeGrid grid, IHitInfo hit, Vector3D rayDirection)
        {
            return grid.WorldToGridInteger(hit.Position + (rayDirection * IntoFaceMetres));
        }
    }
}
