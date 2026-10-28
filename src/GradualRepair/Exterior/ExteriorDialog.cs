using System;
using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        static bool ShouldSubmitRepair(bool keyboardSubmit, bool nativeSubmit, bool mouseGesture) => keyboardSubmit || (nativeSubmit && !mouseGesture);
        bool exteriorConfirmation;
        string RepairDialogQuestion(RepairJob job) => RepairText.Format("repair.confirm-question", job.Name);
        void ShowExteriorRepairDialog()
        {
            if (exteriorConfirmation || (repairConfirmation != null && repairConfirmation.HasWindow)) return;
            exteriorConfirmation = true; confirm = false;
            repairPlan?.SetModal(true);
            try { ShowNativeRepairDialog(); }
            catch (Exception ex) { HideNativeRepairDialog(); openedFrame = Time.frameCount; Log("exterior_dialog_error", ex.ToString()); Logger.LogError(ex); }
        }
        void CancelNativeRepairDialog()
        {
            if (!exteriorConfirmation) { Close(); return; }
            HideNativeRepairDialog(); confirm = false; openedFrame = Time.frameCount;
            if (open) { repairSessionControls.MaintainRepairInputMap(); repairSessionControls.MaintainRepairPause(); RefreshRepairPanel(); }
        }
        void ResetExteriorConfirmation()
        {
            repairPlan?.SetModal(false);
            exteriorConfirmation = false;
        }
    }
}


