using System.Collections.Generic;

namespace Thermodynamics.Presentation
{
    /// <summary>Cancels suspended preparation before it can resume against changed topology.</summary>
    public static class ThermalVisionRefreshLifecycle
    {
        public static bool CancelChanged(ref IEnumerator<bool> work,long startedRevision,long currentRevision)
        {
            if(work==null || startedRevision==currentRevision) return false;
            var stale=work;
            work=null;
            stale.Dispose();
            return true;
        }
    }
}
