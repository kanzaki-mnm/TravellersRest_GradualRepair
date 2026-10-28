using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace GradualRepair
{
    // Owns only preview resources. Selection, payment and save state stay in Plugin.
    internal sealed class RepairPreviewView
    {
        Vector3 exteriorViewPosition;
        float exteriorViewSize, fieldWidth;
        Camera fieldCamera;
        RenderTexture fieldTexture;
        RawImage fieldImage;
        Bounds[] fieldBounds;
        int fieldRenderedCount = -1, fieldOutlinedSelection = -2;
        readonly Dictionary<Sprite, Sprite> fieldOutlineSprites = new Dictionary<Sprite, Sprite>();
        internal bool IsCreated => fieldCamera != null;
        internal void Create(Camera native, Bounds[] exteriorBounds, RectTransform repairRoot, float width)
        {
            fieldBounds = exteriorBounds; fieldWidth = width;
            Bounds all = fieldBounds[0]; foreach (var bounds in fieldBounds) all.Encapsulate(bounds);
            var go = new GameObject("GradualRepair Field Camera"); fieldCamera = go.AddComponent<Camera>(); fieldCamera.CopyFrom(native);
            var urpType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData")).FirstOrDefault(t => t != null);
            if (urpType != null) go.AddComponent(urpType);
            fieldCamera.transform.rotation = native.transform.rotation;
            fieldCamera.orthographic = true; fieldCamera.rect = new Rect(0, 0, 1, 1);
            fieldCamera.orthographicSize = Mathf.Max(all.size.y / 1.45f, all.size.x / ((fieldWidth / 800) * 1.1f));
            // Leave a clear strip on the left for the compact material panel.
            fieldCamera.transform.position = new Vector3(all.center.x + fieldCamera.orthographicSize * .52f, all.center.y, native.transform.position.z);
            fieldCamera.cullingMask = native.cullingMask & ~(1 << 5); fieldCamera.depth = -100;
            fieldCamera.clearFlags = CameraClearFlags.SolidColor; fieldCamera.backgroundColor = new Color(.15f, .22f, .12f);
            fieldCamera.useOcclusionCulling = false; fieldCamera.enabled = false; fieldRenderedCount = -1;
            fieldTexture = new RenderTexture(Mathf.Clamp(Screen.width, 640, 2560), Mathf.Clamp(Screen.height, 400, 1600), 24);
            fieldTexture.name = "GradualRepair Field"; fieldTexture.filterMode = FilterMode.Point; fieldTexture.Create(); fieldCamera.targetTexture = fieldTexture;
            fieldCamera.aspect = fieldWidth / 800f;
            fieldImage = RepairUiLayout.Rect("Field View", repairRoot, 0, 0, fieldWidth, 800).gameObject.AddComponent<RawImage>(); fieldImage.texture = fieldTexture; fieldImage.raycastTarget = false; fieldImage.transform.SetAsFirstSibling();
            fieldOutlinedSelection = -2;

            exteriorViewPosition = fieldCamera.transform.position; exteriorViewSize = fieldCamera.orthographicSize;
        }
        internal bool NeedsRender(int revision) => fieldCamera != null && fieldRenderedCount != revision;
        internal void Render(int revision, SpriteRenderer target, Material outline)
        {
            if (fieldCamera == null) return;
            if (fieldRenderedCount == revision) return;
            // The shared native outline is used only during this camera render.
            // Always restore the target before the normal game camera can render.
            var padded = target != null && outline != null && target.sprite != null ? FieldOutlineSprite(target.sprite) : null;
            RenderWithRepairOutline(target, outline, () => fieldCamera.Render(), padded);
            fieldRenderedCount = revision;
        }
        static void RenderWithRepairOutline(SpriteRenderer target, Material outline, Action render, Sprite padded = null)
        {
            if (target == null || outline == null) { render(); return; }
            var original = target.sharedMaterial; var originalSprite = target.sprite;
            try { if (padded != null) target.sprite = padded; target.sharedMaterial = outline; render(); }
            finally { if (target != null) { target.sharedMaterial = original; target.sprite = originalSprite; } }
        }
        internal Rect Project(Bounds b)
        {
            var low = fieldCamera.WorldToViewportPoint(b.min); var high = fieldCamera.WorldToViewportPoint(b.max);
            return Rect.MinMaxRect(low.x * fieldWidth, (1 - high.y) * 800, high.x * fieldWidth, (1 - low.y) * 800);
        }
        internal Bounds ExteriorBounds(int index) => fieldBounds[index];
        internal void Invalidate() => fieldRenderedCount = -1;
        internal void Select(int selected)
        {
            if (fieldOutlinedSelection == selected) return;
            fieldOutlinedSelection = selected; Invalidate();
        }
        internal void FrameInterior(Bounds? indoor)
        {
            if (fieldCamera == null) return;
            if (!indoor.HasValue) { fieldCamera.transform.position = exteriorViewPosition; fieldCamera.orthographicSize = exteriorViewSize; }
            else
            {
                var bounds = indoor.Value;
                fieldCamera.orthographicSize = Mathf.Max(bounds.size.y / 1.35f, bounds.size.x / (fieldCamera.aspect * 1.1f)); fieldCamera.transform.position = new Vector3(bounds.center.x - fieldCamera.orthographicSize * .25f, bounds.center.y, fieldCamera.transform.position.z);
            }
            Invalidate();
        }
        internal void Release()
        {
            if (fieldCamera != null) { fieldCamera.targetTexture = null; Destroy(fieldCamera.gameObject); }
            fieldCamera = null;
            if (fieldTexture != null) { fieldTexture.Release(); Destroy(fieldTexture); }
            fieldTexture = null;
            fieldImage = null; fieldOutlinedSelection = -2; fieldBounds = null; ReleaseFieldOutlineSprites();
        }
        Sprite FieldOutlineSprite(Sprite original)
        {
            if (fieldOutlineSprites.TryGetValue(original, out var cached) && cached != null) return cached;
            // These native facade sprites are slices of a shared sheet, not packed
            // rotated atlas entries. Keep any unsupported atlas untouched.
            if (original.packed) return original;
            var sprite = CreatePaddedOutlineSprite(original); fieldOutlineSprites[original] = sprite; return sprite;
        }
        static Sprite CreatePaddedOutlineSprite(Sprite original)
        {
            const int padding = 2; var rect = original.rect; int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
            var texture = new Texture2D(width + padding * 2, height + padding * 2, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[texture.width * texture.height]; var sheet = original.texture;
            if (sheet.isReadable)
            {
                var source = sheet.GetPixels32(); for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[(y + padding) * texture.width + x + padding] = source[(y + (int)rect.y) * sheet.width + x + (int)rect.x];
                texture.SetPixels32(pixels);
            }
            else
            {
                texture.SetPixels32(pixels); texture.Apply();
                var previous = RenderTexture.active; var temporary = RenderTexture.GetTemporary(sheet.width, sheet.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                try { Graphics.Blit(sheet, temporary); RenderTexture.active = temporary; texture.ReadPixels(rect, padding, padding, false); }
                catch { Destroy(texture); throw; }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
            }
            texture.Apply(false, false);
            var pivot = (original.pivot + Vector2.one * padding) / new Vector2(texture.width, texture.height);
            var result = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, original.pixelsPerUnit, 0, SpriteMeshType.FullRect); result.name = original.name + " Repair Preview Padded"; return result;
        }
        void ReleaseFieldOutlineSprites()
        {
            foreach (var sprite in fieldOutlineSprites.Values) if (sprite != null) { var texture = sprite.texture; Destroy(sprite); Destroy(texture); }
            fieldOutlineSprites.Clear();
        }
    }
}

