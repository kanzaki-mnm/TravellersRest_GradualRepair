using System;
using System.Linq;
using HarmonyLib;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        const string MaiCelebration = "GradualRepair/ExteriorComplete";
        static string MaiDeliveryBefore => RepairText.Get("mai.delivery-before");
        static string MaiDeliveryAfter => RepairText.Get("mai.delivery-after");
        static string MaiPraise => RepairText.Get("mai.praise");
        static string MaiPraiseMemory => RepairText.Get("mai.memory");
        static string MaiPraiseHome => RepairText.Get("mai.home");
        MaiNPC visitingMai;
        bool maiVisitOwned, maiCelebrationActive, maiLastLineEnded, maiVisitFailed;
        float nextMaiVisit, maiArrivalRetryAfter;
        DialogueDatabase maiDatabase;
        void InstallMaiDialogueHooks()
        {
            // New dialogue contracts are isolated from the existing repair hooks.
            try
            {
                NativeGame.InitializeMaiContracts();
                Patch(NativeGame.MaiConversation, null, nameof(AfterMaiConversationSelected));
                Patch(NativeGame.MaiAvailable, null, nameof(AfterMaiAvailable));
                Patch(NativeGame.MaiOpened, null, nameof(AfterMaiTavernOpened));
                Patch(typeof(DialogueNPCBase), "OnConversationLineEnd", new[] { typeof(Subtitle) }, null, nameof(AfterMaiLineEnded));
                Patch(typeof(MaiNPC), "OnConversationEnd", new[] { typeof(Transform) }, null, nameof(AfterMaiConversationEnded));
                foreach (var name in new[] { "currentDialogueText", "currentLocalizedDialogueText", "subtitleText" })
                    Patch(AccessTools.PropertyGetter(typeof(DialogueEntry), name), null, nameof(AfterMaiText));
            }
            catch (Exception ex) { maiVisitFailed = true; Logger.LogError("Mai dialogue feature disabled: " + ex); }
        }
        bool MaiPending() => Active() && !failed && NativeComplete() && !state.MaiCongratulationsHeard && RepairModel.Jobs.All(j => state.Completed.Contains(j.Id));
        static bool MaiStoryPriority(string title, Conversation conversation)
        {
            if (string.IsNullOrEmpty(title) || title == MaiCelebration) return false;
            // Explicit event/learning/tutorial branches always win. Talk branches may
            // also advance quests; only the verified random-banter sequence is harmless.
            if (!title.StartsWith("Mai/Talk", StringComparison.Ordinal)) return true;
            return conversation == null || conversation.dialogueEntries.Any(e => !string.IsNullOrWhiteSpace(e.userScript) || !MaiBanterSequence(e.Sequence) || !string.IsNullOrWhiteSpace(e.sceneEventGuid));
        }
        static bool MaiBanterSequence(string sequence)
        {
            if (string.IsNullOrWhiteSpace(sequence)) return true;
            // Native Mai/Talk roots randomize their repeat lines. Unknown commands
            // remain story-priority so future event sequences cannot be swallowed.
            return sequence.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .All(command => command.Trim() == "None()" || command.Trim() == "RandomizeNextEntry()");
        }
        bool MaiCanCelebrate(MaiNPC mai)
        {
            if (maiVisitFailed || !MaiPending() || !TavernManager.IsClosed() || mai == null || mai.inEvent) return false;
            if (NativeGame.MaiFlag(mai)) return true;
            string title = mai.dialogue.conversation;
            return !MaiStoryPriority(title, DialogueManager.MasterDatabase.GetConversation(title));
        }
        void EnsureMaiConversation()
        {
            if (DialogueManager.MasterDatabase.GetConversation(MaiCelebration) != null) return;
            var source = DialogueManager.MasterDatabase.GetConversation("Tutorial/T300/Dialogue2");
            if (source == null) throw new InvalidOperationException("Native Mai praise conversation missing");
            var copy = BuildMaiConversation(source, DialogueManager.MasterDatabase.conversations.Max(c => c.id) + 1);
            maiDatabase = ScriptableObject.CreateInstance<DialogueDatabase>(); maiDatabase.name = "GradualRepair Mai dialogue"; maiDatabase.conversations.Add(copy);
            DialogueManager.AddDatabase(maiDatabase); Log("mai_conversation_registered", new { copy.id });
        }
        static Conversation BuildMaiConversation(Conversation source, int id)
        {
            var copy = new Conversation(source); copy.id = id; copy.Title = MaiCelebration; copy.fields.RemoveAll(f => f.title != "Title" && f.title != "Actor" && f.title != "Conversant");
            copy.dialogueEntries = source.dialogueEntries.Where(e => e.id == 0 || e.id == 1).Select(e => new DialogueEntry(e)).ToList();
            if (copy.dialogueEntries.Count != 2) throw new InvalidOperationException("Unexpected Mai praise entries");
            foreach (var entry in copy.dialogueEntries)
            {
                entry.conversationID = copy.id; entry.conditionsString = ""; entry.userScript = ""; entry.Sequence = ""; entry.ResponseMenuSequence = "";
                entry.fields.RemoveAll(f => f.title == "EventGuid" || f.title.StartsWith("Sequence ", StringComparison.Ordinal));
                entry.outgoingLinks.RemoveAll(l => entry.id != 0 || l.destinationDialogueID != 1);
                foreach (var link in entry.outgoingLinks) { link.originConversationID = copy.id; link.destinationConversationID = copy.id; }
                if (entry.id == 1) { entry.DialogueText = MaiPraise; entry.currentLocalizedDialogueText = MaiPraise; }
            }
            var praise = copy.dialogueEntries.Single(e => e.id == 1);
            foreach (var extra in new[] { new { id = 2, text = MaiPraiseMemory }, new { id = 3, text = MaiPraiseHome } })
            {
                var line = new DialogueEntry(praise); line.id = extra.id; line.outgoingLinks.Clear();
                line.DialogueText = extra.text; line.currentLocalizedDialogueText = extra.text;
                copy.dialogueEntries.Add(line);
            }
            for (int line = 1; line < 3; line++) copy.dialogueEntries.Single(e => e.id == line).outgoingLinks.Add(new Link
            { originConversationID = id, originDialogueID = line, destinationConversationID = id, destinationDialogueID = line + 1 });
            return copy;
        }
        void UpdateMaiVisit()
        {
            if (maiVisitFailed || Time.unscaledTime < nextMaiVisit) return; nextMaiVisit = Time.unscaledTime + .5f;
            try
            {
                if (!Active()) { ReleaseMaiVisit(); return; }
                if (maiCelebrationActive) return;
                if (!TavernManager.IsClosed()) return; // Native open handler owns departure; do not launch a second untracked disappearance.
                if (!MaiPending())
                {
                    if (maiVisitOwned && visitingMai != null) { visitingMai.DisappearMai(); maiVisitOwned = false; }
                    return;
                }
                if (MainUI.IsAnyUIOpen() || DialogueManager.isConversationActive) return;
                var mai = visitingMai ?? NativeGame.Mai ?? FindObjectOfType<MaiNPC>(); if (mai == null) return; visitingMai = mai;
                // Let vanilla select its own current storyline before considering ours.
                NativeGame.MaiConversation.Invoke(mai, null);
                if (!MaiCanCelebrate(mai)) { maiVisitOwned = false; return; }
                EnsureMaiConversation();
                // Delivery leaves Mai visible outdoors with Disabled=false. Visibility
                // alone cannot tell whether she has actually arrived at the counter.
                if (Bar.instance == null || Bar.instance.placeable == null || Bar.instance.placeable.placeableSurface == null) return;
                bool atCounter = mai.transform.parent == Bar.instance.placeable.placeableSurface.transform;
                if (MaiNeedsArrival(mai.animationBase.GetBool("Disabled"), atCounter, Time.unscaledTime, maiArrivalRetryAfter))
                {
                    maiArrivalRetryAfter = Time.unscaledTime + 3f;
                    mai.ActiveMaiInBar(); maiVisitOwned = true;
                    Log("mai_celebration_arrival", new { previouslyAtCounter = atCounter });
                }
            }
            catch (Exception ex) { maiVisitFailed = true; ReleaseMaiVisit(); Logger.LogError("Mai visit stopped: " + ex); Log("mai_visit_error", ex.ToString()); }
        }
        static void AfterMaiTavernOpened(MaiNPC __instance)
        {
            if (self == null || self.visitingMai != __instance) return;
            // Vanilla stores this departure coroutine and cancels it on CLOSE.
            // DisappearMai() starts another coroutine that CLOSE cannot cancel.
            self.maiVisitOwned = false;
            self.maiArrivalRetryAfter = Time.unscaledTime + 1.6f;
        }
        static bool MaiNeedsArrival(bool disabled, bool atCounter, float now, float retryAfter) => (disabled || !atCounter) && now >= retryAfter;
        static void AfterMaiConversationSelected(MaiNPC __instance)
        {
            if (self == null || !self.MaiCanCelebrate(__instance)) return;
            try
            {
                self.EnsureMaiConversation(); __instance.dialogue.conversation = MaiCelebration;
                NativeGame.ClearMaiFlag(__instance);
            }
            catch (Exception ex) { self.maiVisitFailed = true; self.Logger.LogError("Mai celebration unavailable: " + ex); }
        }
        static void AfterMaiAvailable(MaiNPC __instance, ref bool __result)
        { if (!__result && self != null && self.MaiCanCelebrate(__instance)) __result = true; }
        static void AfterMaiLineEnded(DialogueNPCBase __instance, Subtitle __0)
        {
            if (self == null || !(__instance is MaiNPC) || __0?.dialogueEntry == null) return;
            if (DialogueManager.MasterDatabase.GetConversation(__0.dialogueEntry.conversationID)?.Title == MaiCelebration)
            { self.maiCelebrationActive = true; self.maiLastLineEnded = __0.dialogueEntry.id == 3; }
        }
        static void AfterMaiConversationEnded(MaiNPC __instance)
        {
            if (self == null || !self.maiLastLineEnded) return;
            self.state.MaiCongratulationsHeard = true; self.dirty = true; self.maiLastLineEnded = false; self.maiCelebrationActive = false;
            self.Log("mai_celebration_heard", new { });
            // Never dismiss a Mai who still has native story business.
            NativeGame.MaiConversation.Invoke(__instance, null);
            if (!MaiStoryPriority(__instance.dialogue.conversation, DialogueManager.MasterDatabase.GetConversation(__instance.dialogue.conversation))) __instance.DisappearMai();
            self.maiVisitOwned = false;
        }
        static void AfterMaiText(DialogueEntry __instance, ref string __result)
        {
            if (self == null || !self.Active()) return;
            string title = DialogueManager.MasterDatabase.GetConversation(__instance.conversationID)?.Title;
            if (title == MaiCelebration)
            {
                if (__instance.id == 1) __result = MaiPraise;
                else if (__instance.id == 2) __result = MaiPraiseMemory;
                else if (__instance.id == 3) __result = MaiPraiseHome;
            }
            else if (__instance.id == 1) __result = MaiText(title, __result);
        }
        static string MaiText(string title, string text)
        {
            if (title == MaiCelebration) return MaiPraise;
            if (text == null) return text;
            if (title == "Tutorial/T300/Dialogue1") return MaiDeliveryBefore;
            if (title == "Tutorial/T300/Dialogue2") return MaiDeliveryAfter;
            return text;
        }
        void ReleaseMaiVisit()
        {
            if (maiVisitOwned && visitingMai != null && !DialogueManager.isConversationActive) visitingMai.DisappearMai();
            maiVisitOwned = false; visitingMai = null; maiCelebrationActive = false; maiLastLineEnded = false;
        }
        void RemoveMaiConversation() { if (maiDatabase != null) { DialogueManager.RemoveDatabase(maiDatabase); Destroy(maiDatabase); maiDatabase = null; } }
        void ResetMaiVisit() { ReleaseMaiVisit(); nextMaiVisit = 0; maiArrivalRetryAfter = 0; }
    }
}






