using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        Vector3? floorEdgePoint;
        FloorLimit agedFloorLimit;
        void EnsureAgedFloorLimit()
        {
            if (agedFloorLimit != null || agedBuildFloor == null) return;
            var original = EditorActionsDBAccessor.GetDecoTile(10).floorLimit;
            agedFloorLimit = CreateAgedFloorLimit(original); agedBuildFloor.floorLimit = agedFloorLimit;
        }
        FloorLimit CreateAgedFloorLimit(FloorLimit original)
        {
            var clone = Instantiate(original); clone.name = "GradualRepair Aged Floor Edges";
            var copies = new Dictionary<TileBase, TileBase>();
            TileBase CopyEdge(TileBase source)
            {
                if (source == null) return null; if (copies.TryGetValue(source, out var cached)) return cached;
                var tile = source as Tile; if (tile?.sprite == null) return source;
                string resource = "AgedEdge." + tile.sprite.name + ".png";
                using (var input = typeof(Plugin).Assembly.GetManifestResourceStream(resource))
                {
                    if (input == null) return source;
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    using (var data = new System.IO.MemoryStream()) { input.CopyTo(data); if (!ImageConversion.LoadImage(texture, data.ToArray()) || texture.width != 24 || texture.height != 24) { Destroy(texture); throw new System.IO.InvalidDataException(resource); } }
                    texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp; agedBuildTextures.Add(texture);
                    var reference = tile.sprite; var sprite = Sprite.Create(texture, new Rect(0, 0, 24, 24), new Vector2(reference.pivot.x / reference.rect.width, reference.pivot.y / reference.rect.height), reference.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = "GradualRepair AgedEdge " + reference.name; agedBuildSprites.Add(sprite);
                    var copy = Instantiate(tile); copy.name = "GradualRepair AgedEdge " + tile.name; copy.sprite = sprite; agedBuildTiles.Add(copy); copies.Add(source, copy); return copy;
                }
            }
            foreach (var field in typeof(FloorLimit).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (field.FieldType == typeof(TileBase)) field.SetValue(clone, CopyEdge((TileBase)field.GetValue(original)));
                else if (field.FieldType == typeof(TileBase[])) { var tiles = (TileBase[])field.GetValue(original); field.SetValue(clone, tiles?.Select(CopyEdge).ToArray()); }
            }
            Log("aged_floor_edges_loaded", new { tiles = copies.Count }); return clone;
        }
        static void AgedEdgeDecoration(int __0, ref DecorationTile __result)
        {
            if (__0 == 10 && self?.floorEdgePoint != null && self.agedBuildFloor != null && self.AgedFloorAt(self.floorEdgePoint.Value)) __result = self.agedBuildFloor;
        }
        bool AgedFloorAt(Vector3 point)
        {
            if (NativeGame.GetFloorDecoration(point, null) != 10) return false;
            if (ExplicitBuildStyle(true, point, out bool aged)) return aged;
            return false;
        }
        bool AgedTrimAt(Vector3 point) => ExplicitBuildStyle('T', point, out bool aged) && aged;
        static void BeforeFloorEdge(Vector3 __0, out Vector3? __state) { __state = self?.floorEdgePoint; if (self != null) self.floorEdgePoint = self.AgedBuildAvailable() ? __0 : (Vector3?)null; }
        static void AfterFloorEdge(Vector3? __state) { if (self != null) self.floorEdgePoint = __state; }
        static Exception EndFloorEdge(Exception __exception, Vector3? __state) { AfterFloorEdge(__state); return __exception; }
        static void FloorEdgeNeighbor(Vector3 __0, DecorationTile __1, ref bool __result)
        {
            // Keep native terrain/stair and material checks. Only distinguish the
            // two visual variants that deliberately share native wood ID 10.
            if (!__result || self?.floorEdgePoint == null || __1 == null || __1.id != 10 || NativeGame.GetFloorDecoration(__0, null) != 10) return;
            if (self.AgedFloorAt(self.floorEdgePoint.Value) != self.AgedFloorAt(__0)) __result = false;
        }
        void RefreshAgedFloorEdges()
        {
            var scene = NativeGame.GetScene(Location.Tavern); if (scene == null) return;
            var maps = scene.GetTilemaps(NativeGame.CurrentSeason); var upper = maps.floorUpperTilemap; var lower = maps.floorLowerTilemap;
            var before = new HashSet<Vector3Int>(); foreach (var cell in upper.cellBounds.allPositionsWithin) if (upper.HasTile(cell)) before.Add(cell);
            var points = state.AgedDecorations.Keys.Where(k => k.StartsWith("F:")).Select(StylePoint).Distinct();
            foreach (var point in points) if (lower.HasTile(lower.WorldToCell(point)) && NativeGame.GetGroundType(point) == GroundType.Floor && NativeGame.TryGetWorldTile(point, out var world) && world.location == Location.Tavern && NativeGame.GetTileScene(world) != null)
                FloorLimitsManager.UpdateFloorLimits(point, NativeGame.GetTileScene(world));
            RemoveNewOutsideEdges(lower, upper, before);
        }
        static void RemoveNewOutsideEdges(Tilemap lower, Tilemap upper, HashSet<Vector3Int> before)
        {
            foreach (var cell in upper.cellBounds.allPositionsWithin) if (upper.HasTile(cell) && !before.Contains(cell) && !lower.HasTile(lower.WorldToCell(upper.GetCellCenterWorld(cell)))) upper.SetTile(cell, null);
        }
    }
}
