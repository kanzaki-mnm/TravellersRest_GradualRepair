using System;
using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        string repairFault = "", saveFailurePath;
        void ShowStartupFailure(Exception error)
        {
            // Keep vanilla running; report the fault without a separate modal UI.
            startupFailed = true; failed = true; health.Fail("startup", error.ToString());
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
    }
}
