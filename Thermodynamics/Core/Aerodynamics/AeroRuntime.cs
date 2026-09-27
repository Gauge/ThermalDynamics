using System;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>
    /// Per-grid profile lifecycle shared by the engine adapter and offline rigs. A changed hull,
    /// resolution or input revision suppresses forces until bounded preparation completes.
    /// </summary>
    public sealed class AeroRuntime
    {
        private GridModel grid;
        private AeroProfileBuilder builder;
        private int topology, directions;
        private long revision;
        private Vector3 centreAnchor;
        private bool centrePending;

        public AeroProfile Profile { get; private set; }
        public bool Ready { get; private set; }
        public int BuildWork { get; private set; }
        public int CentreWork { get; private set; }

        /// <summary>Off clears retained geometry; a settled disabled call does no geometry work or allocation.</summary>
        public void Reset()
        {
            grid = null;
            builder = null;
            Profile = null;
            centrePending = false;
            Ready = false;
            BuildWork = 0;
            CentreWork = 0;
        }

        public bool Prepare(GridModel model, bool enabled, int directionCount, long inputRevision,
            Vector3 centre, int buildBudget, int centreBudget)
        {
            BuildWork = 0;
            CentreWork = 0;
            Ready = false;
            if (!enabled || model == null || model.BlockCount == 0) { Reset(); return false; }
            if (!centre.IsValid()) throw new ArgumentException("The aerodynamic centre must be finite.", "centre");
            int count = directionCount > AeroProfile.CoarseDirections ? AeroProfile.FineDirections : AeroProfile.CoarseDirections;
            if (grid != model || topology != model.Version || directions != count || revision != inputRevision)
            {
                Reset();
                grid = model;
                topology = model.Version;
                directions = count;
                revision = inputRevision;
            }
            if (Profile == null)
            {
                if (buildBudget <= 0) return false;
                if (builder == null) builder = new AeroProfileBuilder(grid, directions, revision);
                BuildWork = builder.Advance(buildBudget, revision);
                AeroProfile result;
                if (!builder.TryGetResult(revision, out result)) return false;
                Profile = result;
                builder = null;
            }
            if (!centrePending || Vector3.DistanceSquared(centreAnchor, centre) > AeroProfile.CentreCacheToleranceSquared)
            {
                centreAnchor = centre;
                centrePending = true;
            }
            int used;
            Ready = Profile.PrepareCentreOfMass(centreAnchor, centreBudget, out used);
            CentreWork = used;
            if (Ready)
            {
                Profile.SetCentreOfMass(centre);
                centrePending = false;
            }
            return Ready;
        }
    }
}
