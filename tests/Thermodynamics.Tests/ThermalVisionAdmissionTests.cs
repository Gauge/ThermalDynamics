using System;
using System.Collections.Generic;
using System.Linq;
using Thermodynamics.Presentation;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Small moving/tied candidate sets match independent full sorting, while bounded snapshot
    /// retention pauses unfinished work and preserves current admission through expiry and reuse.
    /// </summary>
    public class ThermalVisionAdmissionTests
    {
        [Fact]
        public void BoundedAdmissionMatchesFullSortWithTiesAndMovingCandidates()
        {
            var nearest=new ThermalVisionFleetSelection<int>(3);
            double[][] frames={ new[] { 7d,2,2,9,1,5,8 }, new[] { 1d,20,3,2,9,2,0 } };
            foreach(var distances in frames)
                for(int shift=0;shift<distances.Length;shift++)
                    for(int count=0;count<=distances.Length;count++)
                    {
                        nearest.Clear();
                        var offered=new List<int>();
                        for(int i=0;i<count;i++)
                        {
                            int id=(i+shift)%distances.Length;
                            offered.Add(id); nearest.Offer(id,distances[id],id);
                        }
                        nearest.OrderNearToFar();
                        int[] expected=offered.OrderBy(id=>distances[id]).ThenBy(id=>id).Take(3).ToArray();
                        Assert.Equal(expected.Length,nearest.Count);
                        for(int i=0;i<expected.Length;i++)
                        {
                            Assert.Equal(expected[i],nearest[i].Value);
                            Assert.Equal(distances[expected[i]],nearest[i].Distance);
                        }
                    }
        }

        private sealed class View
        {
            public bool Closed;
            public int Pauses;
        }

        private static ThermalVisionViewCache<int,View> Cache(int capacity)
        {
            return new ThermalVisionViewCache<int,View>(capacity,2,
                view=>view.Pauses++,view=>view.Closed);
        }

        [Fact]
        public void RetentionDisposesInactiveWorkAndEvictsOnlyOldInactiveSnapshots()
        {
            var cache=Cache(3);
            var a=new View(); var b=new View(); var c=new View(); var d=new View();
            cache.BeginFrame(); cache.AddAdmitted(1,a,0); cache.AddAdmitted(2,b,0); cache.Trim(0);
            Assert.Equal(0,a.Pauses); Assert.Equal(0,b.Pauses);

            View value;
            cache.BeginFrame(); Assert.True(cache.TryAdmit(1,.5,out value));
            cache.AddAdmitted(3,c,.5); cache.AddAdmitted(4,d,.5); cache.Trim(.5);
            Assert.Equal(3,cache.Count); Assert.Equal(1,b.Pauses);
            Assert.False(cache.TryAdmit(2,.5,out value));
            Assert.Equal(0,a.Pauses); Assert.Equal(0,c.Pauses); Assert.Equal(0,d.Pauses);

            cache.BeginFrame(); Assert.True(cache.TryAdmit(3,1,out value)); cache.Trim(1);
            Assert.Equal(3,cache.Count); Assert.Equal(1,a.Pauses); Assert.Equal(1,d.Pauses);
            cache.BeginFrame(); Assert.True(cache.TryAdmit(3,2.9,out value)); cache.Trim(2.9);
            Assert.Equal(1,cache.Count); Assert.Equal(0,c.Pauses);
            Assert.Equal(1,a.Pauses); Assert.Equal(1,d.Pauses);
        }

        [Fact]
        public void ReturningSnapshotsResumeAndClosedOrClearedViewsReleaseWork()
        {
            var cache=Cache(2); var view=new View(); View value;
            cache.BeginFrame(); cache.AddAdmitted(1,view,0); cache.Trim(0);
            cache.BeginFrame(); cache.Trim(.5);
            Assert.Equal(1,view.Pauses);
            cache.BeginFrame(); Assert.True(cache.TryAdmit(1,1,out value));
            Assert.Same(view,value); cache.Trim(1);
            cache.PauseAll(); cache.PauseAll(); Assert.Equal(2,view.Pauses);
            cache.BeginFrame(); Assert.True(cache.TryAdmit(1,1.5,out value));
            view.Closed=true; cache.BeginFrame();
            Assert.Equal(0,cache.Count); Assert.Equal(3,view.Pauses);
            var replacement=new View(); cache.AddAdmitted(2,replacement,2);
            cache.PauseAll(); cache.BeginFrame();
            Assert.False(cache.TryAdmit(2,4,out value));
            Assert.Null(value); Assert.Equal(0,cache.Count); Assert.Equal(1,replacement.Pauses);
            var fresh=new View(); cache.AddAdmitted(2,fresh,4);
            cache.Trim(4); Assert.Equal(1,cache.Count);
            cache.Clear(); Assert.Equal(0,cache.Count); Assert.Equal(1,fresh.Pauses);
        }
    }
}
