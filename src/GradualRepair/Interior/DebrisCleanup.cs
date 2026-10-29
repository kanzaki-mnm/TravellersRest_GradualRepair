using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        sealed class DebrisPart
        {
            public string Id;
            public Placeable Placeable;
            public Transform Root;
            public Bounds Bounds;
            public bool Large, Released, OriginalPlaced, OriginalEnabled, Persistent, PersistentOccupied;
            public readonly List<DebrisMove> Moves = new List<DebrisMove>();
            public SpriteRenderer[] Sprites;
            public bool[] SpriteEnabled;
            public bool[] SpriteAfterPreparation;
            public Bounds PreparedBounds;
            public Collider2D[] Colliders;
            public bool[] ColliderEnabled;
            public InputByProximity[] Inputs;
            public bool[] InputEnabled;
        }
        readonly List<DebrisPart> debrisParts = new List<DebrisPart>();
        DebrisPart debrisTarget;
        DoWork debrisWork;
        Transform debrisAnchor;
        bool debrisWorking, debrisGuide, debrisWasHeld, debrisFailed;
        float debrisNextScan, debrisFloorSeconds;
        bool debrisRootLogged;
        bool DebrisReady() => DebrisCleanupActive() && !failed && !debrisFailed && !open && !counterWorking && CanOpen();
        Vector3 DebrisGuidePosition() => debrisTarget.Bounds.center + Vector3.up * .35f;
        bool NativeCleanupPriority()
        {
            var go = InteractObject.GetPlayer(1)?.GetCurrentInteractGO();
            var focused = InputByProximityManager.GetPlayer(1)?.GetCurrentFocusedInputElement()?.mainGameObject;
            var hovered = NativeGame.Hovered(HoverObject.GetPlayer(1)) as Component;
            return NativePriorityObject(go) || NativePriorityObject(focused) || NativePriorityObject(hovered == null ? null : hovered.gameObject);
        }
        static bool NativePriorityObject(GameObject go) => go != null && (go.GetComponentInParent<BeerTap>() != null || go.GetComponentInParent<DialogueNPCBase>() != null || go.GetComponentInParent<HumanNPC>() != null || go.GetComponentInParent<FloorDirt>() != null || go.GetComponentInParent<Table>() != null);
        void BindDebris()
        {
            if (debrisParts.Count > 0 && debrisParts[0].Root != null) return;
            if (debrisParts.Count > 0) RestoreDebris();
            if (Bar.instance == null) return;
            var tutorialParent = SceneReferences.GetSceneReferences()?.tutorialParent;
            Transform root = tutorialParent == null ? null : tutorialParent.transform.Find("Decoracion");
            if (root == null && tutorialParent != null && tutorialParent.name == "Decoracion") root = tutorialParent.transform;
            for (var parent = Bar.instance.transform.parent; parent != null && root == null; parent = parent.parent) root = parent.Find("Tutorial/Decoracion");
            if (root == null && tutorialParent != null)
            {
                // Some current versions call this parent Decoration_Props_Tutorial.
                // Resolve only the explicit named groups, within the native tutorial parent.
                foreach (var child in tutorialParent.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Grupo Ladrillos") { root = child.parent; break; }
            }
            if (!debrisRootLogged)
            {
                debrisRootLogged = true;
                var names = new List<string>();
                if (tutorialParent != null) foreach (var child in tutorialParent.GetComponentsInChildren<Transform>(true)) if (child.GetComponent<SpriteRenderer>() != null) names.Add(child.name);
                Log("debris_root", new { nativeParent = tutorialParent == null ? null : tutorialParent.name, root = root == null ? null : root.name, renderObjects = names });
            }
            if (root == null && tutorialParent != null) root = tutorialParent.transform;
            if (root == null) return;
            var prefabField = NativeReferences.Field(typeof(DoWork), "progressorPrefab");
            GameObject prefab = null; float work = 0, speed = 0;
            foreach (var dirt in Resources.FindObjectsOfTypeAll<FloorDirt>())
            {
                var source = dirt.GetComponent<DoWork>();
                if (source == null) continue;
                var candidate = prefabField.GetValue(source) as GameObject;
                float amount = (float)NativeReferences.Field(typeof(FloorDirt), "workNeeded").GetValue(dirt);
                if (candidate == null || amount <= 0) continue;
                prefab = candidate; work = amount; if (!dirt.tutorialDirt) break;
            }
            foreach (var mop in Resources.FindObjectsOfTypeAll<Mop>()) if (mop.speedCleaningDirt > 0) { speed = mop.speedCleaningDirt; break; }
            if (prefab == null || work <= 0 || speed <= 0) throw new InvalidOperationException("Native floor cleaning baseline unavailable");
            debrisFloorSeconds = work / speed;
            string[] paths = { "Grupo Ladrillos", "Grupo Ladrillos (1)", "Grupo Ladrillos (2)", "Mesa Tirada", "Cajas Apiladas", "Lateral Habitacion" };

            for (int i = 0; i < paths.Length; i++)
            {
                var target = root.Find(paths[i]);
                if (target == null) foreach (var descendant in root.GetComponentsInChildren<Transform>(true)) if (descendant.name.Replace("(Clone)", "").Trim() == paths[i]) { target = descendant; break; }
                if (target == null) { Log("debris_missing", paths[i]); continue; }
                var placeable = target.GetComponent<Placeable>(); if (placeable == null || placeable.itemSpace == null) { Log("debris_missing", paths[i] + ": ItemSpace missing"); continue; }
                var sprites = new List<SpriteRenderer>();
                foreach (var sprite in target.GetComponentsInChildren<SpriteRenderer>(true)) if (sprite.sprite != null && sprite.sprite.name != "BuildSquare") sprites.Add(sprite);
                if (sprites.Count == 0) continue;
                var part = new DebrisPart { Id = i == 5 ? "bedroom-clutter" : DebrisRules.Ids[i], Root = target, Placeable = placeable, OriginalPlaced = placeable.hasBeenPlaced, OriginalEnabled = placeable.enabled, Large = i >= 3, Sprites = sprites.ToArray(), Colliders = target.GetComponentsInChildren<Collider2D>(true), Inputs = target.GetComponentsInChildren<InputByProximity>(true) };
                part.SpriteEnabled = new bool[part.Sprites.Length]; part.Bounds = part.Sprites[0].bounds;
                for (int j = 0; j < part.Sprites.Length; j++) { part.SpriteEnabled[j] = part.Sprites[j].enabled; part.Bounds.Encapsulate(part.Sprites[j].bounds); }
                part.ColliderEnabled = new bool[part.Colliders.Length]; for (int j = 0; j < part.Colliders.Length; j++) part.ColliderEnabled[j] = part.Colliders[j].enabled;
                part.InputEnabled = new bool[part.Inputs.Length]; for (int j = 0; j < part.Inputs.Length; j++) part.InputEnabled[j] = part.Inputs[j].enabled;
                ConfigureBedroomDebris(part);
                debrisParts.Add(part);
            }
            var barBricks = Bar.instance.transform.Find("Tutorial/Ladrillos");
            if (barBricks != null)
            {
                var sprite = barBricks.GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    var colliders = barBricks.GetComponentsInChildren<Collider2D>(true);
                    var flags = new bool[colliders.Length]; for (int i = 0; i < flags.Length; i++) flags[i] = colliders[i].enabled;
                    var cornerSprites = new List<SpriteRenderer> { sprite };
                    // Identify the authored component, not its current sprite name.
                    // Gentle Rest replaces the web sprite with moss on this same renderer.
                    var cornerWeb = Bar.instance.transform.Find("Tutorial/Spiderweb")?.GetComponent<SpriteRenderer>();
                    if (cornerWeb != null) cornerSprites.Add(cornerWeb);
                    var cornerEnabled = new bool[cornerSprites.Count]; for (int i = 0; i < cornerEnabled.Length; i++) cornerEnabled[i] = cornerSprites[i].enabled;
                    debrisParts.Add(new DebrisPart { Id = "bar-bricks", Root = barBricks, Bounds = sprite.bounds, Sprites = cornerSprites.ToArray(), SpriteEnabled = cornerEnabled, Colliders = colliders, ColliderEnabled = flags, Inputs = new InputByProximity[0], InputEnabled = new bool[0] });
                    Log("bar_corner_group", new { id = "bar-bricks", renderers = cornerSprites.Count, webFound = cornerWeb != null, displayedSprite = cornerWeb == null || cornerWeb.sprite == null ? null : cornerWeb.sprite.name, interactionBounds = "bricks only", savedCleanup = state.ClearedDebris.Contains("bar-bricks") });
                }
            }
            if (debrisParts.Count == 0) throw new InvalidOperationException("No supported debris targets resolved");
            var owner = new GameObject("GradualRepair Debris Work"); owner.transform.SetParent(transform, false); debrisAnchor = owner.transform;
            debrisWork = owner.AddComponent<DoWork>(); prefabField.SetValue(debrisWork, prefab); NativeReferences.Field(typeof(DoWork), "workBarPosition").SetValue(debrisWork, debrisAnchor); debrisWork.inWorkArea = true;
            var positions = new List<object>(); foreach (var part in debrisParts) positions.Add(new { part.Id, position = part.Bounds.center.ToString("F3"), active = part.Root.gameObject.activeInHierarchy });
            Log("debris_bound", new { count = debrisParts.Count, smallSeconds = debrisFloorSeconds, largeSeconds = DebrisRules.Duration(debrisFloorSeconds, true), nativeWork = work, nativeSpeed = speed, positions });
        }
        DebrisPart ClosestDebris()
        {
            var player = PlayerController.GetPlayer(1); if (player == null) return null;
            // Feet and the player's upper body must be close to the same object.
            Vector2 feet = player.transform.position, reach = feet + Vector2.up * .45f;
            float best = .42f; DebrisPart found = null;
            foreach (var part in debrisParts)
            {
                if (part.Root == null || !part.Root.gameObject.activeInHierarchy || state.ClearedDebris.Contains(part.Id)) continue;
                var point = part.Bounds.ClosestPoint(new Vector3(reach.x, reach.y, part.Bounds.center.z));
                float distance = Vector2.Distance(reach, point);
                if (distance <= best) { best = distance; found = part; }
            }
            return found;
        }
        void UpdateDebris()
        {
            try
            {
                if (nativePreparationPending || !bound || !DebrisCleanupActive()) { StopDebris(); return; }
                if (Time.unscaledTime >= debrisNextScan) { debrisNextScan = Time.unscaledTime + 2; if (!debrisFailed) BindDebris(); }
                if (!DebrisReady() || debrisWork == null) { StopDebris(); return; }
                var next = debrisWorking ? debrisTarget : ClosestDebris();
                bool held = Input.GetKey(KeyCode.E) || PlayerInputs.GetPlayer(1).GetButton(ActionType.Interact);
                bool begin = CounterCleaningRules.CanBegin(debrisGuide && DisplayedCleanupGuide(2), held, debrisWasHeld); debrisWasHeld = held;
                if (next != debrisTarget) { if (debrisGuide) { ButtonsContext.GetPlayer(1)?.ResetButtons(); debrisGuide = false; } debrisTarget = next; begin = false; }
                if (debrisTarget == null || (!debrisWorking && NativeCleanupPriority()) || !held || (!debrisWorking && !begin)) { StopDebris(); return; }
                if (!debrisWorking)
                {
                    if (!TryOwnModInteraction()) return;
                    debrisWorking = true;
                    PlayerInputs.DisableKeyboardInputsAndMovement(1, debrisWork);
                    var player = PlayerController.GetPlayer(1); Vector2 delta = debrisTarget.Bounds.center - player.transform.position;
                    player.characterAnimation.SetDirection(Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x < 0 ? Direction.Left : Direction.Right) : (delta.y < 0 ? Direction.Down : Direction.Up));
                    player.characterAnimation.SetCleaning(true, (debrisTarget.Placeable ?? Bar.instance.placeable).placeableSurface);
                    debrisAnchor.position = debrisTarget.Bounds.center;
                    debrisWork.StartWork(DebrisRules.Duration(debrisFloorSeconds, debrisTarget.Large));
                    Log("debris_clean_start", new { id = debrisTarget.Id, seconds = DebrisRules.Duration(debrisFloorSeconds, debrisTarget.Large) });
                }
                if (!debrisWork.AddWorkDone(1, Time.deltaTime)) return;
                state.ClearedDebris.Add(debrisTarget.Id); dirty = true; QueueDebrisSalvage(debrisTarget.Id);
                Log("debris_clean_complete", new { id = debrisTarget.Id, cost = 0, materials = 0, state.ClearedDebris }); StopDebris();
            }
            catch (Exception ex) { StopDebris(); debrisFailed = true; Log("debris_error", ex.ToString()); Logger.LogError(ex); }
        }
        void StopDebris()
        {
            if (!debrisWorking) return;
            debrisWorking = false;
            try
            {
                if (debrisWork != null) debrisWork.Stop();
                var player = PlayerController.GetPlayer(1);
                if (player != null && debrisTarget != null && Bar.instance != null) player.characterAnimation.SetCleaning(false, (debrisTarget.Placeable ?? Bar.instance.placeable).placeableSurface);
            }
            finally
            {
                if (PlayerInputs.GetPlayer(1) != null && debrisWork != null) PlayerInputs.EnableKeyboardInputsAndMovement(1, debrisWork);
            }
        }

        void ApplyDebris()
        {
            if (debrisFailed) return;
            try
            {
                bool active = nativePreparationPending || DebrisCleanupActive();
                bool after = DebrisAfterPreparation();
                foreach (var part in debrisParts)
                {
                    if (part.Root == null) continue;
                    bool clean = active && state.ClearedDebris.Contains(part.Id);
                    if (after)
                    {
                        if (active && !clean) RetainDebris(part);
                        if (part.Persistent)
                        {
                            if (part.SpriteAfterPreparation != null) part.Bounds = part.PreparedBounds;
                            SetPersistentDebrisOccupied(part, active && !clean);
                            for (int i = 0; i < part.Sprites.Length; i++) if (part.Sprites[i] != null) part.Sprites[i].enabled = active && !clean && part.SpriteEnabled[i] && (part.SpriteAfterPreparation == null || part.SpriteAfterPreparation[i]);
                            for (int i = 0; i < part.Colliders.Length; i++) if (part.Colliders[i] != null) part.Colliders[i].enabled = active && !clean && part.ColliderEnabled[i];
                            foreach (var input in part.Inputs) if (input != null) input.enabled = false;
                        }
                        continue;
                    }
                    if (clean && !part.Released) { if (part.Placeable != null && !part.Placeable.itemSpace.Place(false, false, false)) throw new InvalidOperationException("Cannot release debris occupancy"); part.Released = true; }
                    bool nativeComplete = MissionsManager.instance != null && MissionsManager.instance.IsMissionComplete(301);
                    if (!clean && part.Released) { if (!nativeComplete && part.Placeable != null) { part.Placeable.itemSpace.Place(true, false, false); part.Placeable.hasBeenPlaced = part.OriginalPlaced; } part.Released = false; }
                    for (int i = 0; i < part.Sprites.Length; i++) if (part.Sprites[i] != null) part.Sprites[i].enabled = !clean && part.SpriteEnabled[i];
                    for (int i = 0; i < part.Colliders.Length; i++) if (part.Colliders[i] != null) part.Colliders[i].enabled = !clean && part.ColliderEnabled[i];
                    for (int i = 0; i < part.Inputs.Length; i++) if (part.Inputs[i] != null) part.Inputs[i].enabled = !clean && part.InputEnabled[i];
                }
                bool show = DebrisReady() && debrisTarget != null && !state.ClearedDebris.Contains(debrisTarget.Id) && (debrisWorking || !NativeCleanupPriority());
                if (show) { ButtonsContext.GetPlayer(1)?.SoftResetButtons(); ButtonsContext.AddButton(1, ActionType.Interact, RepairText.Get("action.clear-debris")); SetCleanupGuidePosition(DebrisGuidePosition()); debrisGuide = true; }
                else if (debrisGuide) { if (!NativeCleanupPriority()) ButtonsContext.GetPlayer(1)?.ResetButtons(); debrisGuide = false; }
            }
            catch (Exception ex) { StopDebris(); debrisFailed = true; Log("debris_apply_error", ex.ToString()); }
        }
        void RestoreDebris()
        {
            StopDebris(); if (debrisGuide) ButtonsContext.GetPlayer(1)?.ResetButtons(); debrisGuide = false;
            foreach (var part in debrisParts)
            {
                if (part.Root == null) continue;
                RestoreRetainedDebris(part);
                if (part.Released && part.Placeable != null && MissionsManager.instance != null && !MissionsManager.instance.IsMissionComplete(301))
                {
                    try { part.Placeable.itemSpace.Place(true, false, false); part.Placeable.hasBeenPlaced = part.OriginalPlaced; }
                    catch (Exception ex) { Log("debris_restore_error", ex.ToString()); }
                }
                for (int i = 0; i < part.Sprites.Length; i++) if (part.Sprites[i] != null) part.Sprites[i].enabled = part.SpriteEnabled[i];
                for (int i = 0; i < part.Colliders.Length; i++) if (part.Colliders[i] != null) part.Colliders[i].enabled = part.ColliderEnabled[i];
                for (int i = 0; i < part.Inputs.Length; i++) if (part.Inputs[i] != null) part.Inputs[i].enabled = part.InputEnabled[i];
            }
            debrisParts.Clear(); if (debrisWork != null) { debrisWork.DestroyBar(); Destroy(debrisWork.gameObject); }
            debrisWork = null; debrisAnchor = null; debrisTarget = null; debrisWasHeld = false; debrisFailed = false; debrisRootLogged = false; debrisNativeSwitched = false; if (retainedDebrisRoot != null) Destroy(retainedDebrisRoot); retainedDebrisRoot = null;
        }
    }
}

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        void ConfigureBedroomDebris(DebrisPart part)
        {
            if (part.Id != "bedroom-clutter") return;
            var shutter = part.Root.Find("Ventana");
            if (shutter == null || part.Root.Find("Madera") == null || part.Root.Find("Taburete") == null)
                throw new InvalidOperationException("Bedroom debris hierarchy changed; shutter, wood or stool missing");
            part.SpriteAfterPreparation = new bool[part.Sprites.Length];
            bool boundsSet = false; int hidden = 0;
            for (int i = 0; i < part.Sprites.Length; i++)
            {
                var sprite = part.Sprites[i];
                bool keep = sprite.transform != shutter && !sprite.transform.IsChildOf(shutter);
                part.SpriteAfterPreparation[i] = keep;
                if (!keep) { hidden++; continue; }
                if (!boundsSet) { part.PreparedBounds = sprite.bounds; boundsSet = true; } else part.PreparedBounds.Encapsulate(sprite.bounds);
            }
            if (!boundsSet || hidden == 0) throw new InvalidOperationException("Bedroom debris render split missing");
            Log("bedroom_debris_split", new { shutterRenderers = hidden, remainingRenderers = part.Sprites.Length - hidden, cleanupId = part.Id });
        }
    }
}

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        sealed class DebrisMove { public Transform Node, Parent; public int Sibling; public bool Active; }
        GameObject retainedDebrisRoot;
        bool debrisNativeSwitched;
        bool DebrisAfterPreparation() => debrisNativeSwitched || NativeComplete();
        bool DebrisCleanupActive() => IndoorCleanupProject();
        void PrepareDebrisNativeSwitch()
        {
            StopDebris();
            // Vanilla releases all tutorial occupancy in the same callback.
            // Restore only the grid claim; keep previously cleaned graphics hidden.
            foreach (var part in debrisParts) if (part.Released && part.Placeable != null)
            { part.Placeable.itemSpace.Place(true, false, false); part.Placeable.hasBeenPlaced = part.OriginalPlaced; part.Released = false; }
        }
        static void AfterTutorialObjectsSwitch(bool __0)
        {
            if (__0 || self == null || !self.bound || !self.projectInitialized || !self.state.ExteriorProject || !self.state.InteriorProject) return;
            self.debrisNativeSwitched = true;
            try { self.BindDebris(); self.ApplyDebris(); } catch (Exception e) { self.debrisFailed = true; self.Logger.LogError(e); }
        }
        void RetainDebris(DebrisPart part)
        {
            if (part.Persistent) return;
            if (retainedDebrisRoot == null) retainedDebrisRoot = new GameObject("GradualRepair Retained Debris");
            // Disable native pickup/save handling before activating the original visual.
            // We do not clone Placeable, ItemSetup or any saveable native item.
            if (part.Placeable != null) part.Placeable.enabled = false;
            foreach (var input in part.Inputs) if (input != null) input.enabled = false;
            MoveDebrisNode(part, part.Root);
            foreach (var sprite in part.Sprites) if (sprite != null && !sprite.transform.IsChildOf(part.Root) && sprite.transform != part.Root) MoveDebrisNode(part, sprite.transform);
            part.Persistent = true;
            Log("debris_retained", new { part.Id, nodes = part.Moves.Count });
        }
        void MoveDebrisNode(DebrisPart part, Transform node)
        {
            if (node == null) return;
            foreach (var moved in part.Moves) if (moved.Node == node || node.IsChildOf(moved.Node)) return;
            part.Moves.Add(new DebrisMove { Node = node, Parent = node.parent, Sibling = node.GetSiblingIndex(), Active = node.gameObject.activeSelf });
            node.SetParent(retainedDebrisRoot.transform, true); node.gameObject.SetActive(true);
        }
        void SetPersistentDebrisOccupied(DebrisPart part, bool occupied)
        {
            if (part.PersistentOccupied == occupied) return;
            if (part.Placeable != null && !part.Placeable.itemSpace.Place(occupied, false, false)) throw new InvalidOperationException("Cannot update retained debris occupancy: " + part.Id);
            part.PersistentOccupied = occupied;
        }
        void RestoreRetainedDebris(DebrisPart part)
        {
            if (!part.Persistent) return;
            SetPersistentDebrisOccupied(part, false);
            foreach (var move in part.Moves) if (move.Node != null) { move.Node.SetParent(move.Parent, true); if (move.Parent != null) move.Node.SetSiblingIndex(Math.Min(move.Sibling, move.Parent.childCount - 1)); move.Node.gameObject.SetActive(move.Active); }
            part.Moves.Clear(); part.Persistent = false;
            if (part.Placeable != null) part.Placeable.enabled = part.OriginalEnabled;
        }
        static void BeforeDebrisSave(out List<DebrisPart> __state)
        {
            __state = new List<DebrisPart>(); if (self == null) return;
            foreach (var part in self.debrisParts) if (part.PersistentOccupied) { __state.Add(part); self.SetPersistentDebrisOccupied(part, false); }
        }
        static Exception EndDebrisSave(Exception __exception, List<DebrisPart> __state)
        {
            if (self == null || __state == null) return __exception;
            foreach (var part in __state) try { if (part.Persistent && self.DebrisCleanupActive() && !self.state.ClearedDebris.Contains(part.Id)) self.SetPersistentDebrisOccupied(part, true); } catch (Exception e) { self.debrisFailed = true; self.Logger.LogError(e); }
            return __exception;
        }
    }
}
