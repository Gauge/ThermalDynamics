namespace Thermodynamics.Presentation
{
    public static class ThermalVisionPreparationBudget
    {
        public const int PreviewGridsPerFrame=8;
        public const int AdmittedGrids=128;
        public const int RetainedViews=192;
        public const double RetainSeconds=2;

        public static bool PreferBackground(double priorityMilliseconds,double backgroundMilliseconds)
        {
            return priorityMilliseconds>4*backgroundMilliseconds;
        }


        public static bool CanAdvance(double elapsedMilliseconds, double preparationStart,
            int visited, int visibleGrids, int idle)
        {
            return visibleGrids>0 && elapsedMilliseconds-preparationStart<2
                && (long)visited<System.Math.Max(4096L,(long)visibleGrids*128) && idle<visibleGrids;
        }
    }
}
