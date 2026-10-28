using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        GameObject repairDust; Coroutine repairFeedback; Texture2D repairDustTexture; Sprite repairDustSprite;
        void PlayExteriorRepairFeedback(RepairJob job)
        {
            try { int index = Array.IndexOf(RepairModel.Jobs, job); if (index < 0 || !repairPreview.IsCreated || repairRoot == null) return; var area = FieldRect(index); StopExteriorRepairFeedback(); repairFeedback = StartCoroutine(ExteriorRepairFeedback(area)); }
            catch (Exception ex) { Logger.LogWarning("Repair feedback unavailable: " + ex.Message); }
        }
        IEnumerator ExteriorRepairFeedback(Rect area)
        {
            repairDust = UiRect("Repair Dust", repairRoot, area.center.x, area.center.y, 1, 1).gameObject;
            repairDust.transform.SetAsLastSibling();
            if (repairDustSprite == null) { repairDustTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false); repairDustTexture.filterMode = FilterMode.Point; var pixels = new Color32[64]; for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) pixels[y * 8 + x] = Mathf.Abs(x - 3.5f) + Mathf.Abs(y - 3.5f) <= 5 ? new Color32(230, 215, 186, 255) : new Color32(0, 0, 0, 0); repairDustTexture.SetPixels32(pixels); repairDustTexture.Apply(); repairDustSprite = Sprite.Create(repairDustTexture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 8); }
            var puffs = new Image[12]; var origins = new Vector2[12];
            for (int i = 0; i < puffs.Length; i++) { float x = ((i % 4) / 3f - .5f) * Mathf.Min(area.width, 190), y = (i / 4f - 1) * Mathf.Min(area.height * .22f, 20); origins[i] = new Vector2(x, -y); puffs[i] = UiRect("Dust " + i, repairDust.transform, x, y, 16, 16).gameObject.AddComponent<Image>(); puffs[i].sprite = repairDustSprite; puffs[i].raycastTarget = false; }
            float start = Time.unscaledTime; int hit = 0; var sound = Resources.FindObjectsOfTypeAll<Sound>().FirstOrDefault(s => s.gameObject.activeInHierarchy); var clips = sound == null ? null : sound.repairFurniture;
            while (open && Time.unscaledTime - start < .85f)
            {
                float elapsed = Time.unscaledTime - start, t = elapsed / .85f;
                if (hit < 3 && elapsed >= hit * .18f) { if (sound != null && clips != null && clips.Length > 0) { var clip = clips[hit % clips.Length]; if (clip != null) { try { sound.PlayOneShot(clip, true, null, null, .4f); } catch (Exception ex) { Logger.LogWarning("Repair sound unavailable: " + ex.Message); } } } hit++; }
                for (int i = 0; i < puffs.Length; i++) { var r = (RectTransform)puffs[i].transform; r.anchoredPosition = origins[i] + new Vector2((i % 2 == 0 ? -1 : 1) * t * 16, t * 33); r.sizeDelta = Vector2.one * (16 + Mathf.Floor(t * 3) * 4); puffs[i].color = new Color(1, 1, 1, (1 - t) * .8f); }
                yield return null;
            }
            if (repairDust != null) Destroy(repairDust); repairDust = null; repairFeedback = null;
        }
        void StopExteriorRepairFeedback() { if (repairFeedback != null) StopCoroutine(repairFeedback); repairFeedback = null; if (repairDust != null) Destroy(repairDust); repairDust = null; }
        void ReleaseExteriorRepairFeedback() { StopExteriorRepairFeedback(); if (repairDustSprite != null) Destroy(repairDustSprite); if (repairDustTexture != null) Destroy(repairDustTexture); repairDustSprite = null; repairDustTexture = null; }
    }
}


