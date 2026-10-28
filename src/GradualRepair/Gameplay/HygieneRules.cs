using System;
namespace GradualRepair
{
    public static class HygieneRules
    {
        public const int MaximumAreaDirt = 1000;
        public static int ExtraDirt(bool applicable, bool counterCleaned, bool cornerCleared, int counter, int corner)
        {
            if (!applicable) return 0;
            return (counterCleaned ? 0 : Math.Max(0, Math.Min(MaximumAreaDirt, counter))) + (cornerCleared ? 0 : Math.Max(0, Math.Min(MaximumAreaDirt, corner)));
        }
        public static float EffectiveScore(float native, int extra) => native + Math.Max(0, Math.Min(MaximumAreaDirt * 2, extra));
        // DirtLevel enum order is validated against both supported native builds.
        public static int LevelIndex(float native, int extra)
        {
            float score = EffectiveScore(native, extra);
            return score >= 1000 ? 5 : score >= 500 ? 4 : score >= 200 ? 3 : score >= 100 ? 2 : score >= 1 ? 1 : 0;
        }
    }
}
