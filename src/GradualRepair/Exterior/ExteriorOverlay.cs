using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        GameObject exteriorOverlay;
        readonly Dictionary<SpriteRenderer, bool> hiddenFacade = new Dictionary<SpriteRenderer, bool>();
        Transform PrepareExteriorSource(Transform source)
        {
            bool overlayNeeded = Active() && !source.gameObject.activeInHierarchy;
            if (!overlayNeeded) { ReleaseExteriorOverlay(); return source; }
            if (exteriorOverlay != null) return exteriorOverlay.transform;
            exteriorOverlay = new GameObject("GradualRepair Exterior Render Only");
            exteriorOverlay.transform.SetParent(source.parent, false); exteriorOverlay.transform.localPosition = source.localPosition; exteriorOverlay.transform.localRotation = source.localRotation; exteriorOverlay.transform.localScale = source.localScale;
            CopyRenderTree(source.Find("Old"), exteriorOverlay.transform);
            // Only the visual facade is masked. Doors, grids, scripts and all native
            // scene activation remain under the game's control.
            foreach (string season in new[] { "Spring", "Winter" })
            {
                var branch = source.parent.Find(season); if (branch == null) continue;
                foreach (string name in new[] { "TavernBuildingSide", "TavernBuildingSide B", "MultiplayerDoor", "Front", "Chimenea", "TavernBuildingSign", "Flag Animation" })
                {
                    var renderer = branch.Find(name)?.GetComponent<SpriteRenderer>(); if (renderer != null) hiddenFacade[renderer] = renderer.enabled;
                }
            }
            var lights = source.parent.Find("NightLights"); if (lights != null) foreach (var r in lights.GetComponentsInChildren<SpriteRenderer>(true)) hiddenFacade[r] = r.enabled;
            Log("exterior_overlay_created", new { hidden = hiddenFacade.Count, renderers = exteriorOverlay.GetComponentsInChildren<SpriteRenderer>(true).Length });
            return exteriorOverlay.transform;
        }
        void CopyRenderTree(Transform source, Transform parent)
        {
            var node = new GameObject(source.name); node.layer = source.gameObject.layer; node.transform.SetParent(parent, false); node.transform.localPosition = source.localPosition; node.transform.localRotation = source.localRotation; node.transform.localScale = source.localScale;
            var group = source.GetComponent<SortingGroup>(); if (group != null) { var copy = node.AddComponent<SortingGroup>(); copy.sortingLayerID = group.sortingLayerID; copy.sortingOrder = group.sortingOrder; }
            var r = source.GetComponent<SpriteRenderer>(); if (r != null) { var copy = node.AddComponent<SpriteRenderer>(); copy.sprite = r.sprite; copy.sharedMaterial = r.sharedMaterial; copy.color = r.color; copy.flipX = r.flipX; copy.flipY = r.flipY; copy.sortingLayerID = r.sortingLayerID; copy.sortingOrder = r.sortingOrder; copy.spriteSortPoint = r.spriteSortPoint; }
            foreach (Transform child in source) CopyRenderTree(child, node.transform);
        }
        void MaskCompletedFacade() { if (exteriorOverlay != null) foreach (var pair in hiddenFacade) if (pair.Key != null) pair.Key.enabled = false; }
        void ReleaseExteriorOverlay() { foreach (var pair in hiddenFacade) if (pair.Key != null) pair.Key.enabled = pair.Value; hiddenFacade.Clear(); if (exteriorOverlay != null) { exteriorOverlay.SetActive(false); Destroy(exteriorOverlay); } exteriorOverlay = null; }
    }
}
