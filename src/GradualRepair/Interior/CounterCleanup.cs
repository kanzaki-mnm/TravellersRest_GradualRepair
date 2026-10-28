using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        const float CounterSeconds = 6f;
        SpriteRenderer counter;
        Sprite counterOriginal, counterSprite;
        Texture2D counterTexture;
        Placeable counterPlaceable;
        DoWork counterWork;
        Transform counterWorkAnchor;
        bool counterGuide;
        int displayedGuideKind, displayedGuideFrame = -100;
        bool cleanupPositionCall, restoringGuidePosition;
        Vector3 nativeGuidePosition;
        int nativeGuidePositionFrame = -100;
        bool DisplayedCleanupGuide(int kind) => displayedGuideKind == kind && displayedGuideFrame >= Time.frameCount - 1;
        static void ObserveInteractionGuide(int __0, ActionType __1, string __2)
        {
            if (self == null || __0 != 1 || __1 != ActionType.Interact) return;
            self.displayedGuideKind = __2 == RepairText.Get("action.clean-counter") ? 1 : __2 == RepairText.Get("action.clear-debris") ? 2 : __2 == RepairText.Get("action.repair-floor") || __2 == RepairText.Get("action.repair-wall") ? 3 : 0;
            self.displayedGuideFrame = Time.frameCount;
            if (self.displayedGuideKind != 0 || self.nativeGuidePositionFrame != Time.frameCount) return;
            // The text has become native again. Restore the position originally
            // requested by that native guide, not the previous cleanup position.
            self.restoringGuidePosition = true;
            try { ButtonsContext.SetPosition(1, self.nativeGuidePosition); } finally { self.restoringGuidePosition = false; }
        }
        void SetCleanupGuidePosition(Vector3 position)
        { cleanupPositionCall = true; try { ButtonsContext.SetPosition(1, position); } finally { cleanupPositionCall = false; } }
        bool counterWasHeld;
        float counterNextScan, counterProgress;
        float counterNextDiagnostic;
        int counterDiagnosedBar;
        bool counterWorking, counterFailed;
        bool CounterReady() => !debrisWorking && !counterFailed && CleanupActive() && !failed && !open && !state.CounterCleaned && counter != null && counter.gameObject.activeInHierarchy && CanOpen() && CounterCleaningRules.CanWork(counterWorking, CounterHasCustomers());
        bool CounterHasCustomers()
        {
            var bar = Bar.instance; if (bar == null) return true;
            var target = InteractObject.GetPlayer(1)?.interactingObject;
            if (NativeCleanupPriority()) return true;
            if (target != null && IsWaitingCustomer(target.GetComponentInParent<HumanNPC>())) return true;
            foreach (var npc in bar.waitingAtBar) if (IsWaitingCustomer(npc)) return true;
            if (bar.occupier != null) foreach (var npc in bar.occupier) if (IsWaitingCustomer(npc)) return true;
            return false;
        }
        static bool IsWaitingCustomer(HumanNPC npc) => npc != null && npc.gameObject.activeInHierarchy && npc.customer != null && npc.customer.customerState == CustomerState.WaitingAtBar;
        Vector3 CounterGuidePosition() => counter.bounds.center + Vector3.up * .15f;
        static void CounterGuidePositionOverride(int __0, ref Vector3 __1)
        {
            if (self == null || __0 != 1 || self.restoringGuidePosition) return;
            if (!self.cleanupPositionCall) { self.nativeGuidePosition = __1; self.nativeGuidePositionFrame = Time.frameCount; }
            if (!self.cleanupPositionCall && self.displayedGuideKind == 0 && self.displayedGuideFrame == Time.frameCount) return;
            // The shared guide may now belong to Mai, a customer or floor dirt.
            // Never pin those native interactions to the previous cleanup target.
            if (self != null && self.NativeCleanupPriority()) return;
            if (self.indoorGuide && self.indoorTarget >= 14 && self.IndoorInteractionReady()) { __1 = self.IndoorGuidePosition(); return; }
            if (__0 == 1 && self != null && self.debrisGuide && self.DebrisReady() && self.debrisTarget != null) { __1 = self.DebrisGuidePosition(); return; }
            if (__0 == 1 && self != null && self.counterGuide && self.CounterReady() && self.NearCounter()) __1 = self.CounterGuidePosition();
        }
        Vector3 CounterBarPosition() => counter.bounds.center + Vector3.down * .2f;
        bool NearCounter()
        {
            var player = PlayerController.GetPlayer(1);
            if (player == null || counter == null) return false;
            var position = player.transform.position; var bounds = counter.bounds;
            return CounterCleaningRules.InReach(position.x, position.y, bounds.min.x, bounds.max.x, bounds.min.y, bounds.max.y);
        }
        void FindCounter()
        {
            if (counter != null && counter.gameObject.scene.isLoaded) return;
            RestoreCounter();
            // The live bar is instantiated/renamed by the game. Never search every
            // loaded SpriteRenderer or depend on its prefab's original parent name.
            var bar = Bar.instance;
            if (bar == null || !bar.gameObject.scene.IsValid() || !bar.gameObject.scene.isLoaded) return;
            var tutorial = bar.transform.Find("Tutorial");
            var found = tutorial == null ? null : tutorial.GetComponent<SpriteRenderer>();
            if (found == null || found.sprite == null)
            {
                if (counterDiagnosedBar != bar.GetInstanceID())
                {
                    counterDiagnosedBar = bar.GetInstanceID();
                    var children = new string[bar.transform.childCount]; for (int i = 0; i < children.Length; i++) children[i] = bar.transform.GetChild(i).name;
                    Log("counter_waiting", new { bar = bar.name, children, reason = found == null ? "Tutorial renderer missing" : "Sprite not ready" });
                }
                return;
            }
            if (found.sprite.rect.width != 240 || found.sprite.rect.height != 56) { counterFailed = true; Log("counter_bind_error", new { reason = "Unexpected counter dimensions", sprite = found.sprite.name }); return; }
            counter = found; counterOriginal = counter.sprite; counterPlaceable = bar.placeable;
            if (counterPlaceable == null) { counterFailed = true; Log("counter_bind_error", "Placeable missing"); return; }
            var prefabField = AccessTools.Field(typeof(DoWork), "progressorPrefab");
            GameObject workPrefab = null;
            foreach (var dirt in Resources.FindObjectsOfTypeAll<FloorDirt>())
            {
                var work = dirt.GetComponent<DoWork>();
                if (work != null && (workPrefab = prefabField.GetValue(work) as GameObject) != null) break;
            }
            if (workPrefab == null) throw new InvalidOperationException("Native floor-cleaning progress bar not found");
            var workRoot = new GameObject("GradualRepair Counter Work"); workRoot.transform.SetParent(counter.transform, false);
            counterWorkAnchor = workRoot.transform;
            counterWork = workRoot.AddComponent<DoWork>();
            prefabField.SetValue(counterWork, workPrefab);
            AccessTools.Field(typeof(DoWork), "workBarPosition").SetValue(counterWork, counterWorkAnchor);
            counterWork.inWorkArea = true;
            if (counterSprite == null)
            {
                using (var input = typeof(Plugin).Assembly.GetManifestResourceStream("Counter_Damaged_Cleaned.png"))
                using (var data = new MemoryStream())
                {
                    if (input == null) throw new InvalidDataException("Counter image missing"); input.CopyTo(data);
                    counterTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false); counterTexture.filterMode = FilterMode.Point;
                    if (!ImageConversion.LoadImage(counterTexture, data.ToArray()) || counterTexture.width != 240 || counterTexture.height != 56) throw new InvalidDataException("Counter image must be 240x56");
                    var rect = counterOriginal.rect; var pivot = counterOriginal.pivot;
                    counterSprite = Sprite.Create(counterTexture, new Rect(0, 0, 240, 56), new Vector2(pivot.x / rect.width, pivot.y / rect.height), counterOriginal.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    counterSprite.name = "GradualRepair_Counter_Damaged_Cleaned";
                }
            }
            Log("counter_bound", new { sprite = counterOriginal.name, seconds = CounterSeconds });
        }
        void UpdateCounter()
        {
            try
            {
                if (nativePreparationPending || NativeComplete()) { StopCounter(); return; }
                if (Time.unscaledTime >= counterNextScan) { counterNextScan = Time.unscaledTime + 1; if (bound && !counterFailed) FindCounter(); }
                if (!CleanupActive() || state.CounterCleaned) { StopCounter(); counterWasHeld = false; return; }
                bool ready = CounterReady() && NearCounter();
                if (!ready && CleanupActive() && counter != null && Time.unscaledTime >= counterNextDiagnostic && (Input.GetKey(KeyCode.E) || PlayerInputs.GetPlayer(1).GetButton(ActionType.Interact)))
                {
                    counterNextDiagnostic = Time.unscaledTime + 2;
                    Log("counter_unavailable", new { near = NearCounter(), customers = CounterHasCustomers(), ui = MainUI.IsAnyUIOpen(), state.CounterCleaned, position = PlayerController.GetPlayer(1).transform.position.ToString("F3"), min = counter.bounds.min.ToString("F3"), max = counter.bounds.max.ToString("F3") });
                }
                var input = PlayerInputs.GetPlayer(1);
                bool pressed = Input.GetKey(KeyCode.E) || input.GetButton(ActionType.Interact);
                bool starting = CounterCleaningRules.CanBegin(counterGuide && DisplayedCleanupGuide(1), pressed, counterWasHeld);
                counterWasHeld = pressed;
                bool held = ready && pressed && (counterWorking || starting);
                if (!held) { StopCounter(); return; }
                if (!counterWorking)
                {
                    if (!TryOwnModInteraction()) return;
                    counterWorking = true;
                    PlayerInputs.DisableKeyboardInputsAndMovement(1, this);
                    var player = PlayerController.GetPlayer(1);
                    player.characterAnimation.SetDirection(player.transform.position.y < counter.bounds.center.y ? Direction.Up : Direction.Down);
                    player.characterAnimation.SetCleaning(true, counterPlaceable.placeableSurface);
                    counterWorkAnchor.position = CounterBarPosition();
                    counterWork.StartWork(CounterSeconds);
                    Log("counter_clean_start", new { seconds = CounterSeconds });
                }
                counterProgress += Time.deltaTime;
                if (!counterWork.AddWorkDone(1, Time.deltaTime)) return;
                state.CounterCleaned = true; dirty = true;
                Log("counter_clean_complete", new { seconds = CounterSeconds, cost = 0, materials = 0 });
                StopCounter();
            }
            catch (Exception ex) { StopCounter(); counterFailed = true; Log("counter_error", ex.ToString()); Logger.LogError(ex); }
        }
        void StopCounter()
        {
            if (!counterWorking) return;
            counterWorking = false; counterProgress = 0;
            try
            {
                if (counterWork != null) counterWork.Stop();
                var player = PlayerController.GetPlayer(1);
                if (player != null && counterPlaceable != null) player.characterAnimation.SetCleaning(false, counterPlaceable.placeableSurface);
            }
            finally
            {
                if (PlayerInputs.GetPlayer(1) != null) PlayerInputs.EnableKeyboardInputsAndMovement(1, this);
            }
        }
        void ApplyCounter()
        {
            if (NativeComplete()) return;
            if (counter != null && counterOriginal != null) counter.sprite = CleanupVisualActive() && state.CounterCleaned ? counterSprite : counterOriginal;
            bool show = store != null && CounterReady() && NearCounter();
            if (show)
            {
                ButtonsContext.GetPlayer(1)?.SoftResetButtons();
                ButtonsContext.AddButton(1, ActionType.Interact, RepairText.Get("action.clean-counter"));
                SetCleanupGuidePosition(CounterGuidePosition());
                counterGuide = true;
            }
            else if (counterGuide) { if (!NativeCleanupPriority()) ButtonsContext.GetPlayer(1)?.ResetButtons(); counterGuide = false; }
        }
        void RestoreCounter()
        { StopCounter(); counterWasHeld = false; if (counterGuide) { ButtonsContext.GetPlayer(1)?.ResetButtons(); counterGuide = false; } if (counterWork != null) { counterWork.DestroyBar(); Destroy(counterWork.gameObject); } counterWork = null; counterWorkAnchor = null; if (counter != null && counterOriginal != null) counter.sprite = counterOriginal; counter = null; counterOriginal = null; counterPlaceable = null; }
        void DrawCounter()
        {
            // Native ButtonsContext and DoWork render the prompt and progress bar.
        }
    }
}

