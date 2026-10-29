using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace GradualRepair
{
    internal static class RepairUiLayout
    {
        internal static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
    }

    internal sealed class RepairConfirmationView
    {
        readonly MonoBehaviour host;
        readonly Action<string, object> log;
        Func<RepairConfirmationData> readData;
        Func<bool> isCurrent;
        Action accepted, cancelled;
        Action<Exception> error;
        RepairConfirmationData data;

        internal RepairConfirmationView(MonoBehaviour host, Action<string, object> log)
        { this.host = host; this.log = log; }
        internal bool HasWindow => onsiteDialog != null;
        internal bool IsOpen => onsiteDialog != null && onsiteDialog.IsOpen();
        void Log(string kind, object value) => log(kind, value);
        void OnAccepted() { if (IsOpen && isCurrent != null && isCurrent()) accepted?.Invoke(); }
        void OnCancelled() { if (HasWindow && isCurrent != null && isCurrent()) cancelled?.Invoke(); }
        internal void StopWarmup()
        { if (onsiteWarmup != null) host.StopCoroutine(onsiteWarmup); onsiteWarmup = null; }
        internal void Release() { StopWarmup(); Hide(); }
        static void RaiseRepairDialog(GameObject node, int order)
        {
            if (node == null) return;
            var canvas = node.GetComponent<Canvas>() ?? node.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = order;
            if (node.GetComponent<GraphicRaycaster>() == null) node.AddComponent<GraphicRaycaster>();
        }

        System.Diagnostics.Stopwatch onsiteOpenClock;
        YesNoDialogueUI onsiteDialog;
        GameObject onsiteBackdrop;
        Coroutine onsiteDialogRoutine;
        internal void Show(Func<RepairConfirmationData> readData, Func<bool> isCurrent, Action accepted, Action cancelled, Action<Exception> error, bool overlay)
        {
            Hide();
            this.readData = readData; this.isCurrent = isCurrent; this.accepted = accepted; this.cancelled = cancelled; this.error = error;
            data = readData();
            onsiteOpenClock = System.Diagnostics.Stopwatch.StartNew();
            var template = GetOnsiteTemplate();
            var parent = template.transform.parent; while (parent != null && !parent.gameObject.activeInHierarchy) parent = parent.parent;
            onsiteDialog = Instantiate(template, parent, false); onsiteDialog.name = "GradualRepair Confirmation";
            onsiteDialog.transform.SetAsLastSibling();
            // Never borrow a live native window, its listeners, or an inventory slot.
            if (!onsiteDialog.content.transform.IsChildOf(onsiteDialog.transform) || !onsiteDialog.boxText.transform.IsChildOf(onsiteDialog.transform) || !onsiteDialog.yesButton.transform.IsChildOf(onsiteDialog.transform) || !onsiteDialog.noButton.transform.IsChildOf(onsiteDialog.transform) || onsiteDialog.slotUI != null && !onsiteDialog.slotUI.transform.IsChildOf(onsiteDialog.transform)) throw new InvalidOperationException("Confirmation template contains external controls");
            if (onsiteDialog.blackBg != null && !onsiteDialog.blackBg.transform.IsChildOf(onsiteDialog.transform))
            {
                onsiteBackdrop = Instantiate(onsiteDialog.blackBg, onsiteDialog.transform.parent, false); onsiteBackdrop.name = "GradualRepair Confirmation Background"; onsiteBackdrop.transform.SetSiblingIndex(onsiteDialog.transform.GetSiblingIndex()); onsiteDialog.transform.SetAsLastSibling(); onsiteDialog.blackBg = onsiteBackdrop;
            }
            onsiteDialog.yesButton.onClick = new Button.ButtonClickedEvent(); onsiteDialog.noButton.onClick = new Button.ButtonClickedEvent();
            onsiteDialog.AddYesAction(OnAccepted); onsiteDialog.AddNoAction(OnCancelled);
            onsiteDialog.OnUIClose += OnNativeRepairDialogClosed;
            if (overlay) { RaiseRepairDialog(onsiteBackdrop, 2100); RaiseRepairDialog(onsiteDialog.gameObject, 2101); }
            onsiteDialog.gameObject.SetActive(true); onsiteDialog.enabled = true;
            onsiteDialogRoutine = host.StartCoroutine(OpenNativeRepairDialog());
        }
        IEnumerator OpenNativeRepairDialog()
        {
            // UIWindow.Start initializes the clone as closed. Open afterwards.
            yield return null; onsiteDialogRoutine = null;
            if (!isCurrent() || onsiteDialog == null) yield break;
            try
            {
                onsiteDialog.yesButton.onClick = new Button.ButtonClickedEvent(); onsiteDialog.noButton.onClick = new Button.ButtonClickedEvent(); onsiteDialog.AddYesAction(OnAccepted); onsiteDialog.AddNoAction(OnCancelled);
                onsiteDialog.Open(1); onsiteDialog.SetYesNoButtonsText();
                onsiteDialog.boxText.spriteAsset = GetOnsiteCoins();
                onsiteDialog.boxText.fontSizeMax = onsiteDialog.boxText.fontSize; onsiteDialog.boxText.enableAutoSizing = true; onsiteDialog.boxText.fontSizeMin = Mathf.Min(18, onsiteDialog.boxText.fontSizeMax * .85f);
                BuildOnsiteDialogVisuals(); Refresh(); Log("onsite_open_timing", new { ms = onsiteOpenClock.Elapsed.TotalMilliseconds });
            }
            catch (Exception ex) { Log("onsite_dialog_error", ex.ToString()); error(ex); }
        }
        internal void Refresh()
        {
            if (onsiteDialog == null) return; data = readData(); var n = data.Required; int[] ids = data.MaterialIds;
            onsiteDialog.boxText.text = data.Question;
            int visible = n.Count(amount => amount > 0), column = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                bool needed = n[i] > 0; onsiteMaterialFrames[i].gameObject.SetActive(needed); if (!needed) continue;
                var rect = (RectTransform)onsiteMaterialFrames[i].transform; rect.anchoredPosition = new Vector2(onsiteVisualRoot.rect.width / 2 - visible * 44 / 2f + column++ * 44 + 3, -onsiteMaterialY);
                int own = data.Owned[i]; onsiteMaterialCounts[i].text = $"{own}/{n[i]}"; onsiteMaterialCounts[i].color = own >= n[i] ? Color.white : new Color(.72f, .12f, .08f); onsiteMaterialFrames[i].sprite = OnsiteSprite(own >= n[i] ? "NewSlot" : "SlotRed");
            }
            int fee = data.Fee; onsiteFee.SetPrice(new Price { gold = fee / 10000, silver = fee / 100 % 100, copper = fee % 100 }); bool affordable = data.CanAccept; onsiteDialog.yesButton.interactable = affordable;
            foreach (string field in new[] { "goldText", "silverText", "copperText" }) MoneyText(onsiteFee, field).color = data.Money >= fee ? MoneyText(onsiteMoney, field).color : new Color(.72f, .12f, .08f); onsiteMoney.SetPrice(CurrentMoneyPrice());
        }

        void OnNativeRepairDialogClosed(int player) { OnCancelled(); }
        internal void Hide()
        {
            if (onsiteDialogRoutine != null) host.StopCoroutine(onsiteDialogRoutine); onsiteDialogRoutine = null;
            var window = onsiteDialog; onsiteDialog = null;
            if (window != null) { window.OnUIClose -= OnNativeRepairDialogClosed; if (window.IsOpen()) window.CloseUI(); Destroy(window.gameObject); }
            if (onsiteBackdrop != null) Destroy(onsiteBackdrop); onsiteBackdrop = null;
            readData = null; isCurrent = null; accepted = null; cancelled = null; error = null; data = null;
            onsiteVisualRoot = null; onsiteFee = null; onsiteMoney = null; onsiteMaterialFrames.Clear(); onsiteMaterialCounts.Clear();
        }

        RectTransform onsiteVisualRoot;
        MoneyUI onsiteFee;
        MoneyUI onsiteMoney;
        float onsiteMaterialY;
        readonly List<Image> onsiteMaterialFrames = new List<Image>();
        readonly List<TextMeshProUGUI> onsiteMaterialCounts = new List<TextMeshProUGUI>();
        readonly Dictionary<string, Sprite> onsiteSprites = new Dictionary<string, Sprite>();
        Sprite OnsiteSprite(string name) { if (!onsiteSprites.TryGetValue(name, out var sprite) || sprite == null) { sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == name) ?? throw new InvalidOperationException("Missing native dialog sprite: " + name); onsiteSprites[name] = sprite; } return sprite; }
        Price CurrentMoneyPrice() { int copper = data.Money; return new Price { gold = copper / 10000, silver = copper / 100 % 100, copper = copper % 100 }; }
        void BuildOnsiteDialogVisuals()
        {
            var question = onsiteDialog.boxText; var original = question.rectTransform;
            var resizing = question.GetComponent<UIElementsResizing>(); if (resizing != null) resizing.enabled = false; question.autoSizeTextContainer = false;
            question.text = data.Question; const float width = 240; float questionHeight = Mathf.Max(22, Mathf.Ceil(question.GetPreferredValues(question.text, width, 0).y)); onsiteMaterialY = questionHeight + 4; float bodyHeight = questionHeight + 70;
            onsiteVisualRoot = new GameObject("GradualRepair Dialog Contents", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>(); onsiteVisualRoot.SetParent(original.parent, false); onsiteVisualRoot.anchorMin = onsiteVisualRoot.anchorMax = new Vector2(.5f, .5f); onsiteVisualRoot.pivot = new Vector2(.5f, .5f); onsiteVisualRoot.localPosition = original.localPosition; onsiteVisualRoot.localScale = original.localScale; onsiteVisualRoot.sizeDelta = new Vector2(width, bodyHeight);
            var layout = onsiteVisualRoot.GetComponent<LayoutElement>(); layout.minWidth = layout.preferredWidth = width; layout.minHeight = layout.preferredHeight = bodyHeight; layout.flexibleWidth = layout.flexibleHeight = 0;
            question.transform.SetParent(onsiteVisualRoot, false); original.anchorMin = original.anchorMax = new Vector2(.5f, 1); original.pivot = new Vector2(.5f, 1); original.anchoredPosition = Vector2.zero; original.sizeDelta = new Vector2(width, questionHeight); original.localScale = Vector3.one;
            var quantityTemplate = GetInventoryQuantity();
            var craftingQuantity = GetCraftingQuantity();
            int[] ids = data.MaterialIds;
            for (int i = 0; i < ids.Length; i++)
            {
                var frame = RepairUiLayout.Rect("Material " + ids[i], onsiteVisualRoot, 0, onsiteMaterialY, 38, 38).gameObject.AddComponent<Image>(); frame.sprite = OnsiteSprite("NewSlot"); frame.type = Image.Type.Sliced; frame.raycastTarget = false; onsiteMaterialFrames.Add(frame);
                var icon = RepairUiLayout.Rect("Item Icon", frame.transform, 3, 3, 32, 32).gameObject.AddComponent<Image>(); icon.sprite = ItemDatabaseAccessor.GetItem(ids[i]).icon; icon.preserveAspect = true; icon.raycastTarget = false;
                var count = Instantiate(craftingQuantity, frame.transform, false); count.name = "Owned and Required"; count.gameObject.SetActive(true); count.raycastTarget = false; count.fontSize = craftingQuantity.fontSize; count.fontSizeMax = craftingQuantity.fontSizeMax; count.fontSizeMin = Mathf.Min(craftingQuantity.fontSizeMin, count.fontSizeMax * .5f); count.enableAutoSizing = true; count.overflowMode = TextOverflowModes.Overflow; count.enableWordWrapping = false; count.alignment = TextAlignmentOptions.BottomRight;
                var rect = count.rectTransform; rect.anchorMin = craftingQuantity.rectTransform.anchorMin; rect.anchorMax = craftingQuantity.rectTransform.anchorMax; rect.pivot = craftingQuantity.rectTransform.pivot; rect.localScale = Vector3.one; rect.anchoredPosition = craftingQuantity.rectTransform.anchoredPosition; rect.sizeDelta = craftingQuantity.rectTransform.sizeDelta; onsiteMaterialCounts.Add(count);
            }
            var moneySource = NativeShopWallet();
            onsiteFee = CloneMoneyVisual(moneySource, onsiteVisualRoot, "GradualRepair Fee");
            var fee = (RectTransform)onsiteFee.transform; fee.anchorMin = fee.anchorMax = new Vector2(.5f, 1); fee.pivot = new Vector2(.5f, 1); fee.anchoredPosition = new Vector2(0, -(onsiteMaterialY + 44));
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)onsiteDialog.content.transform); Canvas.ForceUpdateCanvases(); BuildOnsiteWallet();
        }
        static TextMeshProUGUI MoneyText(MoneyUI money, string field) => NativeReferences.Field(typeof(MoneyUI), field).GetValue(money) as TextMeshProUGUI;
        static bool OwnMoneyTexts(MoneyUI money) => new[] { "goldText", "silverText", "copperText" }.All(f => MoneyText(money, f) != null && MoneyText(money, f).transform.IsChildOf(money.transform));
        static bool MoneyVisualOnly(MoneyUI money) => money.GetType() == typeof(MoneyUI) && money.GetComponentsInChildren<MonoBehaviour>(true).All(b => b is MoneyUI || b is Graphic || b is LayoutGroup || b is ContentSizeFitter || b is AspectRatioFitter || b is LayoutElement || b is Mask || b is RectMask2D);
        MoneyUI NativeShopWallet() => onsiteMoneySource != null ? onsiteMoneySource : onsiteMoneySource = Resources.FindObjectsOfTypeAll<MoneyUI>().FirstOrDefault(m => m.name == "PlayerMoney" && m.transform.parent.name == "MoneyPanel Variant" && m.transform.parent.parent.name == "OptionsPanel" && m.transform.parent.parent.parent.parent.name == "ShopUI" && OwnMoneyTexts(m)) ?? throw new InvalidOperationException("Native shop wallet unavailable");
        // Copy measured visuals only. Never instantiate PlayerMoneyUI or its subscriptions.
        RectTransform CloneVisual(RectTransform source, Transform parent)
        {
            var rect = new GameObject(source.name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax; rect.pivot = source.pivot; rect.sizeDelta = source.sizeDelta; rect.anchoredPosition = source.anchoredPosition; rect.localScale = source.localScale;
            var image = source.GetComponent<Image>(); if (image != null) { var copy = rect.gameObject.AddComponent<Image>(); copy.sprite = image.sprite; copy.type = image.type; copy.color = image.color; copy.preserveAspect = image.preserveAspect; copy.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier; copy.raycastTarget = false; }
            foreach (Transform child in source)
            {
                var text = child.GetComponent<TextMeshProUGUI>(); if (text != null) { var copy = Instantiate(text, rect, false); copy.name = text.name; copy.raycastTarget = false; copy.gameObject.SetActive(true); }
                else if (child is RectTransform childRect) CloneVisual(childRect, rect);
            }
            rect.gameObject.SetActive(source.gameObject.activeSelf); return rect;
        }
        MoneyUI CloneMoneyVisual(MoneyUI source, Transform parent, string name)
        {
            var rect = CloneVisual((RectTransform)source.transform, parent); rect.name = name; rect.gameObject.SetActive(true); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = ((RectTransform)source.transform).rect.size; rect.localScale = Vector3.one;
            var money = rect.gameObject.AddComponent<MoneyUI>(); money.initialColor = MoneyText(source, "goldText").color;
            foreach (string field in new[] { "goldText", "silverText", "copperText" })
            {
                var original = MoneyText(source, field); string path = original.name; for (var t = original.transform.parent; t != source.transform; t = t.parent) path = t.name + "/" + path;
                NativeReferences.Field(typeof(MoneyUI), field).SetValue(money, rect.Find(path).GetComponent<TextMeshProUGUI>());
            }
            return money;
        }
        void BuildOnsiteWallet()
        {
            var source = NativeShopWallet(); var sourcePanel = (RectTransform)source.transform.parent;
            var panel = CloneVisual(sourcePanel, onsiteDialog.content.transform); panel.name = "GradualRepair Wallet Frame";
            // Replace the visual-only copied row with a plain MoneyUI-backed row.
            var copiedRow = panel.Find(source.name); if (copiedRow != null) { copiedRow.gameObject.SetActive(false); Destroy(copiedRow.gameObject); }
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.pivot = new Vector2(0, 1); panel.sizeDelta = sourcePanel.rect.size; panel.localScale = Vector3.one; panel.gameObject.SetActive(true);
            var frame = onsiteDialog.boxText.GetComponentsInParent<Image>(true).FirstOrDefault(i => i.transform.IsChildOf(onsiteDialog.transform)); var anchor = frame == null ? onsiteVisualRoot : (RectTransform)frame.transform;
            panel.localPosition = panel.parent.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.xMin, anchor.rect.yMin - 4, 0)));
            onsiteMoney = CloneMoneyVisual(source, panel, "GradualRepair Wallet"); var rect = (RectTransform)onsiteMoney.transform; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = ((RectTransform)source.transform).anchoredPosition;
            onsiteMoney.SetPrice(CurrentMoneyPrice()); Log("onsite_wallet_bound", new { source = source.name, sourceSize = ((RectTransform)source.transform).rect.ToString(), frameSize = sourcePanel.rect.ToString(), scale = 1 });
        }

        YesNoDialogueUI onsiteTemplate;
        TextMeshProUGUI onsiteInventoryQuantity, onsiteCraftingQuantity;
        MoneyUI onsiteMoneySource;
        TMP_SpriteAsset onsiteCoinSprites;
        Coroutine onsiteWarmup;
        float nextOnsiteWarmup;
        bool OnsiteAssetsReady() => onsiteTemplate != null && onsiteInventoryQuantity != null && onsiteCraftingQuantity != null && onsiteMoneySource != null && onsiteCoinSprites != null && onsiteSprites.ContainsKey("NewSlot") && onsiteSprites.ContainsKey("SlotRed");
        internal void Warmup(bool canPrepare, bool otherWindowOpen)
        {
            if (onsiteWarmup != null || OnsiteAssetsReady() || !canPrepare || otherWindowOpen || Time.unscaledTime < nextOnsiteWarmup) return;
            nextOnsiteWarmup = Time.unscaledTime + 5; onsiteWarmup = host.StartCoroutine(WarmOnsiteAssets());
        }
        IEnumerator WarmOnsiteAssets()
        {
            // Prepare scene-owned references once during load/idle, in separate frames.
            // No periodic global search after preparation succeeds.
            yield return null;
            foreach (Action prepare in new Action[] { () => GetOnsiteTemplate(), () => GetInventoryQuantity(), () => GetCraftingQuantity(), () => NativeShopWallet(), () => GetOnsiteCoins(), () => WarmOnsiteSlotSprites() })
            {
                try { prepare(); } catch (Exception ex) { Log("onsite_assets_pending", ex.Message); }
                yield return null;
            }
            onsiteWarmup = null; Log("onsite_assets_ready", new { ready = OnsiteAssetsReady() });
        }
        YesNoDialogueUI GetOnsiteTemplate()
        {
            if (onsiteTemplate != null) return onsiteTemplate;
            var pause = Resources.FindObjectsOfTypeAll<PauseMenuUI>().FirstOrDefault(w => w.gameObject.scene.IsValid() && w.gameObject.scene.isLoaded && w._playerNum == 1);
            onsiteTemplate = pause == null ? null : NativeReferences.Field(typeof(PauseMenuUI), "yesNoDialogue").GetValue(pause) as YesNoDialogueUI;
            if (onsiteTemplate == null) onsiteTemplate = Resources.FindObjectsOfTypeAll<YesNoDialogueUI>().FirstOrDefault(w => w != null && w.gameObject.scene.IsValid() && w.gameObject.scene.isLoaded && w._playerNum == 1 && w.boxText != null && w.yesButton != null && w.noButton != null && !w.name.StartsWith("GradualRepair"));
            return onsiteTemplate ?? throw new InvalidOperationException("Native confirmation dialog unavailable");
        }
        TextMeshProUGUI GetInventoryQuantity()
        {
            if (onsiteInventoryQuantity != null) return onsiteInventoryQuantity;
            var slot = Resources.FindObjectsOfTypeAll<SlotUI>().FirstOrDefault(v => v.gameObject.scene.IsValid() && v.transform.parent.name == "Slots" && v.GetComponentsInParent<Transform>(true).Any(t => t.name == "MainInventoryUI") && v.transform.Find("StackText") != null);
            return onsiteInventoryQuantity = slot == null ? throw new InvalidOperationException("Native inventory slot unavailable") : slot.transform.Find("StackText").GetComponent<TextMeshProUGUI>();
        }
        TextMeshProUGUI GetCraftingQuantity()
        {
            if (onsiteCraftingQuantity != null) return onsiteCraftingQuantity;
            return onsiteCraftingQuantity = Resources.FindObjectsOfTypeAll<RecipeElementUI>().SelectMany(recipe => (SlotUIRecipe[])NativeReferences.Field(typeof(RecipeElementUI), "inputSlots").GetValue(recipe) ?? Array.Empty<SlotUIRecipe>()).Where(v => v != null && v.stackText != null && v.stackText.font != null).Select(v => v.stackText).FirstOrDefault() ?? throw new InvalidOperationException("Native crafting quantity unavailable");
        }
        TMP_SpriteAsset GetOnsiteCoins()
        {
            if (onsiteCoinSprites != null) return onsiteCoinSprites;
            return onsiteCoinSprites = Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>().FirstOrDefault(a => a.spriteCharacterTable != null && new[] { "GoldCoin", "SilverCoin", "CopperCoin" }.All(n => a.spriteCharacterTable.Any(c => c.name == n)));
        }
        void WarmOnsiteSlotSprites()
        {
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>(); foreach (string name in new[] { "NewSlot", "SlotRed" }) { var sprite = sprites.FirstOrDefault(s => s.name == name); if (sprite != null) onsiteSprites[name] = sprite; }
        }
    }
}
