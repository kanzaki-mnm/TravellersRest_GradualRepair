using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        GameObject mailboxCanvas;
        TextMeshProUGUI mailboxLabel;
        GameObject mailboxPadIcon;
        PostboxUI mailboxOwner;
        int mailboxOpenedFrame, mailboxRequestedFrame = -1;
        bool mailboxEntryFailed;

        bool MailboxTopLevel(PostboxUI ui) => ui != null && ui.IsOpen()
            && !PostboxPanelUI.instances.Any(p => p != null && p.IsOpen())
            && !PostboxDeleteConfirmationUI.instances.Any(p => p != null && p.IsOpen());

        void UpdateMailboxEntry()
        {
            CompleteMailboxTransition(Time.frameCount);
            if (!Active() || failed) { if (mailboxCanvas != null) mailboxCanvas.SetActive(false); mailboxOwner = null; return; }
            var ui = PostboxUI.Get(1);
            bool visible = !mailboxEntryFailed && !failed && !open && Active() && NearTavern() && MailboxTopLevel(ui);
            if (!visible) { if (mailboxCanvas != null) mailboxCanvas.SetActive(false); mailboxOwner = null; return; }
            try
            {
                if (mailboxCanvas == null) BuildMailboxEntry(ui);
                if (mailboxOwner != ui || !mailboxCanvas.activeSelf) { mailboxOpenedFrame = Time.frameCount; mailboxOwner = ui; }
                mailboxCanvas.SetActive(true);
                bool pad = PlayerInputs.IsGamepadActive(1);
                mailboxPadIcon.SetActive(pad);
                LayoutMailboxGuide(pad);
                if (Time.frameCount > mailboxOpenedFrame && ((!pad && Input.GetKeyDown(KeyCode.R)) || (pad && PlayerInputs.GetPlayer(1).GetButtonDown(ActionType.UINextCategory)))) RequestMailboxPlan();
            }
            catch (Exception ex)
            {
                mailboxEntryFailed = true; ReleaseMailboxEntry(); Logger.LogError(ex); Log("mailbox_entry_error", ex.ToString());
                // F8 remains available if this optional entry cannot be constructed.
            }
        }
        void LayoutMailboxGuide(bool pad)
        {
            string label = pad ? RepairText.Get("mailbox.repair-plan") : "[R]  " + RepairText.Get("mailbox.repair-plan");
            if (mailboxLabel.text == label) return;
            mailboxLabel.text = label;
            float left = pad ? 34 : 8;
            var text = mailboxLabel.rectTransform; text.offsetMin = new Vector2(left, 2); text.offsetMax = new Vector2(-8, -2);
            var rect = (RectTransform)mailboxLabel.transform.parent;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Ceil(mailboxLabel.GetPreferredValues(label).x + left + 8));
        }
        void CompleteMailboxTransition(int frame)
        {
            if (mailboxRequestedFrame < 0 || frame < mailboxRequestedFrame) return;
            mailboxRequestedFrame = -1;
            // CloseUI releases its selection synchronously. Build in the same frame
            // so no frame is rendered with both windows absent.
            if (!failed && Active() && CanOpen() && NearTavern()) { Open(); Log("mailbox_plan_open", new { }); }
        }
        void RequestMailboxPlan()
        {
            if (mailboxRequestedFrame >= 0 || mailboxOwner == null || !MailboxTopLevel(mailboxOwner) || !Active() || failed) return;
            mailboxOwner.CloseUI();
            mailboxRequestedFrame = Time.frameCount;
            mailboxCanvas.SetActive(false);
            CompleteMailboxTransition(Time.frameCount);
        }
        void BuildMailboxEntry(PostboxUI ui)
        {
            var backIcon = Resources.FindObjectsOfTypeAll<GamepadSprite>().FirstOrDefault(s => s.gameObject.scene.IsValid() && (ActionType)AccessTools.Field(typeof(GamepadSprite), "actionType").GetValue(s) == ActionType.UIBack && s.gameObject.activeInHierarchy);
            var backPlate = backIcon == null || backIcon.transform.parent == null ? null : backIcon.transform.parent.GetComponent<Image>();
            var source = ui.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.font != null) ?? throw new InvalidOperationException("Mailbox native font unavailable");
            mailboxCanvas = new GameObject("GradualRepair Mailbox Entry", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = mailboxCanvas.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2001;
            var scaler = mailboxCanvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 800); scaler.matchWidthOrHeight = 1;
            var rect = UiRect("Repair Plan Button", mailboxCanvas.transform, 0, 0, 300, 32);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(40, 24);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = backPlate == null ? null : backPlate.sprite; image.type = Image.Type.Sliced; image.color = backPlate == null ? new Color(.19f, .08f, .035f, .96f) : backPlate.color;
            var button = rect.gameObject.AddComponent<RepairMouseButton>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(RequestMailboxPlan);
            mailboxLabel = UiRect("Repair Plan Label", rect, 0, 0, 300, 32).gameObject.AddComponent<TextMeshProUGUI>(); mailboxLabel.font = source.font; mailboxLabel.fontSharedMaterial = source.fontSharedMaterial; mailboxLabel.raycastTarget = false; mailboxLabel.fontSize = 20; mailboxLabel.color = Color.white; mailboxLabel.alignment = TextAlignmentOptions.Center; mailboxLabel.enableWordWrapping = false;
            var text = mailboxLabel.rectTransform; text.anchorMin = Vector2.zero; text.anchorMax = Vector2.one; text.offsetMin = new Vector2(46, 2); text.offsetMax = new Vector2(-12, -2); text.localScale = Vector3.one;
            var icon = UiRect("Native Shortcut", rect, 4, 1, 30, 30).gameObject.AddComponent<Image>(); icon.raycastTarget = false; icon.preserveAspect = true; mailboxPadIcon = icon.gameObject;
            var binding = icon.gameObject.AddComponent<GamepadSprite>(); AccessTools.Field(typeof(GamepadSprite), "actionType").SetValue(binding, ActionType.UINextCategory); AccessTools.Field(typeof(GamepadSprite), "image").SetValue(binding, icon);
            Log("mailbox_entry_created", new { keyboard = "R", gamepad = "UINextCategory" });
        }
        void ReleaseMailboxEntry()
        { if (mailboxCanvas != null) Destroy(mailboxCanvas); mailboxCanvas = null; mailboxOwner = null; mailboxLabel = null; mailboxPadIcon = null; mailboxRequestedFrame = -1; }
    }
}
