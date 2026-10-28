using System;
using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        string repairFault = "", saveFailurePath;
        void ShowStartupFailure(Exception error)
        {
            // Keep this behaviour enabled for OnGUI, but never run gameplay code.
            startupFailed = true; failed = true;
            repairFault = RepairText.Get("compatibility.stopped") + "\n" + error.Message;
            Logger.LogError(error);
        }
        void CompleteLedgerSave()
        {
            dirty = false; saveFailurePath = null;
            // A successful disk write cannot repair an incomplete inventory refund.
            failed = paymentRollbackFailed;
            repairFault = paymentRollbackFailed ? RepairText.Get("repair.payment-cancelled") + RepairText.Get("repair.rollback-failed") : "";
        }
        void OnGUI()
        {
            if (string.IsNullOrEmpty(repairFault)) return;
            var previous = GUI.color; int previousDepth = GUI.depth; GUI.depth = -10000; GUI.color = Color.white;
            var box = new Rect(Mathf.Max(10, (Screen.width - 700) / 2), 20, Mathf.Min(700, Screen.width - 20), 210);
            GUI.Box(box, ""); GUILayout.BeginArea(new Rect(box.x + 16, box.y + 12, box.width - 32, box.height - 24));
            GUILayout.Label(RepairText.Get("safety.warning-title"));
            GUILayout.Label(repairFault);
            if (!paymentRollbackFailed && saveFailurePath != null && GUILayout.Button(RepairText.Get("save.retry")))
            {
                try { store.Commit(saveFailurePath, state); CompleteLedgerSave(); Log("commit_retry_success", new { }); }
                catch (Exception ex) { repairFault = RepairText.Get("save.retry-failed") + ex.Message; }
            }
            GUILayout.EndArea(); GUI.color = previous; GUI.depth = previousDepth;
        }
    }
}
