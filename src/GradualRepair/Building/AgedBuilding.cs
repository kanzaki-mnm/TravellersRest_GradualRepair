using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        DecorationTile agedBuildFloor, agedBuildWall, agedBuildTrim;
        readonly List<Sprite> agedBuildSprites = new List<Sprite>(); readonly List<Texture2D> agedBuildTextures = new List<Texture2D>();
        readonly List<Tile> agedBuildTiles = new List<Tile>();
        // Construction confirmation is only an in-memory commit; AfterSave owns disk persistence.
        Dictionary<string, bool> buildStyleBefore;
        bool buildStyleWasDirty, buildReverting, agedStyleRefresh, agedBuildReleasing, lastAgedTavern;
        int buildPainting = -1;
        float nextAgedBuild;
        bool CanTrackBuildStyles() => bound && projectInitialized && !failed && Solo() && !agedBuildReleasing;
        bool AgedBuildAvailable() => CanTrackBuildStyles() && NativeComplete();
        static string StyleKey(bool floor, Vector3 point) => StyleKey(floor ? 'F' : 'W', point);
        static string StyleKey(char kind, Vector3 point) => kind + ":" + Mathf.FloorToInt(point.x * 2).ToString(CultureInfo.InvariantCulture) + "," + Mathf.FloorToInt(point.y * 2).ToString(CultureInfo.InvariantCulture);
        static Vector3 StylePoint(string key) { var xy = key.Substring(2).Split(','); return new Vector3(int.Parse(xy[0], CultureInfo.InvariantCulture) * .5f, int.Parse(xy[1], CultureInfo.InvariantCulture) * .5f, 0); }
        bool ExplicitBuildStyle(bool floor, Vector3 point, out bool aged) => ExplicitBuildStyle(floor ? 'F' : 'W', point, out aged);
        bool ExplicitBuildStyle(char kind, Vector3 point, out bool aged) => state.AgedDecorations.TryGetValue(StyleKey(kind, point), out aged);
        DecorationTile MakeAgedBuildingTile(DecorationTile source, string prefix, string label, Dictionary<string, Sprite> sprites, int count = 16)
        {
            var clone = Instantiate(source); clone.name = "GradualRepair " + label; clone.tiles = new TileBase[count];
            for (int i = 0; i < count; i++) { if (!sprites.TryGetValue(prefix + i, out var sprite)) throw new InvalidOperationException("Missing aged building sprite " + prefix + i); var tile = ScriptableObject.CreateInstance<Tile>(); tile.name = "GradualRepair " + prefix + i; tile.sprite = sprite; var native = source.tiles[i % source.tiles.Length] as Tile; if (native != null) { tile.colliderType = native.colliderType; tile.color = native.color; tile.transform = native.transform; tile.flags = native.flags; } clone.tiles[i] = tile; agedBuildTiles.Add(tile); }
            clone.icon = LoadBuildingIcon(source.id == 10 ? "AgedFloor_Icon.png" : source.id == 12 ? "AgedStoneWall_Icon.png" : "AgedSkirting_Icon.png", source.icon); return clone;
        }
        Sprite LoadBuildingIcon(string resource, Sprite reference)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                using (var input = typeof(Plugin).Assembly.GetManifestResourceStream(resource) ?? throw new System.IO.FileNotFoundException(resource))
                using (var data = new System.IO.MemoryStream()) { input.CopyTo(data); if (!ImageConversion.LoadImage(texture, data.ToArray()) || texture.width != 32 || texture.height != 32) throw new System.IO.InvalidDataException("Building icon must be 32x32: " + resource); }
                texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
                var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), reference != null ? reference.pixelsPerUnit : 100, 0, SpriteMeshType.FullRect);
                sprite.name = "GradualRepair " + resource; agedBuildTextures.Add(texture); agedBuildSprites.Add(sprite); return sprite;
            }
            catch { Destroy(texture); throw; }
        }
        void EnsureAgedBuildingAssets()
        {
            if (agedBuildFloor != null && agedBuildWall != null && agedBuildTrim != null) return;
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>().Where(s => s.name.StartsWith("TS_Wood7_") || s.name.StartsWith("TS_Wall20_")).GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
            if (agedBuildFloor == null) agedBuildFloor = MakeAgedBuildingTile(EditorActionsDBAccessor.GetDecoTile(10), "TS_Wood7_", RepairText.Get("building.aged-floor"), sprites);
            if (agedBuildWall == null) agedBuildWall = MakeAgedBuildingTile(EditorActionsDBAccessor.GetDecoTile(12), "TS_Wall20_", RepairText.Get("building.aged-wall"), sprites);
            if (agedBuildTrim == null) { var source = EditorActionsDBAccessor.GetDecoTile(21); var reference = (source.tiles[0] as Tile).sprite; for (int i = 0; i < 4; i++) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); using (var input = typeof(Plugin).Assembly.GetManifestResourceStream("AgedSkirting_" + i + ".png")) using (var data = new System.IO.MemoryStream()) { input.CopyTo(data); if (!ImageConversion.LoadImage(texture, data.ToArray())) throw new System.IO.InvalidDataException("Invalid aged skirting image"); } texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp; agedBuildTextures.Add(texture); var sprite = Sprite.Create(texture, new Rect(0, 0, 24, 24), new Vector2(reference.pivot.x / reference.rect.width, reference.pivot.y / reference.rect.height), reference.pixelsPerUnit, 0, SpriteMeshType.FullRect); sprite.name = "GradualRepair AgedSkirting_" + i; agedBuildSprites.Add(sprite); sprites["AgedSkirting_" + i] = sprite; } agedBuildTrim = MakeAgedBuildingTile(source, "AgedSkirting_", RepairText.Get("building.aged-trim"), sprites, 4); agedBuildTrim.tileInfo = new TilesInfoBase { numTilesX = 4, numTilesY = 1 }; }
            EnsureAgedFloorLimit(); agedStyleRefresh = true; Log("aged_build_assets", new { floor = agedBuildFloor.id, wall = agedBuildWall.id });
        }
        void UpdateAgedBuilding()
        {
            if (!AgedBuildAvailable() || NativeGame.Game == null || !NativeGame.WorldLoaded(NativeGame.Game)) return;
            var player = PlayerController.GetPlayer(1); bool inside = player != null && NativeGame.TryGetWorldTile(player.transform.position, out var at) && at.location == Location.Tavern; if (inside && !lastAgedTavern) agedStyleRefresh = true; lastAgedTavern = inside;
            if (agedBuildFloor == null || agedBuildWall == null || agedBuildTrim == null) { if (Time.unscaledTime < nextAgedBuild) return; nextAgedBuild = Time.unscaledTime + 5; try { EnsureAgedBuildingAssets(); } catch (Exception ex) { Log("aged_build_assets_pending", ex.Message); return; } }
            if (agedStyleRefresh) { agedStyleRefresh = false; RefreshAgedStyleTiles(); }
        }
        static void AddAgedBuildingChoices(TavernConstructionUI __instance, TavernFloor __0)
        {
            if (self == null || !self.AgedBuildAvailable()) return;
            try { self.InitializeAgedMaterials(); self.EnsureAgedBuildingAssets(); foreach (var tile in new[] { self.agedBuildFloor, self.agedBuildWall, self.agedBuildTrim }) if (tile.tavernFloor.HasFlag(__0) && !__instance.decorationsActions.Contains(tile)) { int basic = __instance.decorationsActions.FindIndex(t => t.id == tile.id); __instance.decorationsActions.Insert(basic < 0 ? __instance.decorationsActions.Count : basic + 1, tile); } }
            catch (Exception ex) { self.Log("aged_build_catalog_error", ex.ToString()); }
        }
        static bool AgedBuildingName(DecorationTile __instance, ref string __result)
        {
            if (self == null) return true;
            if (__instance == self.agedBuildFloor) { __result = RepairText.Get("building.aged-floor"); return false; }
            if (__instance == self.agedBuildWall) { __result = RepairText.Get("building.aged-wall"); return false; }
            if (__instance == self.agedBuildTrim) { __result = RepairText.Get("building.aged-trim"); return false; }
            return true;
        }
        bool WallMaterialStyle(Vector3 point, Tilemap map, out bool aged)
        {
            if (ExplicitBuildStyle('W', point, out aged)) return true;
            // Native ACOAMPHMCIN draws the wall from base + (0,.5).
            // Consult that same column's underlying base material, never neighbors.
            return ExplicitBuildStyle('W', point + Vector3.down * .5f, out aged);
        }
        static bool DrawAgedBuildingTile(DecorationTile __instance, Vector3 __0, Tilemap __1, float __2, float __3, bool __4, bool __5)
        {
            if (self == null || !self.AgedBuildAvailable() || self.agedBuildFloor == null || self.agedBuildWall == null || __instance == self.agedBuildFloor || __instance == self.agedBuildWall || __instance == self.agedBuildTrim) return true;
            if (__1 != null && __1.GetComponentInParent<EditorTileMaps>(true) != null) return true;
            bool trim = __instance.editorAction == EditorAction.ChangeDecoWallTrim;
            if (trim && __instance.id == 21) { if (!self.AgedTrimAt(__0)) return true; NativeGame.DrawDecoration(self.agedBuildTrim, __0, __1, __2, __3, __4, __5); return false; }
            bool floor = __instance.editorAction == EditorAction.ChangeDecoFloor;
            if (!(floor && __instance.id == 10) && !(__instance.editorAction == EditorAction.ChangeDecoWall && __instance.id == 12)) return true;
            bool selected = floor ? self.ExplicitBuildStyle(true, __0, out bool agedFloor) && agedFloor : self.WallMaterialStyle(__0, __1, out bool agedWall) && agedWall;
            if (!selected) return true;
            NativeGame.DrawDecoration((floor ? self.agedBuildFloor : self.agedBuildWall), __0, __1, __2, __3, __4, __5); return false;
        }
        void CaptureBuildStyles()
        {
            if (buildStyleBefore != null) return; buildStyleBefore = new Dictionary<string, bool>(state.AgedDecorations); buildStyleWasDirty = dirty;
        }
        static void BeforeBuildApply(EditorAction __0, DecorationTile __2, out int __state)
        {
            __state = self == null ? -1 : self.buildPainting;
            if (self == null || !self.CanTrackBuildStyles()) return;
            self.InitializeAgedMaterials(); self.CaptureBuildStyles(); self.buildPainting = (__0 == EditorAction.ChangeDecoFloor || __0 == EditorAction.ChangeDecoWall || __0 == EditorAction.ChangeDecoWallTrim) ? ((__2 == self.agedBuildFloor || __2 == self.agedBuildWall || __2 == self.agedBuildTrim) ? 1 : 0) : -1;
        }
        static void AfterBuildApply(EditorAction __0, List<Vector2> __1, bool __result, int __state)
        {
            if (self == null) return; self.buildPainting = __state;
            if (__result) { self.indoorGeometryDirty = true; self.agedStyleRefresh = true; }
            if (__result && __0 == EditorAction.RemoveFloor && self.CanTrackBuildStyles()) foreach (var point in __1) for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) { var p = (Vector3)point + new Vector3(x * .5f, y * .5f); self.state.AgedDecorations.Remove(StyleKey(true, p)); for (int row = -1; row <= 6; row++) { self.state.AgedDecorations.Remove(StyleKey(false, p + Vector3.up * (row * .5f))); self.state.AgedDecorations.Remove(StyleKey('T', p + Vector3.up * (row * .5f))); } self.dirty = true; self.agedStyleRefresh = true; }
        }
        static Exception EndBuildApply(Exception __exception, int __state) { if (self != null) self.buildPainting = __state; return __exception; }
        void TrackBuildPaint(Vector3 point, int id, bool floor) => TrackBuildPaint(point, id, floor ? 'F' : 'W');
        void TrackBuildPaint(Vector3 point, int id, char kind)
        {
            if (buildPainting < 0 || !CanTrackBuildStyles() || !NativeGame.TryGetWorldTile(point, out var world) || world.location != Location.Tavern) return;
            CaptureBuildStyles(); bool floor = kind == 'F'; bool aged = buildPainting == 1 && id == (floor ? 10 : kind == 'T' ? 21 : 12);
            // Include the lintel and roof-cap rows belonging to this wall face.
            for (int row = floor ? 0 : -1; row <= (floor ? 0 : 6); row++) state.AgedDecorations[StyleKey(kind, point + Vector3.up * (row * .5f))] = aged;
            dirty = true; agedStyleRefresh = true;
        }
        static void BeforeBuildTrimPaint(Vector3 __0, int __1) { self?.TrackBuildPaint(__0, __1, 'T'); }
        static void BeforeBuildFloorPaint(Vector3 __0, int __1) { self?.TrackBuildPaint(__0, __1, true); }
        bool BuildStylesChanged() => buildStyleBefore != null && (buildStyleBefore.Count != state.AgedDecorations.Count || buildStyleBefore.Any(p => !state.AgedDecorations.TryGetValue(p.Key, out bool value) || p.Value != value));
        static void HasAgedBuildingChanges(ref bool __result) { if (self != null && self.CanTrackBuildStyles()) __result |= self.BuildStylesChanged(); }
        static void BeforeBuildRevert() { if (self != null) self.buildReverting = true; }
        static void AfterBuildRevert()
        {
            if (self == null) return; self.indoorGeometryDirty = true; if (self.buildStyleBefore != null) { var changedKeys = self.state.AgedDecorations.Keys.ToArray(); self.state.AgedDecorations = self.buildStyleBefore; self.buildStyleBefore = null; self.dirty = self.buildStyleWasDirty; self.agedStyleRefresh = true; self.RefreshAgedStyleTiles(changedKeys); }
            self.buildReverting = false;
        }
        static void AfterBuildReset() { if (self != null && !self.buildReverting) self.buildStyleBefore = null; }
        void RefreshAgedStyleTiles(IEnumerable<string> extra = null)
        {
            if (!bound || !projectInitialized) return; var scene = NativeGame.GetScene(Location.Tavern); if (scene == null) return;
            // Material changes must never rebuild world geometry. A global wall
            // refresh also rebuilds roof/floor edges and can materialize stale cells.
            // Replace only tiles already present in the native render maps.
            var maps = scene.GetTilemaps(NativeGame.CurrentSeason);
            RestoreDoorArchTiles(scene.wallTilemaps.wallFrontNoColliders);
            if (agedBuildFloor != null) { RetileAgedMaterial(maps.floorLowerTilemap, EditorActionsDBAccessor.GetDecoTile(10), agedBuildFloor, 'F'); RetileAgedMaterial(maps.floorUpperTilemap, EditorActionsDBAccessor.GetDecoTile(10), agedBuildFloor, 'F'); }
            if (agedBuildWall != null) { foreach (var map in new[] { scene.wallTilemaps.wallBackTilemap, scene.wallTilemaps.wallFrontNoColliders }) RetileAgedMaterial(map, EditorActionsDBAccessor.GetDecoTile(12), agedBuildWall, 'W'); }
            if (agedBuildTrim != null) RetileAgedMaterial(scene.wallTilemaps.wallBackTrimTilemap, EditorActionsDBAccessor.GetDecoTile(21), agedBuildTrim, 'T');
            MaskAgedDoorArches(scene.wallTilemaps.wallFrontNoColliders);
            RefreshAgedFloorEdges();
        }
        void RetileAgedMaterial(Tilemap map, DecorationTile normal, DecorationTile aged, char kind)
        {
            if (map == null || normal == null || aged == null) return;
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(cell); if (tile == null) continue;
                if (doorArchTileSources.TryGetValue(tile, out var archSource)) tile = archSource;
                int index = Array.IndexOf(normal.tiles, tile); if (index < 0) index = Array.IndexOf(aged.tiles, tile);
                if (index < 0 || index >= normal.tiles.Length || index >= aged.tiles.Length) continue;
                // StyleKey floors directly into the half-unit render cell.
                var renderedPoint = map.GetCellCenterWorld(cell);
                var point = renderedPoint;
                // Wall rows are drawn from a single anchor with a vertical offset.
                if (kind != 'F') point -= Vector3.up * ((index / normal.tileInfo.numTilesX) * .5f);
                bool selected; bool hasStyle = kind == 'W' ? WallMaterialStyle(point, map, out selected) : ExplicitBuildStyle(kind, point, out selected);
                // A door's remaining wall row is also present in the authored
                // material map at its rendered coordinate. Its native drawing
                // anchor need not have an imported tutorial tile (the opening
                // deliberately has none). Keep this lookup in the SAME column.
                if (kind == 'W' && !hasStyle) hasStyle = WallMaterialStyle(renderedPoint, map, out selected);
                bool useAged = !agedBuildReleasing && hasStyle && selected;
                var replacement = useAged ? aged.tiles[index] : normal.tiles[index];
                if (tile != replacement) map.SetTile(cell, replacement);
            }
        }
        void ReleaseAgedBuilding()
        {
            buildPainting = -1; buildStyleBefore = null; buildReverting = false; agedBuildReleasing = true;
            if (agedBuildFloor != null || agedBuildWall != null || agedBuildTrim != null) { RefreshAgedStyleTiles(); if (agedBuildFloor != null) Destroy(agedBuildFloor); if (agedBuildWall != null) Destroy(agedBuildWall); if (agedBuildTrim != null) Destroy(agedBuildTrim); foreach (var tile in agedBuildTiles) Destroy(tile); agedBuildTiles.Clear(); foreach (var sprite in agedBuildSprites) Destroy(sprite); agedBuildSprites.Clear(); foreach (var texture in agedBuildTextures) Destroy(texture); agedBuildTextures.Clear(); }
            doorArchTileSources.Clear(); if (agedFloorLimit != null) Destroy(agedFloorLimit); agedFloorLimit = null; agedBuildFloor = agedBuildWall = agedBuildTrim = null; lastAgedTavern = false; agedBuildReleasing = false; nextAgedBuild = 0; agedStyleRefresh = true;
        }
    }
}





