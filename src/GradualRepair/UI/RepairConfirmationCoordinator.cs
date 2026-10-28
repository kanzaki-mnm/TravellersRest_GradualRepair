using System;
using System.Linq;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        RepairConfirmationView repairConfirmation;
        RepairConfirmationView Confirmation => repairConfirmation ?? (repairConfirmation = new RepairConfirmationView(this, Log));
        void UpdateOnsiteAssets() => Confirmation.Warmup(IndoorCleanupProject(), open);
        void ShowNativeRepairDialog()
        {
            Confirmation.Show(ReadRepairConfirmation, () => open && (onsiteRepair || exteriorConfirmation),
                AcceptOnsiteRepair, CancelNativeRepairDialog,
                ex => { Logger.LogError(ex); Close(); }, exteriorConfirmation);
        }
        RepairConfirmationData ReadRepairConfirmation()
        {
            var job = JobAt(selected);
            return new RepairConfirmationData(RepairDialogQuestion(job), RepairCostRules.MaterialIds,
                Needed(job), RepairCostRules.MaterialIds.Select(Count).ToArray(),
                Fee(job), Money.ToCopper(), CanPayOnsite(job));
        }
        bool CanPayOnsite(RepairJob job)
        {
            if (exteriorConfirmation) { if (!Active() || failed || !RepairModel.Available(state, job) || !targets.ContainsKey(job.Id) || missing.Contains(job.Id)) return false; }
            else if (!IndoorCleanupProject() || failed || !state.InteriorProject || state.CompletedInterior.Contains(job.Id) || !IndoorAvailable()) return false;
            var n = Needed(job); return Count(1036) >= n[0] && Count(1045) >= n[1] && Count(1050) >= n[2] && Count(1052) >= n[3] && Money.ToCopper() >= Fee(job);
        }

        void AcceptOnsiteRepair()
        {
            if (!open || (!onsiteRepair && !exteriorConfirmation) || repairConfirmation == null || !repairConfirmation.IsOpen) return;
            var job = JobAt(selected); if (!CanPayOnsite(job)) { RefreshNativeRepairDialog(); return; }
            // This dialog already is the confirmation. Reuse the checked payment
            // path without asking the player to approve the same thing twice.
            confirm = true; Choose(selected);
            if (exteriorConfirmation) { if (state.Completed.Contains(job.Id)) { PlayExteriorRepairFeedback(job); CancelNativeRepairDialog(); } else RefreshNativeRepairDialog(); }
            else if (state.CompletedInterior.Contains(job.Id)) Close(); else RefreshNativeRepairDialog();
        }

        void RefreshNativeRepairDialog() => repairConfirmation?.Refresh();
        void HideNativeRepairDialog()
        {
            repairConfirmation?.Hide();
            ResetExteriorConfirmation();
        }
    }
}
