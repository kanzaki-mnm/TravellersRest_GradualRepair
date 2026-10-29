using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GradualRepair
{
    [BepInPlugin("s_evans.travellersrest.gradualrepair", "Gradual Repair", "0.29.5")]
    [DefaultExecutionOrder(-1000)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        readonly RepairSessionControls repairSessionControls = new RepairSessionControls();
        static Plugin self;
        Harmony harmony;
        StateStore store;
        RepairState state = new RepairState();
        ConfigEntry<int> materialCostPercent, laborCostPercent;
        bool projectInitialized, visualNativeFacade;
        bool bound, open, wasTimeBlocked, dirty, failed, confirm;
        bool startupFailed, paymentRollbackFailed;
        int selected;
        string message = "", eventLog;
        float nextScan, nextNav;
        int openedFrame;
        readonly Dictionary<string, SpriteRenderer> targets = new Dictionary<string, SpriteRenderer>();
        readonly Dictionary<string, Sprite> originals = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite> replacements = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Vector3> originalPositions = new Dictionary<string, Vector3>();
        readonly Dictionary<string, Vector3> repairedPositions = new Dictionary<string, Vector3>();
        readonly List<string> missing = new List<string>();


        void Awake()
        {
            RepairText.LanguageCode = () => I2.Loc.LocalizationManager.CurrentLanguageCode;
            var cfg = new ConfigFile(Path.Combine(Paths.ConfigPath, "GradualRepair.cfg"), true);
            if (!cfg.Bind("General", "Enabled", true, "Enable staged tavern repairs. Remove the DLL to return to vanilla.").Value) { enabled = false; return; }
            RepairText.Initialize(Path.Combine(Paths.ConfigPath, "GradualRepair", "Localization"), warning => Logger.LogWarning(warning));
            try { NativeGame.Initialize(); Logger.LogInfo("Native compatibility contracts ready: " + NativeGame.Profile + "; diagnostic SHA256=" + NativeGame.AssemblyHash); }
            catch (Exception ex) { ShowStartupFailure(ex); return; }

            materialCostPercent = cfg.Bind("RepairCosts", "MaterialCostPercent", 100, new ConfigDescription("Repair material cost in percent. 100 = standard, 20 = light, 150 = hard, 0 = free. Rounded to nearest whole item, positive costs minimum 1. Native Mai delivery and salvage are unchanged. Restart game after editing.", new AcceptableValueRange<int>(0, 1000)));
            laborCostPercent = cfg.Bind("RepairCosts", "LaborCostPercent", 100, new ConfigDescription("Repair labor cost in percent, calculated in copper. 0 = free; values are clamped to 0..1000. Native Mai delivery unchanged. Restart game after editing.", new AcceptableValueRange<int>(0, 1000)));
            ConfigureHygiene(cfg); ConfigureVisitors(cfg);
            string data = Path.Combine(Paths.ConfigPath, "GradualRepairData");
            store = new StateStore(Path.Combine(data, "v4", "saves"), Path.Combine(Application.persistentDataPath, "GradualRepairRecovery", "v4")); eventLog = Path.Combine(data, "events.jsonl"); self = this;
            try
            {
                harmony = new Harmony("s_evans.travellersrest.gradualrepair");
                harmony.Patch(NativeGame.Hook("SetDirtScore"), new HarmonyMethod(typeof(Plugin), nameof(BeforeNativeDirtScore)), null, null, new HarmonyMethod(typeof(Plugin), nameof(EndNativeDirtScore)), null);
                Patch(NativeGame.Hook("VisitorAdmission"), null, nameof(AfterVisitorAdmission));
                Patch(NativeGame.Hook("SetDirtLevel"), nameof(BeforeNativeDirtLevel), null);
                Patch(typeof(Save), "LoadGame", new[] { typeof(string), typeof(SaveData) }, nameof(BeforeLoad), null);
                Patch(typeof(Save), "SaveGame", new[] { typeof(bool), typeof(string) }, null, nameof(AfterSave));
                harmony.Patch(AccessTools.Method(typeof(Save), "SaveGame", new[] { typeof(bool), typeof(string) }), new HarmonyMethod(typeof(Plugin), nameof(BeforeDebrisSave)), null, null, new HarmonyMethod(typeof(Plugin), nameof(EndDebrisSave)), null);
                Patch(typeof(SaveUI), "StartFadeIntoGame", new[] { typeof(bool), typeof(bool) }, nameof(NewGame), null);
                Patch(typeof(FloorDirt), "Clean", new[] { typeof(int), typeof(float) }, nameof(BeforeInitialDirtClean), nameof(AfterInitialDirtClean));
                Patch(typeof(FloorDirt), "DestroyFloorDirt", Type.EmptyTypes, nameof(BeforeInitialDirtRemoved), null);
                Patch(NativeGame.Hook("PaintWall"), nameof(BeforeWallDecoration), nameof(AfterWallDecoration));
                Patch(NativeGame.Hook("PaintFloor"), nameof(BeforeBuildFloorPaint), nameof(FloorDecorationChanged));
                Patch(NativeGame.Hook("PaintTrim"), nameof(BeforeBuildTrimPaint), null);
                harmony.Patch(NativeGame.Hook("UpdateFloorEdge"), new HarmonyMethod(typeof(Plugin), nameof(BeforeFloorEdge)), new HarmonyMethod(typeof(Plugin), nameof(AfterFloorEdge)), null, new HarmonyMethod(typeof(Plugin), nameof(EndFloorEdge)), null);
                Patch(NativeGame.Hook("FloorEdgeNeighbor"), null, nameof(FloorEdgeNeighbor));
                Patch(typeof(EditorActionsDBAccessor), "GetDecoTile", new[] { typeof(int) }, null, nameof(AgedEdgeDecoration));
                Patch(NativeGame.Hook("SelectBuildingFloor"), null, nameof(AddAgedBuildingChoices));
                Patch(NativeGame.Hook("DecorationName"), nameof(AgedBuildingName), null);
                Patch(NativeGame.Hook("DrawDecoration"), nameof(DrawAgedBuildingTile), null);
                var buildApply = AccessTools.Method(typeof(TavernConstructionModifications), "ApplyChanges"); harmony.Patch(buildApply, new HarmonyMethod(typeof(Plugin), nameof(BeforeBuildApply)), new HarmonyMethod(typeof(Plugin), nameof(AfterBuildApply)), null, new HarmonyMethod(typeof(Plugin), nameof(EndBuildApply)), null);
                Patch(typeof(TavernConstructionModifications), "RevertModifications", Type.EmptyTypes, nameof(BeforeBuildRevert), nameof(AfterBuildRevert));
                Patch(typeof(TavernConstructionModifications), "ResetLists", Type.EmptyTypes, null, nameof(AfterBuildReset));
                Patch(typeof(TavernConstructionModifications), "HasAnyModificationDone", Type.EmptyTypes, null, nameof(HasAgedBuildingChanges));
                Patch(NativeGame.Hook("LoadWorld"), null, nameof(AfterNativeLoad));
                Patch(typeof(InteractObject), "InteractInput", Type.EmptyTypes, nameof(BeforeNativeInteractInput), null);
                Patch(typeof(UseObject), "Update", Type.EmptyTypes, nameof(BeforeNativeToolUpdate), null);
                Patch(typeof(ButtonsContext), "SetPosition", new[] { typeof(int), typeof(Vector3) }, nameof(CounterGuidePositionOverride), null);
                Patch(typeof(ButtonsContext), "AddButton", new[] { typeof(int), typeof(ActionType), typeof(string) }, null, nameof(ObserveInteractionGuide));

                Patch(typeof(NewTutorialManager), "ActivateTutorialObjects", new[] { typeof(bool) }, nameof(BeforeTutorialObjectsSwitch), nameof(AfterTutorialObjectsSwitch));
                Patch(NativeGame.Hook("FinishMai"), nameof(BeforeNativeFinish), null);
                InstallMaiDialogueHooks();
                Log("ready", new { version = "0.29.5", data, savePath = Application.persistentDataPath });
            }
            catch (Exception ex) { harmony?.UnpatchSelf(); ShowStartupFailure(ex); Log("boot_error", ex.ToString()); }
        }
        void Patch(MethodInfo method, string pre, string post)
        { harmony.Patch(method, pre == null ? null : new HarmonyMethod(typeof(Plugin), pre), post == null ? null : new HarmonyMethod(typeof(Plugin), post)); }
        void Patch(Type type, string name, Type[] args, string pre, string post)
        { var method = AccessTools.Method(type, name, args) ?? throw new MissingMethodException(type.Name, name); harmony.Patch(method, pre == null ? null : new HarmonyMethod(typeof(Plugin), pre), post == null ? null : new HarmonyMethod(typeof(Plugin), post)); }
        void Log(string kind, object data)
        { try { File.AppendAllText(eventLog, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), kind, data }) + "\n"); } catch (Exception ex) { Logger.LogError("Repair log: " + ex.Message); } }
        static void BeforeLoad(string __0)
        {
            if (self == null) return;
            self.ResetMaiVisit(); self.salvageFailed = false; self.nextSalvage = 0; self.interactionGesture = new InteractionGesture(); self.ResetVisitorSession(); self.ReleaseHygiene(); self.hygieneFailed = false; self.nativePreparationPending = false; self.Close(); self.ReleaseMailboxEntry(); self.ReleaseAgedBuilding(); self.ReleaseInitialMaterialCache(); self.ReleaseIndoor(); self.RestoreDebris(); self.RestoreCounter(); self.RestoreVisuals(); self.bound = false; self.projectInitialized = false; self.dirty = false; self.failed = false; self.paymentRollbackFailed = false; self.saveFailurePath = null; self.repairFault = "";
            try { self.state = self.store.Load(Save.GetFilePath(__0)); self.bound = true; if (self.store.RecoveredLastLoad) self.repairFault = RepairText.Get("save.recovered"); self.Log("load", new { file = __0, self.state.Completed, self.state.FinalFeePaid, self.state.SaveHash }); }
            catch (Exception ex) { self.failed = true; self.message = RepairText.Get("save.load-failed"); self.repairFault = self.message + RepairText.Get("save.load-help"); self.Log("load_error", ex.ToString()); }
        }
        static void NewGame(bool __0)
        { if (__0 && self != null) { self.ResetMaiVisit(); self.salvageFailed = false; self.nextSalvage = 0; self.interactionGesture = new InteractionGesture(); self.ResetVisitorSession(); self.ReleaseHygiene(); self.hygieneFailed = false; self.nativePreparationPending = false; self.Close(); self.ReleaseMailboxEntry(); self.ReleaseAgedBuilding(); self.ReleaseInitialMaterialCache(); self.ReleaseIndoor(); self.RestoreDebris(); self.RestoreCounter(); self.RestoreVisuals(); self.state = new RepairState(); self.projectInitialized = true; self.bound = true; self.dirty = false; self.failed = false; self.paymentRollbackFailed = false; self.message = ""; self.repairFault = ""; self.saveFailurePath = null; self.Log("new_game", new { }); } }
        static void AfterSave(Save __instance, string __result)
        {
            if (self == null || !self.bound || !self.projectInitialized || !self.Solo()) return;
            string path = null;
            try
            {
                string dir = (string)AccessTools.Field(typeof(Save), "loadedFileSubdirectory").GetValue(__instance);
                path = Path.Combine(dir, __result);
                if (!File.Exists(path)) throw new IOException("Native save file missing");
                self.store.Commit(path, self.state); self.CompleteLedgerSave();
                self.Log("commit", new { file = path, self.state.SaveHash, self.state.Completed, self.state.FinalFeePaid, self.state.CounterCleaned, self.state.ClearedDebris, self.state.CompletedInterior, self.state.CleanedInitialDirt });
            }
            catch (Exception ex) { self.failed = true; self.message = RepairText.Get("save.commit-failed"); self.saveFailurePath = path; if (!self.paymentRollbackFailed) self.repairFault = self.message + RepairText.Get("save.commit-help") + ex.Message; self.Log("commit_error", ex.ToString()); }
        }
        static void AfterNativeLoad()
        {
            if (self == null || !self.bound || MissionsManager.instance == null) return;
            RepairModel.ReconcileProject(self.state, self.NativeComplete()); self.projectInitialized = true;
            self.Log("project_reconciled", new { self.state.ExteriorProject, self.state.Schema, vanillaComplete = self.NativeComplete(), self.state.Completed });
        }
        static bool BeforeNativeFinish(bool __0)
        {
            if (self == null) return true;
            self.Close();
            if (self.Active() && !self.NativeComplete()) { self.nativePreparationPending = true; self.StopDebris(); self.StopCounter(); if (self.debrisGuide || self.counterGuide) ButtonsContext.GetPlayer(1)?.ResetButtons(); self.debrisGuide = self.counterGuide = false; }
            else { self.ReleaseIndoor(); self.RestoreDebris(); self.RestoreCounter(); }
            self.Log("native_finish_allowed", new { self.state.Completed }); return true;
        }
        bool nativePreparationPending;
        bool CleanupVisualActive() => nativePreparationPending || CleanupActive();
        static void BeforeTutorialObjectsSwitch(bool __0)
        {
            if (__0 || self == null || !self.nativePreparationPending) return;
            // Q301 calls this from the fade-out completion callback. Restore occupancy
            // before vanilla releases it; the same call then disables the tutorial roots.
            self.ReleaseIndoor(); self.PrepareDebrisNativeSwitch(); self.RestoreCounter(); self.nativePreparationPending = false; self.debrisNativeSwitched = true;
            self.Log("preparation_visual_release_at_native_switch", new { });
        }
        bool NativeComplete() => MissionsManager.instance != null && MissionsManager.instance.IsMissionComplete(301);
        bool IndoorCleanupProject() => bound && projectInitialized && state.ExteriorProject && state.InteriorProject && !failed && NativeGame.Game != null && PlayerInventory.GetPlayer(1) != null && MissionsManager.instance != null && Solo() && !NativeGame.TutorialRunning;
        bool CleanupActive() => IndoorCleanupProject() && !NativeComplete();
        bool Solo() => !OnlineManager.PlayingOnline() && !GameManager.LocalCoop();
        bool Active() => bound && projectInitialized && state.ExteriorProject && NativeGame.Game != null && PlayerInventory.GetPlayer(1) != null && MissionsManager.instance != null && Solo() && !NativeGame.TutorialRunning;
        bool CanOpen() => !MainUI.IsAnyUIOpen() && !DialogueNPCBase.IsLocalPlayerLockedInScriptedEvent(1);
        bool NearTavern()
        {
            var p = PlayerController.GetPlayer(1);
            var t = targets.Values.FirstOrDefault(x => x != null);
            return p != null && t != null && Vector2.Distance(p.transform.position, t.transform.position) < 12;
        }
        bool IsInterior(RepairJob job) => job.Stage == 5;
        RepairJob JobAt(int index) => index == 13 ? throw new ArgumentOutOfRangeException(nameof(index), "Retired repair slot") : index >= 14 ? RepairModel.InteriorJobs[index - 14] : RepairModel.Jobs[index];
        int Fee(RepairJob job) => IsInterior(job) && state.InteriorCostsWaived ? 0 : RepairCostRules.FeeCopper(job, laborCostPercent.Value, IndoorRemaining(job));
        int Count(int id) => PlayerInventory.GetPlayer(1).NumberOfItems(id);
        int[] Needed(RepairJob job) => IsInterior(job) && state.InteriorCostsWaived ? new int[4] : RepairCostRules.Materials(job, materialCostPercent.Value, IndoorRemaining(job));
        void Scan()
        {
            bool live = targets.Count > 0 && targets.Values.All(t => t != null && t.gameObject.scene.isLoaded);
            if (live && visualNativeFacade == NativeFacadeVisible() && ((exteriorOverlay != null) == (Active() && NativeFacadeVisible()))) return;
            RestoreVisuals(); missing.Clear();
            var roots = Resources.FindObjectsOfTypeAll<Transform>().Where(t => t.name == "Tutorial Tavern" && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded && t.Find("Old/Left Side/Window A Old Left") != null).ToArray();
            if (roots.Length != 1) return;
            visualNativeFacade = !roots[0].gameObject.activeInHierarchy;
            var renderRoot = PrepareExteriorSource(roots[0]);
            foreach (var job in RepairModel.Jobs)
            {
                var a = renderRoot.Find("Old/" + job.OldPath)?.GetComponent<SpriteRenderer>();
                var b = roots[0].Find("New/" + job.NewPath)?.GetComponent<SpriteRenderer>();
                if (a == null || b == null || b.sprite == null) { missing.Add(job.Id); continue; }
                BindVisual(job.Id, a, b);
                if (job.Id == "chimney")
                {
                    var back = a.transform.Find("Chimenea Back Old")?.GetComponent<SpriteRenderer>();
                    var cleanBack = b.transform.Find("Chimenea Back")?.GetComponent<SpriteRenderer>();
                    if (back == null || cleanBack == null || cleanBack.sprite == null) missing.Add(job.Id);
                    else BindVisual(job.Id + "#back", back, cleanBack);
                }
            }
            Log("visual_bind", new { resolved = targets.Count, missing = missing.ToArray() });
        }
        bool NativeFacadeVisible() { if (exteriorOverlay != null) return true; var t = targets.Values.FirstOrDefault(x => x != null); if (t == null) return false; var root = t.transform; while (root != null && root.name != "Tutorial Tavern") root = root.parent; return root != null && !root.gameObject.activeInHierarchy; }
        readonly Dictionary<string, Sprite> damagedSprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Vector3> damagedPositions = new Dictionary<string, Vector3>();
        void BindVisual(string id, SpriteRenderer old, SpriteRenderer clean)
        {
            var target = old;
            targets.Add(id, target); originals.Add(id, target.sprite); replacements.Add(id, clean.sprite); damagedSprites.Add(id, old.sprite);
            originalPositions.Add(id, target.transform.localPosition);
            repairedPositions.Add(id, target.transform.parent.InverseTransformPoint(clean.transform.position));
            damagedPositions.Add(id, target.transform.parent.InverseTransformPoint(old.transform.position));
        }
        void RestoreVisuals()
        { foreach (var pair in targets) if (pair.Value != null && originals.TryGetValue(pair.Key, out var sprite)) { pair.Value.sprite = sprite; pair.Value.transform.localPosition = originalPositions[pair.Key]; } targets.Clear(); originals.Clear(); replacements.Clear(); damagedSprites.Clear(); damagedPositions.Clear(); ReleaseExteriorOverlay(); originalPositions.Clear(); repairedPositions.Clear(); }
        void ApplyVisuals()
        { MaskCompletedFacade(); foreach (var pair in targets) if (pair.Value != null) { bool active = Active(); bool done = state.Completed.Contains(pair.Key.Split('#')[0]); pair.Value.sprite = !active ? originals[pair.Key] : done ? replacements[pair.Key] : damagedSprites[pair.Key]; pair.Value.transform.localPosition = !active ? originalPositions[pair.Key] : done ? repairedPositions[pair.Key] : damagedPositions[pair.Key]; } }
        void Update()
        {
            if (startupFailed || !NativeGame.Ready) return;
            if (store == null) return;
            UpdateMaiVisit();
            UpdateMailboxEntry();
            ObserveHygiene(); ObserveVisitors();
            if (failed) return;
            UpdateSalvage(); InitializeAgedMaterials(); UpdateAgedBuilding(); UpdateIndoor(); UpdateOnsiteAssets();
            UpdateDebris();
            if (debrisWorking) return;
            UpdateCounter();
            if (counterWorking) return;
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1; Scan(); }
            UpdateIndoorInteraction();
            if (!Active()) { if (open && (!onsiteRepair || !IndoorCleanupProject())) Close(); return; }
            if (Input.GetKeyDown(KeyCode.F8)) { if (open) Close(); else if (CanOpen() && NearTavern()) Open(); return; }
            if (!open) return;
            if (onsiteRepair || exteriorConfirmation) return; // Native YesNoDialogueUI owns accept/cancel and its opening-frame guard.
            repairSessionControls.MaintainRepairInputMap();
            if (Time.frameCount <= openedFrame) return;
            var input = PlayerInputs.GetPlayer(1);
            if (Input.GetKeyDown(KeyCode.Escape) || input.GetButtonDown(ActionType.UIBack) || input.GetButtonDown(ActionType.ClosePopUp)) { Close(); return; }
            if (!onsiteRepair) UpdateFieldPointer();
            if (!onsiteRepair && Time.unscaledTime >= nextNav)
            {
                Vector2 direction = Vector2.zero;
                // Controller UI navigation can be an axis binding, for which
                // GetButtonDown(Up/Down) does not report a directional press.
                float vertical = repairSessionControls.VerticalNavigation();
                if (input.GetButtonDown(ActionType.Down) || vertical < -.5f || Input.GetKeyDown(KeyCode.DownArrow)) direction = Vector2.down;
                else if (input.GetButtonDown(ActionType.Up) || vertical > .5f || Input.GetKeyDown(KeyCode.UpArrow)) direction = Vector2.up;
                else if (input.GetButtonDown(ActionType.Left) || Input.GetKeyDown(KeyCode.LeftArrow)) direction = Vector2.left;
                else if (input.GetButtonDown(ActionType.Right) || Input.GetKeyDown(KeyCode.RightArrow)) direction = Vector2.right;
                if (direction != Vector2.zero) { NavigateField(direction); nextNav = Time.unscaledTime + .15f; }
            }
            // Mouse clicks belong to the specific row, preview, or action button. Native UIInteract also maps to left click and would confirm the old selection before the row receives OnPointerClick.
            bool mouseGesture = Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0);
            if (ShouldSubmitRepair(Input.GetKeyDown(KeyCode.Return), input.GetButtonDown(ActionType.UIInteract) || input.GetButtonDown(ActionType.Interact), mouseGesture)) Choose(selected);
            PollRepairPanel();
        }
        void LateUpdate() { if (!startupFailed && store != null) { if (open && !onsiteRepair) { repairSessionControls.MaintainRepairInputMap(); repairSessionControls.MaintainRepairPause(); } ApplyVisuals(); ApplyCounter(); ApplyDebris(); ApplyIndoor(); ApplyIndoorGuide(); RenderFieldIfNeeded(); } }
        void Open()
        {
            if (open || !Active()) return;
            onsiteRepair = false; Scan(); open = true; openedFrame = Time.frameCount; confirm = false; selected = Array.FindIndex(RepairModel.Jobs, j => RepairModel.Available(state, j)); if (selected < 0) selected = 0;
            wasTimeBlocked = WorldTime.TickTimeIsBlocked(); WorldTime.BlockTickTime(true);
            PlayerInputs.DisableKeyboardInputsAndMovement(1, this);
            repairSessionControls.BeginRepairInputMap();
            try { repairSessionControls.BeginRepairPause(); ShowRepairPanel(); } catch (Exception ex) { Close(); Log("repair_ui_error", ex.ToString()); Logger.LogError(ex); return; }
        }
        void Close()
        {
            if (!open) return; open = false; repairSessionControls.EndRepairPause(); confirm = false; HideNativeRepairDialog(); HideRepairPanel(); onsiteRepair = false; ClearIndoorGuide();
            try { repairSessionControls.EndRepairInputMap(); if (PlayerInputs.GetPlayer(1) != null) PlayerInputs.EnableKeyboardInputsAndMovement(1, this); WorldTime.BlockTickTime(wasTimeBlocked); } catch (Exception ex) { Logger.LogWarning(ex.Message); }
        }
        void Choose(int index)
        {
            if (index == 13) return; // Retired final-fee slot; retained indices keep indoor jobs stable.
            if (!open || failed || (!Active() && !(onsiteRepair && IndoorCleanupProject()))) return;
            if (index != selected) { selected = index; confirm = false; return; }
            var job = JobAt(index);
            if (!Active() && !IsInterior(job)) return;
            if (IsInterior(job) ? (!state.InteriorProject || state.CompletedInterior.Contains(job.Id)) : !RepairModel.Available(state, job)) { confirm = false; return; }
            if (IsInterior(job) ? !IndoorAvailable() : (!targets.ContainsKey(job.Id) || missing.Contains(job.Id))) { confirm = false; return; }
            if (!onsiteRepair && !exteriorConfirmation && index < 13) { ShowExteriorRepairDialog(); return; }
            int[] n = Needed(job);
            if (Count(1036) < n[0] || Count(1045) < n[1] || Count(1050) < n[2] || Count(1052) < n[3] || Money.ToCopper() < Fee(job)) { confirm = false; return; }
            if (!confirm) return; // Only the confirmation dialog can authorize payment.
            Pay(job);
        }
        void Pay(RepairJob job)
        {
            int[] ids = RepairCostRules.MaterialIds; int[] before = ids.Select(Count).ToArray(); int money = Money.ToCopper(); int[] n = Needed(job);
            try
            {
                var nextState = state.Copy(); if (IsInterior(job)) nextState.CompletedInterior.Add(job.Id); else nextState.Completed.Add(job.Id); RepairModel.Validate(nextState);
                for (int i = 0; i < ids.Length; i++) if (n[i] > 0) PlayerInventory.RemoveItems(1, ids[i], n[i], true);
                Money.MinusPrice(new Price(Fee(job), 0, 0));
                for (int i = 0; i < ids.Length; i++) if (Count(ids[i]) != before[i] - n[i]) throw new InvalidOperationException("Material debit mismatch");
                if (Money.ToCopper() != money - Fee(job)) throw new InvalidOperationException("Money debit mismatch");
                state = nextState; dirty = true; confirm = false;
                Log("purchase", new { job = job.Id, before, after = ids.Select(Count).ToArray(), moneyBefore = money, moneyAfter = Money.ToCopper(), state.Completed, state.CompletedInterior, state.FinalFeePaid });
            }
            catch (Exception ex)
            {
                try
                {
                    for (int i = 0; i < ids.Length; i++) { int refund = before[i] - Count(ids[i]); if (refund > 0 && PlayerInventory.GetPlayer(1).AddItems(new ItemInstance(ItemDatabaseAccessor.GetItem(ids[i])), refund) != 0) throw new InvalidOperationException("Refund inventory full"); }
                    int refundMoney = money - Money.ToCopper(); if (refundMoney > 0) Money.AddPrice(new Price(refundMoney, 0, 0));
                    if (ids.Where((id, i) => Count(id) != before[i]).Any() || Money.ToCopper() != money) throw new InvalidOperationException("Refund mismatch");
                }
                catch (Exception refund) { paymentRollbackFailed = true; failed = true; repairFault = RepairText.Get("repair.payment-cancelled") + RepairText.Get("repair.rollback-failed"); Log("refund_error", refund.ToString()); }
                confirm = false; if (failed) message = RepairText.Get("repair.payment-cancelled") + RepairText.Get("repair.rollback-failed"); Log("payment_error", ex.ToString());
            }
        }
        void OnDisable() { ReleaseMaiVisit(); ReleaseMailboxEntry(); if (!NativeGame.Ready) return; ReleaseHygiene(); nativePreparationPending = false; Close(); repairConfirmation?.Release(); ReleaseAgedBuilding(); ReleaseInitialMaterialCache(); ReleaseIndoor(); RestoreDebris(); RestoreCounter(); RestoreVisuals(); }
        void OnDestroy() { ReleaseMaiVisit(); RemoveMaiConversation(); ReleaseMailboxEntry(); if (!NativeGame.Ready) return; ReleaseHygiene(); nativePreparationPending = false; Close(); repairConfirmation?.Release(); ReleaseAgedBuilding(); ReleaseInitialMaterialCache(); ReleaseIndoor(); RestoreDebris(); RestoreCounter(); RestoreVisuals(); if (counterSprite != null) Destroy(counterSprite); if (counterTexture != null) Destroy(counterTexture); harmony?.UnpatchSelf(); if (self == this) self = null; }
    }
}

































































