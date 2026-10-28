using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        readonly Dictionary<TileBase, TileBase> doorArchTileSources = new Dictionary<TileBase, TileBase>();
        readonly List<Rect> indoorDoorOpenings = new List<Rect>();
        void RefreshIndoorDoorOpenings()
        {
            indoorDoorOpenings.Clear(); if (!NativeComplete()) return;
            foreach (var door in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if (!door.enabled || !door.gameObject.activeInHierarchy || !door.gameObject.scene.IsValid() || door.sprite == null || !doorArchOpenings.TryGetValue(door.sprite.name, out var rows)) continue;
                var sprite = door.sprite;
                for (int y = 0; y < rows.Length / 2; y++)
                {
                    int left = rows[y * 2], right = rows[y * 2 + 1]; if (left == right) continue;
                    var a = new Vector3((left - sprite.pivot.x) / sprite.pixelsPerUnit, (sprite.rect.height - y - 1 - sprite.pivot.y) / sprite.pixelsPerUnit);
                    var b = new Vector3((right - sprite.pivot.x) / sprite.pixelsPerUnit, (sprite.rect.height - y - sprite.pivot.y) / sprite.pixelsPerUnit);
                    if (door.flipX) { a.x = -a.x; b.x = -b.x; }
                    if (door.flipY) { a.y = -a.y; b.y = -b.y; }
                    a = door.transform.TransformPoint(a); b = door.transform.TransformPoint(b);
                    indoorDoorOpenings.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
                }
            }
        }
        void RestoreDoorArchTiles(Tilemap map)
        {
            if (map != null) foreach (var cell in map.cellBounds.allPositionsWithin) { var tile = map.GetTile(cell); if (tile != null && doorArchTileSources.TryGetValue(tile, out var source)) map.SetTile(cell, source); }
            foreach (var tile in doorArchTileSources.Keys) { if (tile is Tile t && t.sprite != null) { agedBuildSprites.Remove(t.sprite); Destroy(t.sprite); } agedBuildTiles.Remove((Tile)tile); Destroy(tile); }
            doorArchTileSources.Clear();
        }
        static bool InsideDoorArch(SpriteRenderer door, Vector3 world)
        {
            if (door?.sprite == null || !doorArchOpenings.TryGetValue(door.sprite.name, out var rows)) return false;
            var sprite = door.sprite; var p = door.transform.InverseTransformPoint(world); if (door.flipX) p.x = -p.x; if (door.flipY) p.y = -p.y;
            int x = Mathf.FloorToInt(p.x * sprite.pixelsPerUnit + sprite.pivot.x);
            int y = Mathf.FloorToInt(sprite.rect.height - (p.y * sprite.pixelsPerUnit + sprite.pivot.y));
            return y >= 0 && y * 2 + 1 < rows.Length && x >= rows[y * 2] && x < rows[y * 2 + 1];
        }
        void MaskAgedDoorArches(Tilemap map)
        {
            if (map == null || agedBuildReleasing || agedBuildWall == null) return;
            var doors = Resources.FindObjectsOfTypeAll<SpriteRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && r.gameObject.scene.IsValid() && r.sprite != null && doorArchOpenings.ContainsKey(r.sprite.name)).ToArray();
            int changed = 0;
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var source = map.GetTile(cell); if (source == null) continue;
                if (doorArchTileSources.TryGetValue(source, out var original)) source = original;
                if (!agedBuildWall.tiles.Contains(source) || !(source is Tile native) || native.sprite == null) continue;
                var center = map.GetCellCenterWorld(cell);
                var near = doors.Where(d => Mathf.Abs(d.transform.position.x - center.x) < 1 && Mathf.Abs(d.transform.position.y - center.y) < 1).ToArray(); if (near.Length == 0) continue;
                var sprite = native.sprite; int width = (int)sprite.rect.width, height = (int)sprite.rect.height;
                if (width != 24 || height != 24) continue;
                var matrix = Matrix4x4.TRS(center, map.transform.rotation, map.transform.lossyScale) * map.GetTransformMatrix(cell);
                var vertices = new List<Vector2>(); var triangles = new List<ushort>(); int removed = 0;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    var p = new Vector2((x - sprite.pivot.x) / sprite.pixelsPerUnit, (y - sprite.pivot.y) / sprite.pixelsPerUnit);
                    float step = 1 / sprite.pixelsPerUnit;
                    var world = matrix.MultiplyPoint3x4(p + Vector2.one * (step * .5f));
                    if (near.Any(d => InsideDoorArch(d, world))) { removed++; continue; }
                    ushort i = (ushort)vertices.Count; vertices.Add(p); vertices.Add(p + Vector2.right * step); vertices.Add(p + Vector2.one * step); vertices.Add(p + Vector2.up * step);
                    triangles.Add(i); triangles.Add((ushort)(i + 1)); triangles.Add((ushort)(i + 2)); triangles.Add(i); triangles.Add((ushort)(i + 2)); triangles.Add((ushort)(i + 3));
                }
                if (removed == 0) continue;
                var copySprite = Instantiate(sprite); copySprite.name = "GradualRepair Door Arch " + sprite.name; OverrideSpriteGeometry(copySprite, vertices.ToArray(), triangles.ToArray()); agedBuildSprites.Add(copySprite);
                var copyTile = Instantiate(native); copyTile.name = "GradualRepair Door Arch " + native.name; copyTile.sprite = copySprite; agedBuildTiles.Add(copyTile); doorArchTileSources[copyTile] = source; map.SetTile(cell, copyTile); changed++;
            }
            if (changed > 0) Log("aged_door_arch_masked", new { tiles = changed });
        }
    }
}

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        static readonly Dictionary<string, int[]> doorArchOpenings = new Dictionary<string, int[]> {
{"Puerta-5-Top_6",new[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,16,36,13,39,11,41,11,42,11,43,11,43,11,44}},
{"Puerta-5-Top_4",new[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}},
{"Puerta-5-Top_2",new[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}},
{"Puerta-5-Top_3",new[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}},
{"Puerta-5-Top_1",new[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}},
{"Puerta-5-Top_5",new[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19,36,19,39,19,41,19,42,19,43,19,43,19,44}},
};
    }
}
