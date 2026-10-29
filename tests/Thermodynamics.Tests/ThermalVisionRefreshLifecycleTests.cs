using System.Collections.Generic;
using Thermodynamics.Presentation;

namespace Thermodynamics.Tests
{
    /// <summary>A real suspended enumerator is disposed before changed topology can resume it.</summary>
    public class ThermalVisionRefreshLifecycleTests
    {
        private sealed class WorkSource
        {
            public readonly Dictionary<int,int> Cells=new Dictionary<int,int>();
            public int Visits,Disposals;

            public IEnumerable<bool> Read()
            {
                try
                {
                    foreach(var cell in Cells)
                    {
                        Visits++;
                        yield return true;
                    }
                }
                finally { Disposals++; }
            }
        }

        [Fact]
        public void ChangedEpochDisposesSuspendedWorkAndUnchangedReplacementCanFinish()
        {
            var source=new WorkSource(); source.Cells.Add(1,1); source.Cells.Add(2,2);
            IEnumerator<bool> work=source.Read().GetEnumerator();
            Assert.True(work.MoveNext());
            source.Cells.Add(3,3);
            Assert.True(ThermalVisionRefreshLifecycle.CancelChanged(ref work,10,11));
            Assert.Null(work); Assert.Equal(1,source.Visits); Assert.Equal(1,source.Disposals);
            Assert.False(ThermalVisionRefreshLifecycle.CancelChanged(ref work,10,11));
            Assert.Equal(1,source.Disposals);

            work=source.Read().GetEnumerator();
            Assert.True(work.MoveNext());
            Assert.False(ThermalVisionRefreshLifecycle.CancelChanged(ref work,11,11));
            Assert.NotNull(work);
            while(work.MoveNext()) { }
            work.Dispose();
            Assert.Equal(4,source.Visits); Assert.Equal(2,source.Disposals);
        }
    }
}
