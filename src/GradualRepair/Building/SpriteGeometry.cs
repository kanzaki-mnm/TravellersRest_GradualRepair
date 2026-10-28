using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        static void OverrideSpriteGeometry(Sprite sprite, Vector2[] vertices, ushort[] triangles)
        {
            // Sprite.vertices returns pivot-relative UNIT coordinates, but
            // OverrideGeometry accepts PIXELS relative to the Sprite rect. Unity
            // applies the pivot/PPU conversion itself. Passing units silently rejects
            // negative coordinates and retains the original, unclipped mesh.
            var safe = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var pixel = vertices[i] * sprite.pixelsPerUnit + sprite.pivot;
                safe[i] = new Vector2(Mathf.Clamp(pixel.x, 0, sprite.rect.width), Mathf.Clamp(pixel.y, 0, sprite.rect.height));
            }
            sprite.OverrideGeometry(safe, triangles);
        }
    }
}
