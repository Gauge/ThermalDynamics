using System;

namespace Thermodynamics.Presentation
{
    public static class ThermalVisionViewPolicy
    {

        public static bool DrawContext(bool fleetMode, bool hasField)
        { return fleetMode || hasField; }


        public static bool SuppressForUi(bool guiAvailable, bool gameChat, bool frameworkChat, bool cursor)
        { return !guiAvailable; }


        public static double FarDistance(double cameraFar, double sessionFar, double near)
        {
            if (Valid(cameraFar, near)) return cameraFar;
            if (Valid(sessionFar, near)) return sessionFar;
            return Math.Max(15000, near + 1);
        }


        public static double BackdropDistance(double far)
        {
            return far * .999999;
        }


        public static int DetailBudget(double diameterPixels, int previous, bool interior)
        {
            if(interior || double.IsNaN(diameterPixels)) return 512;
            double cells=Math.Max(0,diameterPixels)/8;
            double demand=Math.Max(16,Math.Min(512,cells*cells));
            if(previous>=16 && previous<=512 && (previous & (previous-1))==0
                && demand>=previous*.6 && demand<=previous*1.4) return previous;
            int budget=16;
            while(budget<demand && budget<512) budget*=2;
            return budget;
        }


        // Bound the fleet's target partition count instead of preparing 512
        // regions for every visible grid. Each grid keeps at least 16 regions;
        // the independent submission cap still bounds exceptionally large fleets.
        public static int FleetDetailCeiling(int visibleGrids)
        {
            if(visibleGrids<1) return 512;
            int ceiling=512;
            while(ceiling>16 && ceiling/2>=512/visibleGrids) ceiling/=2;
            return ceiling;
        }

        private static bool Valid(double value, double near)
        { return !double.IsNaN(value) && !double.IsInfinity(value) && value > near + 1; }
    }
}
