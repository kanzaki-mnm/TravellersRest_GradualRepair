using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        readonly List<Rect> repaintedWallAreas = new List<Rect>();
        bool wallAreasDirty = true;
        float nextInitialMaterialAttempt;
        bool initialWallColumnsReady;
        static void FloorDecorationChanged() { if (self != null) { self.agedStyleRefresh = true; self.indoorGeometryDirty = true; } }
        static void BeforeWallDecoration(Vector3 __0, int __1, out int __state) { __state = NativeGame.GetWallDecoration(__0); self?.TrackBuildPaint(__0, __1, false); }
        static void AfterWallDecoration(Vector3 __0, int __state)
        {
            if (self == null) return; self.indoorGeometryDirty = self.agedStyleRefresh = self.wallAreasDirty = true;
            if (!self.bound || !self.projectInitialized || !self.state.InteriorProject || __state == 0) return;
            string key = "wall-column@" + __0.x.ToString("R", CultureInfo.InvariantCulture) + "," + __0.y.ToString("R", CultureInfo.InvariantCulture);
            if (!self.state.IndoorDecorBaseline.ContainsKey(key)) { self.state.IndoorDecorBaseline[key] = __state; self.dirty = true; }
        }
        void RefreshWallAreas()
        {
            if (!wallAreasDirty) return; wallAreasDirty = false; repaintedWallAreas.Clear();
            foreach (var entry in state.IndoorDecorBaseline)
            {
                if (!entry.Key.StartsWith("wall-column@") || entry.Value == 0) continue; var xy = entry.Key.Substring(12).Split(','); if (xy.Length != 2) continue;
                if (!float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) continue;
                if (NativeGame.GetWallDecoration(new Vector3(x, y, 0)) != entry.Value) repaintedWallAreas.Add(new Rect(x - .25f, y - .5f, .5f, 3f));
            }
        }
        bool WallRepainted(Vector3 point) { foreach (var area in repaintedWallAreas) if (area.Contains(point)) return true; return false; }
        bool InitialMaterialsProject() => bound && projectInitialized && state.ExteriorProject && state.InteriorProject && Solo() && PlayerInventory.GetPlayer(1) != null;

        // Only reads the authored tutorial maps once. No render-only maps are created.
        void InitializeAgedMaterials()
        {
            if (state.AgedMaterialsInitialized && state.AgedDecorations.Count == 0 && InitialMaterialsProject()) { state.AgedMaterialsInitialized = false; dirty = true; }
            if (!InitialMaterialsProject() || !NativeComplete() || (state.AgedMaterialsInitialized && initialWallColumnsReady) ||
               NativeGame.Game == null || !NativeGame.WorldLoaded(NativeGame.Game)) return;
            if (Time.unscaledTime < nextInitialMaterialAttempt) return; nextInitialMaterialAttempt = Time.unscaledTime + 2;
            var tutorial = SceneReferences.GetSceneReferences()?.tutorialParent?.transform;
            var floor = tutorial?.Find("FloorLower Tutorial")?.GetComponent<Tilemap>();
            var back = tutorial?.Find("WallBack Tutorial")?.GetComponent<Tilemap>();
            var front = tutorial?.Find("WallFront-No Colliders Tutorial")?.GetComponent<Tilemap>();
            if (floor == null || back == null || front == null || floor.GetUsedTilesCount() == 0 || back.GetUsedTilesCount() == 0) return;
            try
            {
                EnsureAgedBuildingAssets(); RefreshWallAreas();
                var defaults = new List<string>(); var wallColumns = new List<string>();
                // Hidden tutorial maps cannot be trusted to expose their active Grid
                // layout. Use an active, renderless copy with the authored layout.
                var sources = new[] { floor, back, front };
                var points = new Dictionary<Tilemap, List<InitialMaterialCell>>();
                foreach (var source in sources) points[source] = InitialMaterialPoints(source);
                foreach (var entry in points[floor])
                {
                    var point = entry.Point;
                    if (NativeGame.GetFloorDecoration(point, null) == 10)
                        defaults.Add(StyleKey('F', point));
                    // Native wall decoration is stored on the floor-level base,
                    // including where a door hides the lower wall rows.
                    if (NativeGame.GetWallDecoration(point) == 12 && NativeGame.HasWall(point + Vector3.up * .5f) && !WallRepainted(point))
                        wallColumns.Add(StyleKey('W', point));
                }
                foreach (var map in new[] { back, front }) foreach (var entry in points[map])
                {
                    var point = entry.Point;
                    var sprite = map.GetSprite(entry.Cell); if (sprite == null || !sprite.name.StartsWith("TS_Wall20_")) continue;
                    // Keep already repainted materials and explicit aged/normal choices.
                    if (NativeGame.GetWallDecoration(point) == 12 && !WallRepainted(point)) defaults.Add(StyleKey('W', point));
                    if (int.TryParse(sprite.name.Substring(10), out int variant) && variant >= 0 && variant < 4)
                    {
                        // Trim is an independent material, even after repainting the wall.
                        defaults.Add(StyleKey('T', point));
                    }
                }
                if (defaults.Count == 0) throw new InvalidOperationException("No initial material cells matched native decorations; initialization deferred");
                int added;
                if (!state.AgedMaterialsInitialized) { defaults.AddRange(wallColumns); added = AgedStyleRules.ImportDefaults(state, defaults); }
                else
                {
                    var next = new Dictionary<string, bool>(state.AgedDecorations); added = 0;
                    foreach (var key in wallColumns) if (!next.ContainsKey(key)) { next.Add(key, true); added++; }
                    state.AgedDecorations = next;
                }
                initialWallColumnsReady = true;
                dirty = true; agedStyleRefresh = indoorGeometryDirty = true;
                Log("aged_materials_imported", new { added, total = state.AgedDecorations.Count, rendering = "native decoration tiles; no material overlay" });
            }
            catch (Exception ex) { Log("aged_materials_import_error", ex.ToString()); }
        }
        sealed class InitialMaterialCell { public Vector3Int Cell; public Vector3 Point; }
        List<InitialMaterialCell> InitialMaterialPoints(Tilemap source)
        {
            var root = new GameObject("GradualRepair Material Coordinates", typeof(Grid));
            try
            {
                var original = source.GetComponentInParent<Grid>(true); var grid = root.GetComponent<Grid>();
                if (original != null) { grid.cellSize = original.cellSize; grid.cellGap = original.cellGap; grid.cellLayout = original.cellLayout; grid.cellSwizzle = original.cellSwizzle; root.transform.position = original.transform.position; root.transform.rotation = original.transform.rotation; root.transform.localScale = original.transform.lossyScale; }
                var node = new GameObject("Coordinates", typeof(Tilemap)); node.transform.SetParent(root.transform, false);
                node.transform.position = source.transform.position; node.transform.rotation = source.transform.rotation;
                var scale = root.transform.lossyScale; node.transform.localScale = new Vector3(source.transform.lossyScale.x / scale.x, source.transform.lossyScale.y / scale.y, source.transform.lossyScale.z / scale.z);
                var map = node.GetComponent<Tilemap>(); map.tileAnchor = source.tileAnchor; map.orientation = source.orientation; map.orientationMatrix = source.orientationMatrix;
                var result = new List<InitialMaterialCell>();
                foreach (var cell in source.cellBounds.allPositionsWithin) if (source.HasTile(cell)) result.Add(new InitialMaterialCell { Cell = cell, Point = map.GetCellCenterWorld(cell) });
                return result;
            }
            finally { Destroy(root); }
        }
        void ReleaseInitialMaterialCache() { repaintedWallAreas.Clear(); wallAreasDirty = true; nextInitialMaterialAttempt = 0; initialWallColumnsReady = false; }
    }
}
