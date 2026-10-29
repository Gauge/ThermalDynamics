using System;
using System.Collections.Generic;

namespace Thermodynamics.Presentation
{
    /// <summary>Bounded nearest-first admission; each frame uses current bounds and distances.</summary>
    public sealed class ThermalVisionFleetSelection<T>
    {
        public struct Candidate
        {
            public T Value;
            public double Distance;
            public long EntityId;
        }

        private readonly int capacity;
        private readonly List<Candidate> candidates;
        private sealed class CandidateComparer : IComparer<Candidate>
        {
            public int Compare(Candidate a,Candidate b)
            { return ThermalVisionFleetSelection<T>.Compare(a,b); }
        }
        private static readonly IComparer<Candidate> NearFirst = new CandidateComparer();

        public ThermalVisionFleetSelection(int capacity)
        {
            if(capacity<1) throw new ArgumentException("Positive fleet capacity required", "capacity");
            this.capacity=capacity;
            candidates=new List<Candidate>(capacity);
        }

        public int Count { get { return candidates.Count; } }
        public Candidate this[int index] { get { return candidates[index]; } }
        public void Clear() { candidates.Clear(); }

        private static int Compare(Candidate a,Candidate b)
        {
            int distance=a.Distance.CompareTo(b.Distance);
            return distance!=0?distance:a.EntityId.CompareTo(b.EntityId);
        }

        public void Offer(T value,double distance,long entityId)
        {
            var candidate=new Candidate { Value=value,Distance=distance,EntityId=entityId };
            // A max heap makes the common overflow rejection constant work.
            if(candidates.Count==capacity)
            {
                if(Compare(candidate,candidates[0])>=0) return;
                int index=0;
                while(true)
                {
                    int child=index*2+1;
                    if(child>=candidates.Count) break;
                    if(child+1<candidates.Count && Compare(candidates[child+1],candidates[child])>0) child++;
                    if(Compare(candidate,candidates[child])>=0) break;
                    candidates[index]=candidates[child]; index=child;
                }
                candidates[index]=candidate;
                return;
            }
            int added=candidates.Count;
            candidates.Add(candidate);
            while(added>0)
            {
                int parent=(added-1)/2;
                if(Compare(candidate,candidates[parent])<=0) break;
                candidates[added]=candidates[parent]; added=parent;
            }
            candidates[added]=candidate;
        }

        // Call after all offers, then Clear before the next frame's offers.
        public void OrderNearToFar() { candidates.Sort(NearFirst); }
    }
}
