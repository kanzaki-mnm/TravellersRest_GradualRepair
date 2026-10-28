using BepInEx.Configuration;
using System;
using UnityEngine;



namespace GradualRepair
{
    public sealed partial class Plugin
    {
        ConfigEntry<bool> hygieneEnabled;
        ConfigEntry<int> counterDirtScore, cornerDirtScore;
        int lastHygieneExtra = -1, lastHygieneFlags = -1;
        bool hygieneFailed;
        TavernManager hygieneTavern;
        [ThreadStatic] static TavernManager nativeDirtSetter;
        void ConfigureHygiene(ConfigFile cfg)
        {
            hygieneEnabled = cfg.Bind("Hygiene", "Enabled", true, "Add initial unclean areas to native cleanliness classification only. No direct reputation multiplier or native dirt-score changes.");
            counterDirtScore = cfg.Bind("Hygiene", "CounterDirtScore", 50, new ConfigDescription("Extra dirt score for the unclean counter. Native floor dirt contributes 20 per stain.", new AcceptableValueRange<int>(0, HygieneRules.MaximumAreaDirt)));
            cornerDirtScore = cfg.Bind("Hygiene", "CornerDirtScore", 30, new ConfigDescription("Extra dirt score for uncleared bar-corner bricks and cobwebs. Removed immediately on cleanup.", new AcceptableValueRange<int>(0, HygieneRules.MaximumAreaDirt)));
        }
        int CurrentHygieneExtra()
        {
            bool applicable = hygieneEnabled != null && hygieneEnabled.Value && !hygieneFailed && !failed && !nativePreparationPending && state.InteriorProject && DebrisCleanupActive();
            return HygieneRules.ExtraDirt(applicable, NativeComplete() || counterFailed || state.CounterCleaned, debrisFailed || state.ClearedDebris.Contains("bar-bricks"), counterDirtScore?.Value ?? 0, cornerDirtScore?.Value ?? 0);
        }
        static void BeforeNativeDirtScore(TavernManager __instance, out TavernManager __state) { __state = nativeDirtSetter; nativeDirtSetter = __instance; }
        static Exception EndNativeDirtScore(Exception __exception, TavernManager __state) { nativeDirtSetter = __state; return __exception; }
        static void BeforeNativeDirtLevel(TavernManager __instance, ref DirtLevel __0)
        {
            if (self == null || !NativeGame.Ready || nativeDirtSetter != __instance) return;
            int extra = self.CurrentHygieneExtra();
            if (extra > 0) __0 = (DirtLevel)HygieneRules.LevelIndex(NativeGame.DirtScore(__instance), extra);
        }
        void ObserveHygiene()
        {
            if (hygieneFailed) return;
            if (!bound || !projectInitialized) { ReleaseHygiene(); return; }
            try
            {
                var tavern = NativeGame.Tavern; if (tavern == null) { hygieneTavern = null; lastHygieneExtra = -1; return; }
                int extra = CurrentHygieneExtra();
                int flags = (state.CounterCleaned ? 1 : 0) | (state.ClearedDebris.Contains("bar-bricks") ? 2 : 0) | (NativeComplete() ? 4 : 0);
                float raw = NativeGame.DirtScore(tavern);
                var expected = NativeGame.DirtLevelBlocked ? DirtLevel.Perfect : (DirtLevel)HygieneRules.LevelIndex(raw, extra);
                if (tavern == hygieneTavern && extra == lastHygieneExtra && flags == lastHygieneFlags && NativeGame.DirtLevel(tavern) == expected) return;
                hygieneTavern = tavern; lastHygieneExtra = extra; lastHygieneFlags = flags;
                // Reclassify without adding anything to the native accumulator. Native UI,
                // sounds, tutorial protection and customer mood all keep their own logic.
                NativeGame.SetDirtLevel(tavern, expected);
                Log("hygiene_dirt_state", new { extra, nativeScore = raw, effectiveScore = HygieneRules.EffectiveScore(raw, extra), level = NativeGame.DirtLevel(tavern).ToString(), state.CounterCleaned, cornerCleared = state.ClearedDebris.Contains("bar-bricks"), nativeComplete = NativeComplete(), directReputationPenalty = false });
            }
            catch (Exception e) { hygieneFailed = true; ReleaseHygiene(); Log("hygiene_disabled", e.ToString()); }
        }
        void ReleaseHygiene()
        {
            var tavern = hygieneTavern; hygieneTavern = null; lastHygieneExtra = lastHygieneFlags = -1;
            if (tavern != null) try { NativeGame.SetDirtLevel(tavern, (DirtLevel)HygieneRules.LevelIndex(NativeGame.DirtScore(tavern), 0)); } catch { }
        }
    }
}


namespace GradualRepair
{
    public sealed partial class Plugin
    {
        ConfigEntry<bool> visitorsEnabled; ConfigEntry<float> initialVisitorRate, fullVisitorHours;
        bool visitorsFailed;
        ulong? visitorClock; bool visitorWasOpen; int visitorAccepted, visitorDeclined; double visitorLastLog;
        void ConfigureVisitors(ConfigFile cfg)
        {
            visitorsEnabled = cfg.Bind("Visitors", "Enabled", true, "Reduce ordinary admission opportunities while the tavern becomes known. Does not change native spawns, capacity or important guests.");
            initialVisitorRate = cfg.Bind("Visitors", "InitialAdmissionRate", .05f, new ConfigDescription("Initial ordinary admission opportunity share (0.05 = 5%). Rises slowly at first with cumulative open hours.", new AcceptableValueRange<float>(.01f, 1f)));
            fullVisitorHours = cfg.Bind("Visitors", "HoursToFullTraffic", 100f, new ConfigDescription("Cumulative in-game open hours until native admission is restored. Default 100 = about five 20-hour or eight to nine 12-hour business days. 0 disables the reduction.", new AcceptableValueRange<float>(0, 1000f)));

        }
        bool VisitorsActive() => visitorsEnabled != null && visitorsEnabled.Value && !visitorsFailed && bound && projectInitialized && !failed && state.ExteriorProject && Solo() && !NativeGame.TutorialRunning;
        double VisitorRate() => VisitorRules.Rate(state.VisitorOpenMinutes, initialVisitorRate.Value, fullVisitorHours.Value);
        void ResetVisitorSession() { visitorClock = null; visitorWasOpen = false; visitorAccepted = visitorDeclined = 0; visitorLastLog = -30; }
        void ObserveVisitors() { try { ObserveVisitorClock(); } catch (Exception e) { visitorsFailed = true; visitorClock = null; Log("visitors_disabled", e.ToString()); } }
        void ObserveVisitorClock()
        {
            if (!VisitorsActive()) { visitorClock = null; visitorWasOpen = false; return; }
            ulong now = NativeGame.GameMinutes; bool opened = TavernManager.IsOpen() && !WorldTime.TickTimeIsBlocked();
            if (visitorClock.HasValue && now > visitorClock.Value && now - visitorClock.Value <= 10 && opened && visitorWasOpen) { state.VisitorOpenMinutes += now - visitorClock.Value; dirty = true; }
            visitorClock = now; visitorWasOpen = opened;
            if (state.VisitorOpenMinutes >= visitorLastLog + 30) { visitorLastLog = state.VisitorOpenMinutes; Log("visitors_status", new { state.VisitorOpenMinutes, rate = VisitorRate(), accepted = visitorAccepted, declined = visitorDeclined }); }
        }
        static void AfterVisitorAdmission(CustomerStateSpawning __instance, ref bool __result)
        {
            if (!__result || self == null || !self.VisitorsActive()) return;
            var customer = NativeGame.SpawningCustomer(__instance); if (customer == null || customer.customerType != CustomerType.Traveller) return;
            double rate = self.VisitorRate(); if (rate >= .999999) return;
            __result = VisitorRules.Admit(rate, ref self.state.VisitorAdmissionCredit); self.dirty = true;
            if (__result) self.visitorAccepted++; else self.visitorDeclined++;
        }
    }
}
