using System;
using System.Collections.Generic;

namespace GradualRepair
{
    // Presentation snapshot. Eligibility and resource lookup belong to the coordinator.
    internal sealed class RepairPlanData
    {
        internal int Selected { get; }
        internal int[] Remaining { get; }
        internal int CompletedCount { get; }
        internal string Title { get; }
        internal int[] Required { get; }
        internal int Fee { get; }
        internal int Money { get; }
        internal bool CanPay { get; }
        internal bool AllDone { get; }
        internal bool Gamepad { get; }
        internal bool Onsite { get; }
        internal bool Confirm { get; }
        internal string Notice { get; }
        readonly Dictionary<int, int> owned;

        internal RepairPlanData(int selected, int[] remaining, int completedCount,
            string title, int[] required, int fee, int money, bool canPay,
            bool allDone, bool gamepad, bool onsite, bool confirm, string notice,
            IDictionary<int, int> owned)
        {
            if (remaining == null || required == null || owned == null) throw new ArgumentNullException("Display data");
            if (required.Length != 4) throw new ArgumentException("Four construction material costs are required");
            Selected = selected; Remaining = (int[])remaining.Clone(); CompletedCount = completedCount;
            Title = title; Required = (int[])required.Clone(); Fee = fee; Money = money;
            CanPay = canPay; AllDone = allDone; Gamepad = gamepad; Onsite = onsite;
            Confirm = confirm; Notice = notice; this.owned = new Dictionary<int, int>(owned);
        }
        internal int Owned(int id) => owned.TryGetValue(id, out var count) ? count : 0;
    }
}
