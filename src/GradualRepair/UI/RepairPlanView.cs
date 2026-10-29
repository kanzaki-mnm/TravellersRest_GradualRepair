using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace GradualRepair
{
    // Submit is handled by the mod's existing single confirmation path. Letting
    // EventSystem submit these buttons too would double-handle the same pad press.
    public sealed class RepairMouseButton : Button
    { public override void OnSubmit(BaseEventData eventData) { } }

    public sealed class RepairHoverButton : Button
    {
        public Action Hover;
        public override void OnPointerEnter(PointerEventData data) { base.OnPointerEnter(data); if (IsInteractable()) Hover?.Invoke(); }
        public override void OnSubmit(BaseEventData data) { }
    }
    internal sealed class RepairPlanView
    {

        GameObject repairCanvas;
        RectTransform repairRoot;
        TMP_FontAsset repairFont;
        TMP_SpriteAsset repairCoinSprites;
        readonly List<Sprite> coinImages = new List<Sprite>();
        readonly Dictionary<string, Sprite> repairSprites = new Dictionary<string, Sprite>();
        readonly List<TextMeshProUGUI> ownedResources = new List<TextMeshProUGUI>();
        readonly List<Button> exteriorRows = new List<Button>();
        MoneyUI ownedCoinValues;
        TextMeshProUGUI repairTitle, repairNotice, repairAction, remainingLabel;
        Button repairPayButton;
        ScrollRect exteriorList;
        RectTransform exteriorListContent, repairCostView, repairGuides;
        TextMeshProUGUI[] compactCosts;
        TextMeshProUGUI compactCopper;
        Color compactNormal, compactInvalid;
        float compactScale, repairListNativePpu, fieldWidth;
        int[] walletResourceIds;
        int listedCompleted = -1;
        GameObject repairAcceptKey, repairCancelKey, repairAcceptPad, repairCancelPad;
        string[] names;
        RepairPlanData data;
        Action<int> hover, activate;
        Action close;
        bool modal;
        CanvasGroup planInput;
        readonly Dictionary<GameObject, bool> hiddenPanels = new Dictionary<GameObject, bool>();
        static readonly Color Ink = new Color(.24f, .12f, .07f), Cream = new Color(1f, .91f, .7f), Brown = new Color(.38f, .19f, .12f);
        const float NativeRepairUiScale = 800f / 450f;
        const float RepairListWidth = 310, RepairListViewportHeight = 364, RepairCostTop = 581;
        float RepairSidebarX => fieldWidth - 400;
        internal RectTransform Root => repairRoot;
        internal float Width => fieldWidth;
        internal bool IsCreated => repairCanvas != null;
        internal int[] WalletResourceIds => walletResourceIds ?? Array.Empty<int>();
        static RectTransform UiRect(string name, Transform parent, float x, float y, float width, float height)
            => RepairUiLayout.Rect(name, parent, x, y, width, height);
        static void SetCoins(MoneyUI native, int copper) => native.SetPrice(new Price { gold = copper / 10000, silver = copper / 100 % 100, copper = copper % 100 });
        internal void Show(string[] names, RepairPlanData data, Action<int> hover, Action<int> activate, Action close)
        {
            Hide(); this.names = (string[])names.Clone(); this.data = data; this.hover = hover; this.activate = activate; this.close = close;
            string[] wanted = { "Caja_Principal", "Ventana_Secundaria", "NewTitle", "NewTab_NotSelected", "NewUI_MisionSlot", "NewSlot", "SlotRed", "BgOptionsCharacter" };
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>()) if (wanted.Contains(sprite.name) && !repairSprites.ContainsKey(sprite.name)) repairSprites.Add(sprite.name, sprite);
            foreach (string name in wanted) UiSprite(name);
            var nativeTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            var japanese = nativeTexts.FirstOrDefault(t => t.isActiveAndEnabled && t.font != null && !string.IsNullOrEmpty(t.text) && t.text.Any(c => c >= '\u3040' && c <= '\u30ff'));
            repairFont = japanese == null ? Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == "NotoSansCJK-Bold") : japanese.font;
            if (repairFont == null) repairFont = nativeTexts.FirstOrDefault(t => t.font != null)?.font;
            if (repairFont == null) throw new InvalidOperationException("Native TMP font unavailable");
            repairCoinSprites = Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>().FirstOrDefault(a => a.spriteCharacterTable != null && new[] { "GoldCoin", "SilverCoin", "CopperCoin" }.All(n => a.spriteCharacterTable.Any(c => c.name == n)));
            if (repairCoinSprites == null) throw new InvalidOperationException("Native coin sprite asset unavailable");
            foreach (string name in new[] { "GoldCoin", "SilverCoin", "CopperCoin" })
            {
                var glyph = repairCoinSprites.spriteCharacterTable.First(c => c.name == name).glyph; var r = glyph.glyphRect;
                coinImages.Add(Sprite.Create((Texture2D)repairCoinSprites.spriteSheet, new Rect(r.x, r.y, r.width, r.height), new Vector2(.5f, .5f), 100));
            }
            repairCanvas = new GameObject("GradualRepair Plan UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = repairCanvas.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
            var scaler = repairCanvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 800); scaler.matchWidthOrHeight = 1;
            fieldWidth = 800f * Screen.width / Screen.height; repairRoot = UiRect("Repair Plan", repairCanvas.transform, 0, 0, fieldWidth, 800);
            repairRoot.anchorMin = repairRoot.anchorMax = new Vector2(.5f, .5f); repairRoot.anchoredPosition = new Vector2(-fieldWidth / 2, 400);
            CreateRepairWallet(); CreateExteriorList();
            CreateCompactRepairCosts(); CreateRepairGuides();
            repairNotice = UiText("Notice", repairRoot, 20, 705, Mathf.Max(200, fieldWidth - 440), 44, "", 17, Cream);



            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Refresh(data);
        }

        internal void Refresh(RepairPlanData data)
        {
            if (!IsCreated) return;
            this.data = data;
            RefreshExteriorList();
            for (int i = 0; i < ownedResources.Count; i++) ownedResources[i].text = data.Owned(walletResourceIds[i]).ToString();
            SetCoins(ownedCoinValues, data.Money);
            repairTitle.text = data.AllDone ? RepairText.Get("repair.exterior-complete") : data.Title;
            bool affordable = RefreshCompactRepairCosts(data.AllDone);
            repairAction.text = RepairText.Get("action.confirm-pay");
            repairPayButton.interactable = data.CanPay && affordable;
            repairNotice.text = data.Notice;
            UpdateRepairGuideDevice();
        }
        internal void EnsureVisible() => EnsureRepairRowVisible();
        internal void SetModal(bool active)
        {
            modal = active;
            if (active)
            {
                if (repairRoot == null) return;
                foreach (var name in new[] { "Construction Wallet Host", "Repair Cost Host", "Repair Guide Host" })
                { var node = repairRoot.Find(name)?.gameObject; if (node == null || hiddenPanels.ContainsKey(node)) continue; hiddenPanels.Add(node, node.activeSelf); node.SetActive(false); }
                planInput = repairCanvas.GetComponent<CanvasGroup>() ?? repairCanvas.AddComponent<CanvasGroup>();
                planInput.interactable = false; planInput.blocksRaycasts = false;
            }
            else
            {
                foreach (var entry in hiddenPanels) if (entry.Key != null) entry.Key.SetActive(entry.Value);
                hiddenPanels.Clear();
                if (planInput != null) { planInput.interactable = true; planInput.blocksRaycasts = true; }
                planInput = null;
            }
        }
        internal void Hide()
        {
            SetModal(false);
            if (repairCanvas != null) { repairCanvas.SetActive(false); Destroy(repairCanvas); }
            repairCanvas = null; repairRoot = null;
            foreach (var sprite in coinImages) Destroy(sprite); coinImages.Clear();
            repairSprites.Clear(); exteriorRows.Clear(); ownedResources.Clear();
            repairCostView = null; compactCosts = null; compactCopper = null;
            exteriorList = null; exteriorListContent = null; repairGuides = null;
            walletResourceIds = null; data = null; names = null; hover = null; activate = null; close = null;
        }
        Sprite UiSprite(string name)
        { if (!repairSprites.TryGetValue(name, out var sprite) || sprite == null) throw new InvalidOperationException("Missing native UI sprite: " + name); return sprite; }
        Image UiImage(string name, Transform parent, float x, float y, float w, float h, string sprite)
        {
            var image = UiRect(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.sprite = UiSprite(sprite); image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = .5f; return image;
        }
        TextMeshProUGUI UiText(string name, Transform parent, float x, float y, float w, float h, string text, int size = 24, Color? color = null)
        {
            var label = UiRect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>(); label.font = repairFont; label.spriteAsset = repairCoinSprites; label.fontSize = size; label.color = color ?? Ink;
            label.text = text; label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft; label.enableWordWrapping = true; return label;
        }
        void CreateRepairWallet()
        {
            var source = Resources.FindObjectsOfTypeAll<FarmConstructionUI>().Select(v => v.playerInfo).FirstOrDefault(v => v != null);
            if (source == null) throw new InvalidOperationException("Native farm construction player-info UI unavailable");
            var original = (RectTransform)source.transform;
            string[] materialFields = { "playerPlanksTextMesh", "playerNailsTextMesh", "playerStonesTextMesh", "playerMortarTextMesh" };
            string[] items = { "planks", "nails", "stones", "mortar" };
            var paths = materialFields.Select(n => CostPath(original, ((TextMeshProUGUI)AccessTools.Field(typeof(ConstructionPlayerInfo), n).GetValue(source)).transform)).ToArray();
            walletResourceIds = items.Select(n => (int)AccessTools.Field(typeof(Item), "id").GetValue(AccessTools.Field(typeof(ConstructionPlayerInfo), n).GetValue(source))).ToArray();
            var sourceMoney = source.GetComponentInChildren<PlayerMoneyUI>(true);
            if (sourceMoney == null) throw new InvalidOperationException("Native construction money display unavailable");
            string[] moneyFields = { "goldText", "silverText", "copperText" };
            var moneyPaths = moneyFields.Select(n => CostPath(original, ((TextMeshProUGUI)AccessTools.Field(typeof(MoneyUI), n).GetValue(sourceMoney)).transform)).ToArray();
            var host = UiRect("Construction Wallet Host", repairRoot, fieldWidth / 2, 8, 1, 1); host.gameObject.SetActive(false);
            var bar = Instantiate(original, host, false); bar.name = "Native Construction Wallet";
            // Strip controllers before activation: no balance subscriptions, material
            // consumption, localization callbacks or construction state on this clone.
            foreach (var behaviour in bar.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is Graphic) && !(behaviour is LayoutGroup) && !(behaviour is LayoutElement) && !(behaviour is ContentSizeFitter) && !(behaviour is Mask) && !(behaviour is RectMask2D)) DestroyImmediate(behaviour);
            var nativeScaler = original.GetComponentInParent<CanvasScaler>(true); var nativeCanvas = original.GetComponentInParent<Canvas>(true);
            MatchCostImageDensity(bar, nativeScaler != null ? nativeScaler.referencePixelsPerUnit : nativeCanvas.referencePixelsPerUnit, repairRoot.GetComponentInParent<Canvas>().referencePixelsPerUnit);
            bar.anchorMin = bar.anchorMax = new Vector2(0, 1); bar.pivot = new Vector2(.5f, 1); bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(458, 37); bar.localScale = Vector3.one * (800f / 450f);
            ownedResources.Clear(); foreach (var path in paths) ownedResources.Add(bar.Find(path).GetComponent<TextMeshProUGUI>());
            ownedCoinValues = bar.Find("PlayerMoney").gameObject.AddComponent<MoneyUI>();
            for (int i = 0; i < 3; i++) AccessTools.Field(typeof(MoneyUI), moneyFields[i]).SetValue(ownedCoinValues, bar.Find(moneyPaths[i]).GetComponent<TextMeshProUGUI>());
            foreach (var graphic in bar.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            bar.gameObject.SetActive(true); host.gameObject.SetActive(true);
        }
        static string CostPath(Transform root, Transform child)
        {
            if (child == null || !child.IsChildOf(root)) throw new InvalidOperationException("Native construction cost reference is external");
            string path = child.name; while (child.parent != root) { child = child.parent; path = child.name + "/" + path; }
            return path;
        }
        void CreateCompactRepairCosts()
        {
            var source = Resources.FindObjectsOfTypeAll<FarmConstructionActionInfoUI>().FirstOrDefault(v => v.titleText != null && v.planksCost != null);
            if (source == null) throw new InvalidOperationException("Native farm construction cost UI unavailable");
            var original = (RectTransform)AccessTools.Field(typeof(FarmConstructionActionInfoUI), "contentRectTransform").GetValue(source);
            var fields = new[] { source.planksCost, source.nailsCost, source.stonesCost, source.mortarCost, source.goldCost, source.silverCost };
            var paths = fields.Select(v => CostPath(original, v.transform)).ToArray(); var titlePath = CostPath(original, source.titleText.transform);
            // Clone only the presentation subtree, never the live UIWindow/controller.
            var holder = UiRect("Repair Cost Host", repairRoot, RepairSidebarX, RepairCostTop, 380, 220); holder.gameObject.SetActive(false);
            repairCostView = Instantiate(original, holder, false); repairCostView.name = "Native Construction Costs";
            // Sliced borders are measured using the receiving Canvas' reference PPU.
            // Native UI uses 48, while our canvas uses 100; retaining only the Image
            // multiplier would more than double the border and ornament pixels.
            var nativeScaler = original.GetComponentInParent<CanvasScaler>(true);
            var nativeCanvas = original.GetComponentInParent<Canvas>(true);
            float sourcePpu = nativeScaler != null ? nativeScaler.referencePixelsPerUnit : nativeCanvas.referencePixelsPerUnit;
            float targetPpu = repairRoot.GetComponentInParent<Canvas>().referencePixelsPerUnit;
            MatchCostImageDensity(repairCostView, sourcePpu, targetPpu);
            foreach (var behaviour in repairCostView.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is Graphic) && !(behaviour is LayoutGroup) && !(behaviour is LayoutElement) && !(behaviour is ContentSizeFitter) && !(behaviour is Mask) && !(behaviour is RectMask2D)) { behaviour.enabled = false; Destroy(behaviour); }
            foreach (Transform child in repairCostView) child.gameObject.SetActive(child.name == "Background" || child.name == "Title" || child.name == "ActionCosts" || child.name == "Adorno" || child.name == "Separacion" && child.GetSiblingIndex() == 2);
            compactCosts = paths.Select(p => repairCostView.Find(p).GetComponent<TextMeshProUGUI>()).ToArray();
            repairTitle = repairCostView.Find(titlePath).GetComponent<TextMeshProUGUI>(); repairTitle.font = repairFont;
            repairTitle.enableWordWrapping = false; repairTitle.enableAutoSizing = true; repairTitle.fontSizeMax = repairTitle.fontSize; repairTitle.fontSizeMin = repairTitle.fontSize * .65f;
            compactNormal = (Color)AccessTools.Field(typeof(FarmConstructionActionInfoUI), "normalColor").GetValue(source);
            compactInvalid = (Color)AccessTools.Field(typeof(FarmConstructionActionInfoUI), "invalidColor").GetValue(source);
            // Native parent scaling is absent from the cloned subtree. Use our 800-high UI coordinates.
            compactScale = 800f / 450f;
            repairCostView.anchorMin = repairCostView.anchorMax = new Vector2(.5f, 1); repairCostView.pivot = new Vector2(.5f, 1); repairCostView.anchoredPosition = Vector2.zero; repairCostView.sizeDelta = new Vector2(150, 64); repairCostView.localScale = Vector3.one * compactScale;
            var silver = compactCosts[5].transform.parent; var copper = Instantiate(silver, silver.parent, false); copper.name = "CopperCost";
            compactCopper = copper.GetComponentInChildren<TextMeshProUGUI>(true); copper.GetComponentInChildren<Image>(true).sprite = coinImages[2]; copper.gameObject.SetActive(false);
            foreach (var graphic in repairCostView.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            repairCostView.gameObject.SetActive(true); holder.gameObject.SetActive(true);

        }
        static void MatchCostImageDensity(RectTransform view, float sourcePpu, float targetPpu)
        {
            if (sourcePpu <= 0 || targetPpu <= 0) throw new InvalidOperationException("Invalid native cost UI pixel density");
            foreach (var image in view.GetComponentsInChildren<Image>(true))
                if (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled) image.pixelsPerUnitMultiplier *= targetPpu / sourcePpu;
        }
        Button CreateNativeRepairRow(int index, Image template)
        {
            var image = UiRect("Repair Row " + index, exteriorListContent, 0, index * 46, RepairListWidth, 42).gameObject.AddComponent<Image>();
            image.sprite = template.sprite; image.type = template.type; image.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier * repairRoot.GetComponentInParent<Canvas>().referencePixelsPerUnit / repairListNativePpu / NativeRepairUiScale;
            image.material = template.material; image.fillCenter = template.fillCenter; image.preserveAspect = template.preserveAspect; image.color = template.color;
            var button = image.gameObject.AddComponent<RepairHoverButton>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.Hover = () => { hover(index); };
            button.onClick.AddListener(() => { if (modal) return; activate(index); });
            var label = UiText("Repair Name", image.transform, 12, 3, RepairListWidth - 24, 36, names[index], 19, Ink);
            label.enableWordWrapping = false; label.enableAutoSizing = true; label.fontSizeMin = 15; label.fontSizeMax = 19;
            return button;
        }
        Image NativeRepairRowTemplate()
        {
            // bodySprite is the sender portrait, not the row background.
            foreach (var element in Resources.FindObjectsOfTypeAll<PostboxElementUI>())
            {
                var background = MailboxRowBackground(element.gameObject); if (background != null) return background;
            }
            foreach (var ui in Resources.FindObjectsOfTypeAll<PostboxUI>())
            {
                var prefab = HarmonyLib.AccessTools.Field(typeof(PostboxUI), "elementPrefab").GetValue(ui) as GameObject;
                var background = MailboxRowBackground(prefab); if (background != null) return background;
            }
            throw new InvalidOperationException("Native mailbox row background unavailable");
        }
        static Image MailboxRowBackground(GameObject row)
        {
            var background = row == null ? null : row.transform.Find("NewPanelNotFilled/Background/Corners")?.GetComponent<Image>();
            if (background == null || background.sprite == null || background.sprite.name != "VentanaListaCrafting" || background.type != Image.Type.Sliced || background.sprite.border == Vector4.zero) return null;
            return background;
        }
        void CreateExteriorList()
        {
            var panel = CreateNativeListFrame();
            remainingLabel = UiText("Completion", panel.transform, 18, 18, 330, 90, "", 20); remainingLabel.gameObject.SetActive(false);
            var viewport = UiRect("Viewport", panel.transform, 28, 28, RepairListWidth, RepairListViewportHeight); viewport.gameObject.AddComponent<RectMask2D>();
            exteriorList = panel.gameObject.AddComponent<ScrollRect>(); exteriorList.viewport = viewport; exteriorList.horizontal = false; exteriorList.vertical = true; exteriorList.inertia = false; exteriorList.movementType = ScrollRect.MovementType.Clamped; exteriorList.scrollSensitivity = 35;
            exteriorListContent = UiRect("Rows", viewport, 0, 0, RepairListWidth, 0); exteriorList.content = exteriorListContent;
            exteriorRows.Clear(); listedCompleted = -1;
            var template = NativeRepairRowTemplate();
            for (int i = 0; i < 13; i++) exteriorRows.Add(CreateNativeRepairRow(i, template));
            var rail = UiRect("Scroll Rail", panel.transform, 350, 28, 8, RepairListViewportHeight); var railImage = rail.gameObject.AddComponent<Image>(); railImage.color = Brown;
            var handle = UiRect("Scroll Handle", rail, 0, 0, 8, 35).gameObject.AddComponent<Image>(); handle.color = Cream;
            var handleRect = (RectTransform)handle.transform; handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one; handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
            var scrollbar = rail.gameObject.AddComponent<Scrollbar>(); scrollbar.targetGraphic = handle; scrollbar.handleRect = (RectTransform)handle.transform; scrollbar.direction = Scrollbar.Direction.BottomToTop; exteriorList.verticalScrollbar = scrollbar;
        }
        void RefreshExteriorList()
        {
            if (exteriorList == null) return;
            var remaining = data.Remaining;
            int row = 0;
            for (int i = 0; i < 13; i++) { bool visible = remaining.Contains(i); exteriorRows[i].gameObject.SetActive(visible); if (!visible) continue; ((RectTransform)exteriorRows[i].transform).anchoredPosition = new Vector2(0, -46 * row++); exteriorRows[i].targetGraphic.color = i == data.Selected ? new Color(1, .84f, .43f) : Color.white; }
            exteriorListContent.sizeDelta = new Vector2(RepairListWidth, Mathf.Max(RepairListViewportHeight, row * 46));
            remainingLabel.gameObject.SetActive(row == 0); remainingLabel.text = row == 0 ? RepairText.Get("repair.all-exterior-complete") : "";
            if (listedCompleted != data.CompletedCount) { listedCompleted = data.CompletedCount; EnsureRepairRowVisible(); }
        }
        void EnsureRepairRowVisible()
        {
            if (exteriorList == null) return; var remaining = data.Remaining; int row = Array.IndexOf(remaining, data.Selected); if (row < 0) return;
            float top = exteriorListContent.anchoredPosition.y, target = row * 46;
            if (target < top) top = target; else if (target + 42 > top + RepairListViewportHeight) top = target + 42 - RepairListViewportHeight;
            exteriorListContent.anchoredPosition = new Vector2(0, Mathf.Clamp(top, 0, Mathf.Max(0, exteriorListContent.rect.height - RepairListViewportHeight)));
        }
        RectTransform CreateNativeListFrame()
        {
            var source = Resources.FindObjectsOfTypeAll<PostboxUI>().Select(u => u.transform.Find("PostboxUIContent")).FirstOrDefault(t => t != null && t.Find("Background/Corners") != null && t.Find("NewTitleTab/Background") != null);
            if (source == null) throw new InvalidOperationException("Native mailbox frame unavailable");
            var host = UiRect("Remaining Repairs", repairRoot, RepairSidebarX, 146, 380, 420); host.gameObject.SetActive(false);
            var original = (RectTransform)source.Find("Background"); var frame = Instantiate(original, host, false); frame.name = "Native Mailbox Frame";
            var sold = frame.Find("SoldOut"); if (sold != null) DestroyImmediate(sold.gameObject);
            foreach (var behaviour in frame.GetComponentsInChildren<MonoBehaviour>(true)) if (!(behaviour is Graphic)) DestroyImmediate(behaviour);
            frame.anchorMin = frame.anchorMax = new Vector2(0, 1); frame.pivot = new Vector2(0, 1); frame.anchoredPosition = Vector2.zero; frame.sizeDelta = new Vector2(380 / NativeRepairUiScale, 420 / NativeRepairUiScale); frame.localScale = Vector3.one * NativeRepairUiScale;
            var scaler = source.GetComponentInParent<CanvasScaler>(true); float nativePpu = scaler.referencePixelsPerUnit, targetPpu = repairRoot.GetComponentInParent<Canvas>().referencePixelsPerUnit;
            repairListNativePpu = nativePpu; MatchCostImageDensity(frame, nativePpu, targetPpu);
            foreach (var image in frame.GetComponentsInChildren<Graphic>(true)) image.raycastTarget = false;
            frame.gameObject.SetActive(true);
            var template = source.Find("NewTitleTab/Background").GetComponent<Image>();
            // Native tab extends 35 units above the frame and overlaps its rim by 5.
            var tab = UiRect("Repair Tab", host, 380 - (30 + 140) * NativeRepairUiScale, -35 * NativeRepairUiScale, 140, 40); tab.localScale = Vector3.one * NativeRepairUiScale;
            var imageTab = tab.gameObject.AddComponent<Image>(); imageTab.sprite = template.sprite; imageTab.type = template.type; imageTab.color = template.color; imageTab.material = template.material; imageTab.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier; imageTab.raycastTarget = false; MatchCostImageDensity(tab, nativePpu, targetPpu);
            var tools = Resources.FindObjectsOfTypeAll<FarmConstructionUI>().SelectMany(u => u.GetComponentsInChildren<Image>(true)).FirstOrDefault(i => i.name == "Icon" && i.transform.parent.name == "Modifications" && i.transform.parent.parent.name == "Tabs");
            if (tools == null || tools.sprite == null) throw new InvalidOperationException("Native farm modification icon unavailable");
            var icon = UiRect("Repair Tools Icon", tab, 11, 12, 16, 16).gameObject.AddComponent<Image>(); icon.sprite = tools.sprite; icon.preserveAspect = true; icon.raycastTarget = false;
            var label = UiText("Repair Tab Label", tab, 34, 7, 100, 26, RepairText.Get("repair.tab"), 15); label.alignment = TextAlignmentOptions.MidlineLeft; label.enableWordWrapping = false;
            host.gameObject.SetActive(true); return host;
        }
        void CreateRepairGuides()
        {
            var shop = Resources.FindObjectsOfTypeAll<ShopUI>().FirstOrDefault(s => s.transform.Find("ContentShopUI/CancelAcceptButtons") != null);
            var original = shop == null ? null : shop.transform.Find("ContentShopUI/CancelAcceptButtons") as RectTransform;
            if (original == null) throw new InvalidOperationException("Native shop accept/cancel guides unavailable");
            var host = UiRect("Repair Guide Host", repairRoot, fieldWidth - 20, 700, 1, 1); host.gameObject.SetActive(false);
            repairGuides = Instantiate(original, host, false); repairGuides.name = "Native Repair Guides";
            foreach (var behaviour in repairGuides.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is Graphic)) DestroyImmediate(behaviour);
            var scaler = original.GetComponentInParent<CanvasScaler>(true);
            MatchCostImageDensity(repairGuides, scaler.referencePixelsPerUnit, repairRoot.GetComponentInParent<Canvas>().referencePixelsPerUnit);
            repairGuides.anchorMin = repairGuides.anchorMax = new Vector2(0, 1); repairGuides.pivot = new Vector2(1, 1); repairGuides.anchoredPosition = Vector2.zero; repairGuides.sizeDelta = new Vector2(145, 39); repairGuides.localScale = Vector3.one * (800f / 450f);
            var accept = ConfigureRepairGuide("AcceptButton", 0, RepairText.Get("action.confirm-pay"), () => activate(data.Selected));
            repairPayButton = accept; repairAction = accept.GetComponentInChildren<TextMeshProUGUI>(true);
            var cancel = ConfigureRepairGuide("CancelButton", 21, RepairText.Get("action.close"), close);
            repairGuides.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(((RectTransform)accept.transform).rect.width, ((RectTransform)cancel.transform).rect.width));
            repairAcceptKey = repairGuides.Find("AcceptButton/Key").gameObject; repairCancelKey = repairGuides.Find("CancelButton/Key2").gameObject;
            repairAcceptPad = repairGuides.Find("A").gameObject; repairCancelPad = repairGuides.Find("B").gameObject;
            SetupRepairPad(repairAcceptPad, ActionType.UIInteract, 0); SetupRepairPad(repairCancelPad, ActionType.UIBack, 21);
            repairGuides.gameObject.SetActive(true); host.gameObject.SetActive(true); UpdateRepairGuideDevice();
        }
        Button ConfigureRepairGuide(string name, float y, string caption, Action action)
        {
            var rect = (RectTransform)repairGuides.Find(name); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(0, -y); rect.sizeDelta = new Vector2(145, 18);
            var label = rect.GetComponentInChildren<TextMeshProUGUI>(true); label.text = caption; label.enableWordWrapping = false; label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
            var text = label.rectTransform; text.anchorMin = text.anchorMax = new Vector2(0, 1); text.pivot = new Vector2(0, 1); text.anchoredPosition = new Vector2(23, 0); text.sizeDelta = new Vector2(120, 18);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Ceil(label.GetPreferredValues(caption).x + 31));
            var key = (RectTransform)rect.Find(name == "AcceptButton" ? "Key" : "Key2"); key.anchorMin = key.anchorMax = new Vector2(0, 1); key.pivot = new Vector2(0, 1); key.anchoredPosition = new Vector2(1, 0);
            var button = rect.gameObject.AddComponent<RepairMouseButton>(); button.targetGraphic = rect.GetComponent<Image>(); button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(() => action());
            foreach (var image in rect.GetComponentsInChildren<Image>(true)) image.raycastTarget = image.transform == rect;
            rect.gameObject.SetActive(true); return button;
        }
        void SetupRepairPad(GameObject node, ActionType action, float y)
        {
            var rect = (RectTransform)node.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(1, -y); var image = node.GetComponent<Image>(); image.raycastTarget = false;
            // GamepadSprite.SetNativeSize uses texture pixels, which are much larger
            // than this cloned guide's canvas units. Refresh the sprite ourselves.
            image.sprite = ButtonsContext.GetPlayer(1).GetSpriteWithActionType(action);
            image.preserveAspect = true; rect.sizeDelta = new Vector2(18, 18); rect.localScale = Vector3.one;
        }
        void UpdateRepairGuideDevice()
        {
            if (repairGuides == null) return; bool pad = data.Gamepad;
            repairAcceptKey.SetActive(!pad); repairCancelKey.SetActive(!pad); repairAcceptPad.SetActive(pad && repairPayButton.gameObject.activeSelf); repairCancelPad.SetActive(pad);
            if (pad)
            {
                repairAcceptPad.GetComponent<Image>().sprite = ButtonsContext.GetPlayer(1).GetSpriteWithActionType(ActionType.UIInteract);
                repairCancelPad.GetComponent<Image>().sprite = ButtonsContext.GetPlayer(1).GetSpriteWithActionType(ActionType.UIBack);
            }
        }
        bool RefreshCompactRepairCosts(bool complete)
        {
            var needed = data.Required; int fee = data.Fee; bool moneyEnough = data.Money >= fee;
            int[] values = { needed[0], needed[1], needed[2], needed[3], fee / 10000, fee / 100 % 100 };
            int[] owned = { data.Owned(1036), data.Owned(1045), data.Owned(1050), data.Owned(1052) }; bool affordable = moneyEnough;
            for (int i = 0; i < compactCosts.Length; i++) { bool enough = i < 4 ? owned[i] >= values[i] : moneyEnough || values[i] == 0; compactCosts[i].text = values[i].ToString(); compactCosts[i].color = enough ? compactNormal : compactInvalid; if (i < 4) affordable &= enough; }
            bool copper = fee % 100 != 0; compactCopper.transform.parent.gameObject.SetActive(copper); compactCopper.text = (fee % 100).ToString(); compactCopper.color = moneyEnough ? compactNormal : compactInvalid;
            repairCostView.sizeDelta = new Vector2(150, copper ? 78 : 64); var costs = compactCosts[0].transform.parent.parent as RectTransform; costs.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, copper ? 42 : 28);

            if (repairGuides != null) ((RectTransform)repairGuides.parent).anchoredPosition = new Vector2(fieldWidth - 20, -Mathf.Max(700, RepairCostTop + (copper ? 78 : 64) * compactScale + 8));
            costs.gameObject.SetActive(!complete); repairPayButton.gameObject.SetActive(!complete); return affordable;
        }
    }
}
