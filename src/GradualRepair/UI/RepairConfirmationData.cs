using System;

namespace GradualRepair
{
    // A display snapshot only: no payment, progress, save or game-object ownership.
    internal sealed class RepairConfirmationData
    {
        internal string Question { get; }
        internal int[] MaterialIds { get; }
        internal int[] Required { get; }
        internal int[] Owned { get; }
        internal int Fee { get; }
        internal int Money { get; }
        internal bool CanAccept { get; }

        internal RepairConfirmationData(string question, int[] materialIds, int[] required,
            int[] owned, int fee, int money, bool canAccept)
        {
            if (materialIds == null || required == null || owned == null)
                throw new ArgumentNullException("Material display arrays");
            if (materialIds.Length != required.Length || materialIds.Length != owned.Length)
                throw new ArgumentException("Material display arrays must have matching lengths");
            Question = question;
            MaterialIds = (int[])materialIds.Clone();
            Required = (int[])required.Clone();
            Owned = (int[])owned.Clone();
            Fee = fee;
            Money = money;
            CanAccept = canAccept;
        }
    }
}
