using System;
using System.Collections.Generic;
using VRageMath;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics.Presentation
{
    public sealed class ThermalVisionBlockDetail
    {
        private readonly int budget, focusLimit;
        private readonly Vector3D eye;

        private readonly List<Region> exact = new List<Region>();

        private readonly List<Region> focus = new List<Region>();

        private readonly List<double> distances = new List<double>();
        private bool overflow;
        public int RefinedBlocks { get; private set; }
        public bool AllExact { get; private set; }
        public int CoarseCapacity { get { return Math.Max(1, budget / 4); } }


        public ThermalVisionBlockDetail(int budget, Vector3D localEye)
        {
            if (budget < 1) throw new ArgumentException("Positive detail budget required");
            this.budget = budget; eye = localEye; focusLimit = budget / 12;
        }


        public void Observe(Region block, bool visible)
        {
            if (!overflow)
            {
                if (exact.Count < budget) exact.Add(block);
                else { exact.Clear(); overflow = true; }
            }
            if (!visible || focusLimit == 0) return;
            Vector3D closest = Vector3D.Max(block.Min, Vector3D.Min(eye, block.Max));
            double distance = Vector3D.DistanceSquared(eye, closest);
            int index = distances.BinarySearch(distance);
            if (index < 0) index = ~index;
            if (index >= focusLimit) return;
            distances.Insert(index, distance); focus.Insert(index, block);
            if (focus.Count > focusLimit) { focus.RemoveAt(focusLimit); distances.RemoveAt(focusLimit); }
        }


        public List<Region> Build(ThermalVisionRegionScan coarse, bool refine)
        {
            AllExact = false; RefinedBlocks = 0;
            if (coarse == null || coarse.Running || coarse.Failure != null || coarse.Generation == 0)
                throw new ArgumentException("A complete coarse field is required");
            ThermalVisionRegionOrder order;
            // Temperature data can arrive between the detail and mean scans.
            // An empty first scan must not replace a populated mean with an
            // empty reusable order; retain the current coarse geometry instead.
            if (refine && !overflow && (exact.Count > 0 || coarse.Count == 0)
                && ThermalVisionRegionOrder.TryBuild(exact, budget, out order))

            { AllExact = true; RefinedBlocks = exact.Count; return new List<Region>(exact); }
            for (int take = refine ? focus.Count : 0; take > 0; take /= 2)
            {
                var field = ThermalVisionRegionPartition.FromScan(coarse, Math.Max(1, budget / 2));
                if (field == null) break;
                RefinedBlocks = 0;
                for (int i = 0; i < take; i++)
                    if (field.TryOverwrite(focus[i])) RefinedBlocks++;
                if (ThermalVisionRegionOrder.TryBuild(field, budget, out order))
                {

                    var result = new List<Region>();
                    for (int i = 0; i < field.Count; i++) result.Add(field[i]);
                    return result;
                }
            }
            RefinedBlocks = 0;

            var fallback = new List<Region>();
            for (int i = 0; i < coarse.Count; i++) fallback.Add(coarse[i]);
            return fallback;
        }
    }
}
