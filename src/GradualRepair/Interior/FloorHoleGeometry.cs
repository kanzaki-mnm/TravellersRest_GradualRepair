using System;
using System.Collections.Generic;
using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        // Authored soil silhouettes from the seven native floor-damage sprites.
        // Pixel row spans are generated offline; no texture reads during play.
        static readonly Dictionary<string, int[]> floorHolePixels = new Dictionary<string, int[]>{
            {"Tutorial_FloorDamage1",new[]{117,14,175,19,117,19,176,25,116,25,177,26,116,26,193,27,51,27,193,31,51,31,192,32,51,32,191,33,51,33,193,34,50,34,193,35,50,35,188,36,67,36,137,37,68,37,117,38,70,38,117,47,69,47,97,48}},
            {"Tutorial_FloorDamage2",new[]{49,14,191,19,49,19,184,20,49,20,191,24,68,24,191,26,68,26,210,27,68,27,209,28,67,28,209,36,96,36,191,37,96,37,192,41,96,41,196,42,96,42,193,43,96,43,191,44,96,44,192,48,127,48,192,49,127,49,173,52,126,52,173,60}},
            {"Tutorial_FloorDamage3",new[]{115,13,144,14,115,14,174,17,115,17,175,24,114,24,175,25,99,25,192,27,97,27,193,36,96,36,194,37,68,37,219,40,68,40,218,44,68,44,220,47,68,47,199,48,86,48,193,52,86,52,197,53,86,53,194,54,86,54,192,55,86,55,193,60,86,60,163,61,18,61,163,68,19,68,163,71,49,71,145,73,50,73,145,74,51,74,145,75,49,75,145,76,54,76,145,77,55,77,144,78,56,78,144,79,49,79,144,80,52,80,144,81,53,81,144,83}},
            {"Tutorial_FloorDamage4",new[]{91,14,141,15,91,15,140,16,91,16,139,22,91,22,141,23,91,23,140,24,91,24,138,25,90,25,125,26,31,26,124,27,26,27,124,31,26,31,119,32,30,32,123,33,33,33,123,34,34,34,123,36}},
            {"Tutorial_FloorDamage5",new[]{88,15,182,16,88,16,181,17,88,17,180,23,88,23,182,25,88,25,164,26,86,26,163,27,68,27,163,31,68,31,160,32,68,32,162,36,68,36,161,37,99,37,151,38,98,38,145,42,98,42,149,43,98,43,146,44,99,44,144,45,98,45,145,49,97,49,145,50,96,50,146,51,68,51,163,60,73,60,163,61,72,61,164,62,67,62,165,63,38,63,193,73,68,73,163,74,69,74,163,80,69,80,164,85,86,85,147,86,89,86,146,89,87,89,146,91,87,91,145,98,86,98,146,99,68,99,163,109}},
            {"Tutorial_FloorDamage6",new[]{175,14,222,16,178,16,222,21,178,21,221,23,177,23,222,24,176,24,203,25,174,25,203,26,145,26,203,29,145,29,204,36,175,36,204,37,149,37,205,38,174,38,222,44,175,44,222,48,177,48,193,49,178,49,193,50,98,50,193,60,127,60,193,61,127,61,194,62,127,62,222,67,128,67,222,69,127,69,222,71,127,71,221,72,127,72,199,73,126,73,193,74,100,74,193,76,98,76,193,77,98,77,197,78,98,78,194,79,98,79,192,80,98,80,193,85,97,85,194,86,83,86,194,87,83,87,221,88,81,88,222,96,81,96,204,98,14,98,204,102,14,102,203,108,128,108,204,110,128,110,222,112,127,112,222,118,132,118,222,119,133,119,222,120}},
            {"Tutorial_FloorDamage7",new[]{193,14,223,15,128,15,223,21,129,21,223,23,129,23,222,24,129,24,195,25,129,25,196,26,52,26,196,27,51,27,196,29,50,29,196,33,50,33,195,36,69,36,163,37,69,37,161,38,70,38,160,40,70,40,163,42,70,42,164,48,98,48,147,50,97,50,147,51,98,51,147,59,100,59,145,60}},
        };
        const float FloorRepairReach = .25f, WallRepairReach = .55f;
        float IndoorReachLimit(IndoorPatch part) => part.Group.StartsWith("floor-") ? FloorRepairReach : part.Group == "wall-fireplace" || part.Group.StartsWith("wall-kitchen-") ? WallRepairReach : .12f;
        void BindFloorHoles(IndoorPatch part)
        {
            if (!part.Group.StartsWith("floor-")) return;
            if (!floorHolePixels.TryGetValue(part.Original.name, out var pixels)) { part.FloorHoles = Array.Empty<Rect>(); part.HoleCenter = part.Bounds.center; Log("floor_hole_unmapped", new { part.Id, sprite = part.Original.name }); return; }
            var sprite = part.Original; part.FloorHoles = new Rect[pixels.Length / 4]; Bounds all = new Bounds();
            for (int i = 0; i < pixels.Length; i += 4)
            {
                var low = SpriteWorld(part, new Vector2((pixels[i] - sprite.pivot.x) / sprite.pixelsPerUnit, (sprite.rect.height - pixels[i + 3] - sprite.pivot.y) / sprite.pixelsPerUnit));
                var high = SpriteWorld(part, new Vector2((pixels[i + 2] - sprite.pivot.x) / sprite.pixelsPerUnit, (sprite.rect.height - pixels[i + 1] - sprite.pivot.y) / sprite.pixelsPerUnit));
                var rect = Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y)); part.FloorHoles[i / 4] = rect;
                var bounds = new Bounds(rect.center, new Vector3(rect.width, rect.height, 0)); if (i == 0) all = bounds; else all.Encapsulate(bounds);
            }
            part.HoleCenter = all.center;
        }
        Vector3 ClosestFloorHole(IndoorPatch part, Vector3 feet)
        {
            Vector3 closest = part.HoleCenter; float best = float.MaxValue;
            if (part.FloorHoles == null || part.FloorHoles.Length == 0) return feet + Vector3.up * 10;
            foreach (var rect in part.FloorHoles)
            {
                var point = new Vector3(Mathf.Clamp(feet.x, rect.xMin, rect.xMax), Mathf.Clamp(feet.y, rect.yMin, rect.yMax), feet.z);
                float distance = (point - feet).sqrMagnitude; if (distance < best) { best = distance; closest = point; }
            }
            return closest;
        }
    }
}
