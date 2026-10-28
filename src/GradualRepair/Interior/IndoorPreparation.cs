using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        sealed class IndoorPatch { public string Group, Id; public SpriteRenderer Source, Copy; public bool Enabled, Selected, Visible = true; public Sprite Original, Owned; public Bounds Bounds; public int Cells, Kept; public Color Color; public Material Material; public float ApproachY; public int JobIndex; public Rect[] FloorHoles; public Vector3 HoleCenter; }
        bool indoorGeometryDirty = true;
        static void IndoorDecorationChanged() { if (self != null) self.indoorGeometryDirty = true; }
        readonly List<IndoorPatch> indoorPatches = new List<IndoorPatch>();
        readonly Dictionary<string, FloorDirt> initialDirt = new Dictionary<string, FloorDirt>();
        GameObject indoorRoot; float nextIndoorScan;
        static string InitialDirtId(FloorDirt dirt)
        {
            if (dirt == null || !dirt.tutorialDirt || dirt.transform.parent == null || dirt.transform.parent.name != "Manchas") return null;
            string name = dirt.name; return name == "FloorDirt" ? "floor-dirt-0" : name == "FloorDirt (1)" ? "floor-dirt-1" : name == "FloorDirt (2)" ? "floor-dirt-2" : name == "FloorDirt (3)" ? "floor-dirt-3" : null;
        }
        static void BeforeInitialDirtClean(FloorDirt __instance, out string __state) { __state = InitialDirtId(__instance); }
        static void AfterInitialDirtClean(bool __result, string __state) { if (__result) self?.RecordInitialDirt(__state); }
        static void BeforeInitialDirtRemoved(FloorDirt __instance) { self?.RecordInitialDirt(InitialDirtId(__instance)); }
        void RestoreInitialDirt(string id, FloorDirt dirt)
        {
            // CleanDirtImmediately only completes DoWork; it does not remove the
            // FloorDirt. Repeating it while mopping stops/restarts the gauge.
            if (dirt != null && state.CleanedInitialDirt.Contains(id)) dirt.DestroyFloorDirt();
        }
        void RecordInitialDirt(string id) { if (id == null || !CleanupActive() || !state.InteriorProject || state.CleanedInitialDirt.Contains(id)) return; state.CleanedInitialDirt.Add(id); dirty = true; Log("initial_dirt_cleaned", new { id }); }
        bool IndoorAvailable() => state.InteriorProject && indoorPatches.Count > 0;
        float IndoorRemaining(RepairJob job)
        {
            if (!IsInterior(job)) return 1;
            var parts = indoorPatches.Where(p => p.Group == job.Id).ToArray();
            int total = parts.Sum(p => p.Cells); return total == 0 ? 1 : (float)parts.Sum(p => p.Kept) / total;
        }

        void UpdateIndoor()
        {
            if (!bound || !projectInitialized || !IndoorCleanupProject() || !state.InteriorProject || !NativeGame.WorldLoaded(NativeGame.Game)) return;
            if (Time.unscaledTime < nextIndoorScan) return; nextIndoorScan = Time.unscaledTime + 1;
            var tutorial = SceneReferences.GetSceneReferences()?.tutorialParent?.transform; if (tutorial == null) return;
            if (indoorPatches.Count == 0)
            {
                foreach (var source in tutorial.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (source.sprite == null) continue;
                    string relative = source.transform.name; for (var parent = source.transform.parent; parent != null && parent != tutorial; parent = parent.parent) relative = parent.name + "/" + relative;
                    var job = RepairModel.InteriorJobs.FirstOrDefault(j => RepairModel.MatchesIndoorPath(j, relative)); if (job == null) continue;
                    var part = new IndoorPatch { Group = job.Id, Id = relative, Source = source, Enabled = source.enabled, Selected = true, Original = source.sprite, Bounds = source.bounds, Color = source.color, Material = source.sharedMaterial, JobIndex = Array.IndexOf(RepairModel.InteriorJobs, job) + 14 };
                    part.Owned = Instantiate(source.sprite); part.Owned.name = "GradualRepair " + relative; source.sprite = part.Owned; BindFloorHoles(part); indoorPatches.Add(part);
                }
                foreach (var part in indoorPatches) { string branch = part.Id.Split('/')[0]; part.ApproachY = indoorPatches.Where(p => p.Id.StartsWith(branch + "/")).Min(p => p.Bounds.min.y) - .35f; }
                indoorGeometryDirty = true;
                Log("indoor_bound", new { total = indoorPatches.Count, selected = indoorPatches.Count(p => p.Selected) });
            }
            if (!NativeComplete())
            {
                var manchas = tutorial.Find("Manchas"); if (manchas != null)
                {
                    initialDirt.Clear(); foreach (var dirt in manchas.GetComponentsInChildren<FloorDirt>(true)) { var id = InitialDirtId(dirt); if (id != null) initialDirt[id] = dirt; }
                    foreach (string id in RepairModel.InitialDirtIds)
                    {
                        if (!initialDirt.TryGetValue(id, out var dirt) || dirt == null) continue;
                        RestoreInitialDirt(id, dirt);
                    }
                }
            }
            if (indoorGeometryDirty) { RefreshIndoorDoorOpenings(); foreach (var part in indoorPatches) RebuildIndoorGeometry(part); indoorGeometryDirty = false; repairPreview.Invalidate(); }
        }

        void ApplyIndoor()
        {
            bool active = IndoorCleanupProject() && state.InteriorProject;
            if (!active) { if (indoorRoot != null) indoorRoot.SetActive(false); return; }
            if (NativeComplete() && indoorRoot == null && indoorPatches.Count > 0)
            {
                indoorRoot = new GameObject("GradualRepair Indoor Render Only");
                foreach (var patch in indoorPatches.Where(p => p.Selected && p.Source != null))
                {
                    var node = new GameObject(patch.Id); node.layer = patch.Source.gameObject.layer; node.transform.SetParent(indoorRoot.transform, false); node.transform.position = patch.Source.transform.position; node.transform.rotation = patch.Source.transform.rotation; node.transform.localScale = patch.Source.transform.lossyScale;
                    var r = node.AddComponent<SpriteRenderer>(); r.sprite = patch.Owned; r.flipX = patch.Source.flipX; r.flipY = patch.Source.flipY; r.sharedMaterial = patch.Material; r.color = patch.Source.color; r.sortingLayerID = patch.Source.sortingLayerID; r.sortingOrder = patch.Source.sortingOrder; r.spriteSortPoint = patch.Source.spriteSortPoint; r.maskInteraction = patch.Source.maskInteraction; patch.Copy = r;
                }
            }
            if (indoorRoot != null) indoorRoot.SetActive(NativeComplete());
            foreach (var patch in indoorPatches)
            {
                bool repaired = state.CompletedInterior.Contains(patch.Group);
                bool highlight = onsiteRepair && open && !repaired && JobAt(selected).Id == patch.Group;
                var outline = highlight ? DecorationMode.GetPlayer(1)?.GetOutlineMaterial() : null;
                var material = outline != null ? outline : patch.Material;
                if (patch.Source != null) { patch.Source.color = patch.Color; patch.Source.sharedMaterial = material; }
                if (patch.Copy != null) { patch.Copy.color = patch.Color; patch.Copy.sharedMaterial = material; }
                if (!NativeComplete() && patch.Source != null) patch.Source.enabled = patch.Enabled && !repaired && patch.Visible;
                if (patch.Copy != null) patch.Copy.enabled = patch.Selected && !repaired && patch.Visible;
            }
        }
        void ReleaseIndoor()
        {
            foreach (var patch in indoorPatches) { if (patch.Source != null) { patch.Source.enabled = patch.Enabled; patch.Source.sprite = patch.Original; patch.Source.color = patch.Color; patch.Source.sharedMaterial = patch.Material; } if (patch.Owned != null) Destroy(patch.Owned); }
            ClearIndoorGuide(); indoorPatches.Clear(); initialDirt.Clear(); indoorDoorOpenings.Clear(); if (indoorRoot != null) { indoorRoot.SetActive(false); Destroy(indoorRoot); }
            indoorRoot = null; nextIndoorScan = Time.unscaledTime + 2;
        }
        void SetInteriorPreview(bool show)
        {
            if (!repairPreview.IsCreated) return; indoorPreview = show;
            Bounds? frame = null;
            if (show)
            {
                var parts = indoorPatches.Where(p => p.Source != null).ToArray(); if (parts.Length == 0) return;
                Bounds bounds = parts[0].Bounds; foreach (var part in parts) bounds.Encapsulate(part.Bounds);
                frame = bounds;
            }
            repairPreview.FrameInterior(frame); UpdateFieldHighlight();
        }
    }
}
