using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        readonly HealthReport health = new HealthReport();
        readonly List<Action> patchChecks = new List<Action>();
        float nextPatchAudit;
        int lastHealthFlags = -1;
        public string[] GetModHealthV1() => health.Read();
        void InstallPatch(MethodInfo target, HarmonyMethod prefix = null, HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null, HarmonyMethod finalizer = null, HarmonyMethod ilmanipulator = null)
        {
            if (target == null) throw new MissingMethodException("Required patch target missing");
            if (transpiler != null || ilmanipulator != null) throw new InvalidOperationException("Unexpected patch role");
            harmony.Patch(target, prefix, postfix, null, finalizer, null);
            foreach (var entry in new[] { Tuple.Create("prefix", prefix), Tuple.Create("postfix", postfix), Tuple.Create("finalizer", finalizer) })
            {
                if (entry.Item2 == null) continue;
                var expected = entry.Item2.method;
                patchChecks.Add(() => PatchIntegrity.Verify(target, harmony.Id, expected, entry.Item1));
            }
        }
        void VerifyHealthStartup(int compiled)
        {
            if (maiVisitFailed) throw new InvalidOperationException("Mai dialogue patch installation failed");
            foreach (var check in patchChecks) check();
            if (NativeGame.Profile != "0.7.6.14.0-hotfix-later") health.Warn("native compatibility", "Alternative profile actually selected: " + NativeGame.Profile + ". Structural matching accepts exactly one candidate; missing or ambiguous matches fail closed.");
            health.Complete("Profile: " + NativeGame.Profile + "; DLL hash (informational only): " + NativeGame.AssemblyHash
                + "\nReferences: native compatibility methods/properties/fields (exact type, parameters, return and static/instance), " + NativeReferences.Count + " cached UI/save/input fields, " + compiled + " compiled game member uses. CLR direct calls retain their compiled identities; reflected operations use the validated cached handles."
                + "\nRequired patches: " + patchChecks.Count + " prefix/postfix/finalizer contracts checked with target, patch method and owner. Rechecked every 5 unscaled seconds outside this API."
                + "\nTitle scope: metadata and patch installation only. Runtime scene objects, damaged/repaired sprites, UI templates/fonts, input-module instance, inventory capacity, dialogue events, building application, cleanliness, traffic, actual repair/cleanup and ledger save/load are NOT verified at title. They are checked by their normal operations; detected failures are recorded here. No game function is called and no save is read/written for diagnosis."
                + "\nFeatures: exterior, interior/cleanup/salvage, construction materials, mailbox/UI/input, Mai dialogue/visit, hygiene, visitors and save hooks. Multiplayer unsupported; tutorial repair controls intentionally unavailable. Disabled hygiene/visitor settings are intentional configuration choices."
                + "\nDetected errors are retained for this plugin lifetime. No manager dependency or separate warning UI. Remove the mod DLL and mod-only data to restore vanilla.");
        }
        void ObserveHealth()
        {
            int flags = (failed ? 1 : 0) | (paymentRollbackFailed ? 2 : 0) | (hygieneFailed ? 4 : 0) | (visitorsFailed ? 8 : 0)
                | (counterFailed ? 16 : 0) | (debrisFailed ? 32 : 0) | (salvageFailed ? 64 : 0) | (maiVisitFailed ? 128 : 0) | (mailboxEntryFailed ? 256 : 0);
            if (flags != lastHealthFlags) {
                lastHealthFlags = flags;
                if (flags != 0) health.Fail("runtime feature guards", "Failure mask=" + flags + " (save/repair=1, refund=2, hygiene=4, traffic=8, counter=16, debris=32, salvage=64, Mai=128, mailbox=256). " + repairFault);
            }
            if (!healthStartupComplete || Time.unscaledTime < nextPatchAudit) return;
            nextPatchAudit = Time.unscaledTime + 5;
            try { foreach (var check in patchChecks) check(); }
            catch (Exception ex) {
                health.Fail("patch ownership", ex.Message);
                startupFailed = true; failed = true;
                Close(); harmony.UnpatchSelf(); Logger.LogError(ex);
            }
        }
        void RecordHealthEvent(string kind, object data)
        {
            if (kind.EndsWith("_error", StringComparison.Ordinal) || kind.EndsWith("_disabled", StringComparison.Ordinal)) health.Fail(kind, data?.ToString());
            else if (kind.EndsWith("_pending", StringComparison.Ordinal)) health.Warn(kind, "Runtime asset unavailable; operation defers until resolved. " + data);
        }
    }
}

