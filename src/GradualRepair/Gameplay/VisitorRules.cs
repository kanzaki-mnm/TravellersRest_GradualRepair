using System;
namespace GradualRepair
{
    public static class VisitorRules
    {
        public static double Rate(double minutes, double initial = .05, double fullHours = 100)
        {
            double start = Math.Max(.01, Math.Min(1, initial));
            if (fullHours <= 0) return 1;
            double progress = Math.Max(0, Math.Min(1, minutes / (fullHours * 60)));
            return start + (1 - start) * progress * progress;
        }
        public static bool Admit(double rate, ref double credit)
        {
            rate = Math.Max(.01, Math.Min(1, rate)); credit = Math.Max(0, Math.Min(.999999, credit)) + rate;
            if (credit + 1e-9 < 1) return false; credit = Math.Max(0, credit - 1); return true;
        }
    }
}
