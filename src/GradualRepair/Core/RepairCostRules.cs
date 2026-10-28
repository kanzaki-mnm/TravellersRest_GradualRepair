using System;
namespace GradualRepair
{
    public static class RepairCostRules
    {
        public static readonly int[] MaterialIds = { 1036, 1045, 1050, 1052 };
        public static int Percent(int value) => Math.Max(0, Math.Min(1000, value));
        public static int Amount(int basis, int percent, double remaining = 1)
        {
            if (basis <= 0 || Percent(percent) == 0 || remaining <= 0) return 0;
            return Math.Max(1, checked((int)Math.Round(basis * (Percent(percent) / 100d) * remaining, MidpointRounding.AwayFromZero)));
        }
        public static int[] Materials(RepairJob job, int percent, double remaining = 1) => new[] { Amount(job.Planks, percent, remaining), Amount(job.Nails, percent, remaining), Amount(job.Blocks, percent, remaining), Amount(job.Mortar, percent, remaining) };
        public static int FeeCopper(RepairJob job, int percent, double remaining = 1) => Amount(checked(job.Silver * 100), percent, remaining);
    }
}
