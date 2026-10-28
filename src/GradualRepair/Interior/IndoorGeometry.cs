using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        Vector2 SpriteWorld(IndoorPatch p, Vector2 v) { if (p.Source.flipX) v.x = -v.x; if (p.Source.flipY) v.y = -v.y; return p.Source.transform.TransformPoint(v); }
        Vector2 SpriteLocal(IndoorPatch p, Vector2 v) { Vector2 q = p.Source.transform.InverseTransformPoint(v); if (p.Source.flipX) q.x = -q.x; if (p.Source.flipY) q.y = -q.y; return q; }
        static List<Vector2> ClipEdge(List<Vector2> polygon, int axis, float edge, bool greater)
        {
            var result = new List<Vector2>(); if (polygon.Count == 0) return result; var a = polygon[polygon.Count - 1]; bool insideA = greater ? a[axis] >= edge : a[axis] <= edge;
            foreach (var b in polygon) { bool insideB = greater ? b[axis] >= edge : b[axis] <= edge; if (insideA != insideB) { float t = (edge - a[axis]) / (b[axis] - a[axis]); result.Add(a + (b - a) * t); } if (insideB) result.Add(b); a = b; insideA = insideB; }
            return result;
        }
        static IEnumerable<List<Vector2>> OutsideRect(List<Vector2> polygon, Rect rect)
        {
            if (polygon.Count < 3) yield break;
            if (polygon.All(p => p.x <= rect.xMin) || polygon.All(p => p.x >= rect.xMax) || polygon.All(p => p.y <= rect.yMin) || polygon.All(p => p.y >= rect.yMax)) { yield return polygon; yield break; }
            // Disjoint left/right strips, then bottom/top within the middle.
            var left = ClipEdge(polygon, 0, rect.xMin, false); if (left.Count >= 3) yield return left;
            var right = ClipEdge(polygon, 0, rect.xMax, true); if (right.Count >= 3) yield return right;
            var middle = ClipEdge(ClipEdge(polygon, 0, rect.xMin, true), 0, rect.xMax, false);
            var bottom = ClipEdge(middle, 1, rect.yMin, false); if (bottom.Count >= 3) yield return bottom;
            var top = ClipEdge(middle, 1, rect.yMax, true); if (top.Count >= 3) yield return top;
        }
        void RebuildIndoorGeometry(IndoorPatch part)
        {
            RefreshWallAreas(); bool excluded = false; var keep = new List<Rect>(); part.Cells = part.Kept = 0; bool floor = part.Group.StartsWith("floor-"); var bounds = part.Bounds;
            var nativeScene = NativeGame.GetScene(Location.Tavern); var nativeFloor = floor && nativeScene != null ? nativeScene.GetTilemaps(NativeGame.CurrentSeason).floorLowerTilemap : null;
            var openings = floor ? Array.Empty<Rect>() : indoorDoorOpenings.Where(r => r.Overlaps(Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y))).ToArray();
            for (int y = Mathf.FloorToInt(bounds.min.y * 2); y < Mathf.CeilToInt(bounds.max.y * 2); y++) for (int x = Mathf.FloorToInt(bounds.min.x * 2); x < Mathf.CeilToInt(bounds.max.x * 2); x++)
            {
                var center = new Vector3((x + .5f) * .5f, (y + .5f) * .5f, 0); int decor = floor ? NativeGame.GetFloorDecoration(center, null) : NativeGame.GetWallDecoration(center);
                string key = part.Id + "@" + x + "," + y; if (!state.IndoorDecorBaseline.TryGetValue(key, out int original)) { original = floor && NativeGame.GetGroundType(center) != GroundType.Floor ? int.MinValue : decor; state.IndoorDecorBaseline[key] = original; dirty = true; }
                // Decoration is cosmetic; only removing the underlying floor/wall clips unrepaired damage.
                if (original == int.MinValue || (floor && nativeFloor != null && !nativeFloor.HasTile(nativeFloor.WorldToCell(center)))) { excluded = true; continue; }
                part.Cells++; if (floor ? NativeGame.GetGroundType(center) == GroundType.Floor : original == 0 || decor != 0) { part.Kept++; keep.Add(new Rect(x * .5f, y * .5f, .5f, .5f)); }
            }
            part.Visible = part.Kept > 0; if (!part.Visible) return;
            var originalVertices = part.Original.vertices; var originalTriangles = part.Original.triangles;
            if (part.Kept == part.Cells && !excluded && openings.Length == 0) { SetIndoorMesh(part, originalVertices, originalTriangles); return; }
            var vertices = new List<Vector2>(); var triangles = new List<ushort>();
            foreach (var cell in keep) for (int i = 0; i < originalTriangles.Length; i += 3)
            {
                var polygon = new List<Vector2> { SpriteWorld(part, originalVertices[originalTriangles[i]]), SpriteWorld(part, originalVertices[originalTriangles[i + 1]]), SpriteWorld(part, originalVertices[originalTriangles[i + 2]]) };
                polygon = ClipEdge(polygon, 0, cell.xMin, true); polygon = ClipEdge(polygon, 0, cell.xMax, false); polygon = ClipEdge(polygon, 1, cell.yMin, true); polygon = ClipEdge(polygon, 1, cell.yMax, false); if (polygon.Count < 3) continue;
                var pieces = new List<List<Vector2>> { polygon };
                foreach (var opening in openings) pieces = pieces.SelectMany(p => OutsideRect(p, opening)).ToList();
                foreach (var piece in pieces)
                {
                    if (vertices.Count + piece.Count > 65000) throw new InvalidOperationException("Indoor repair geometry exceeded safe limit"); int start = vertices.Count; foreach (var vertex in piece) vertices.Add(SpriteLocal(part, vertex)); for (int j = 1; j < piece.Count - 1; j++) { triangles.Add((ushort)start); triangles.Add((ushort)(start + j)); triangles.Add((ushort)(start + j + 1)); }
                }
            }
            part.Visible = vertices.Count > 0; if (part.Visible) SetIndoorMesh(part, vertices.ToArray(), triangles.ToArray());
            if (part.Group == "floor-02") Log("entrance_floor_clipped", new { part.Cells, part.Kept, part.Visible, vertices = vertices.Count, worldMin = vertices.Count == 0 ? null : vertices.Select(v => SpriteWorld(part, v)).Aggregate(Vector2.Min).ToString("F3"), worldMax = vertices.Count == 0 ? null : vertices.Select(v => SpriteWorld(part, v)).Aggregate(Vector2.Max).ToString("F3") });
            if (openings.Length > 0) Log("wall_damage_door_clipped", new { part.Id, openings = openings.Length, vertices = vertices.Count });
        }
        static void SetIndoorMesh(IndoorPatch part, Vector2[] vertices, ushort[] triangles)
        {
            OverrideSpriteGeometry(part.Owned, vertices, triangles);
            // Refresh SpriteRenderer's cached geometry after mutating a sprite
            // already assigned to the tutorial renderer or its post-Mai copy.
            foreach (var renderer in new[] { part.Source, part.Copy }) if (renderer != null) { renderer.sprite = null; renderer.sprite = part.Owned; }
        }
        Bounds IndoorBounds(int index)
        {
            var parts = indoorPatches.Where(p => p.Group == RepairModel.InteriorJobs[index - 14].Id).ToArray(); if (parts.Length == 0) return new Bounds(); var b = parts[0].Bounds; foreach (var p in parts) b.Encapsulate(p.Bounds); return b;
        }
    }
}


