using System;
using System.Linq;
using UnityEngine;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        readonly RepairPreviewView repairPreview = new RepairPreviewView();
        float fieldWidth = 1280;
        bool indoorPreview;
        Vector3 lastFieldMouse;
        void CreateFieldView()
        {
            var native = NativeGame.References.GetMainCamera(1);
            if (native == null || targets.Count < 13) throw new InvalidOperationException("Exterior camera or repair parts unavailable");
            var bounds = new Bounds[13];
            for (int i = 0; i < 13; i++)
            {
                if (!targets.TryGetValue(RepairModel.Jobs[i].Id, out var renderer) || renderer == null || !renderer.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Exterior part not visible: " + RepairModel.Jobs[i].Id);
                bounds[i] = renderer.bounds;
            }
            repairPreview.Create(native, bounds, repairRoot, fieldWidth);
            indoorPreview = false; SetInteriorPreview(selected >= 14);
            lastFieldMouse = Input.mousePosition; UpdateFieldHighlight();
        }
        void RenderFieldIfNeeded()
        {
            if (!open || !repairPreview.IsCreated) return;
            int revision = state.Completed.Count + state.CompletedInterior.Count * 100;
            if (!repairPreview.NeedsRender(revision)) return;
            SpriteRenderer target = null;
            if (selected >= 0 && selected < 13 && !state.Completed.Contains(RepairModel.Jobs[selected].Id)) targets.TryGetValue(RepairModel.Jobs[selected].Id, out target);
            var outline = target == null ? null : DecorationMode.GetPlayer(1)?.GetOutlineMaterial();
            repairPreview.Render(revision, target, outline);
        }
        Rect FieldRect(int index) => repairPreview.Project(index >= 14 ? IndoorBounds(index) : repairPreview.ExteriorBounds(index));
        void UpdateFieldHighlight() => repairPreview.Select(selected);
        void ReleaseFieldView() => repairPreview.Release();
        int PickField(Vector2 point)
        {
            if (indoorPreview) { int chosen = -1; float size = float.MaxValue; for (int i = 14; i < 14 + RepairModel.InteriorJobs.Length; i++) { if (state.CompletedInterior.Contains(RepairModel.InteriorJobs[i - 14].Id)) continue; var r = FieldRect(i); float area = r.width * r.height; if (r.Contains(point) && area < size) { chosen = i; size = area; } } return chosen; }
            // Small details win over their underlying roof/wall rectangles.
            var priority = RepairModel.Jobs.Select((job, index) => new { job, index }).OrderBy(p => RepairSelectionRules.Priority(p.job.Id));
            foreach (var candidate in priority)
            {
                int i = candidate.index; string id = candidate.job.Id;
                if (state.Completed.Contains(RepairModel.Jobs[i].Id)) continue;
                var r = FieldRect(i);
                if (RepairSelectionRules.IsDetail(id)) { r.xMin -= 4; r.xMax += 4; r.yMin -= 4; r.yMax += 4; }
                // Roof sprites include transparent corners. Restrict to the roof plane,
                // allowing wall selection below the eaves.
                if (RepairSelectionRules.IsRoof(id) && point.y > r.yMax - 18) continue;
                if (r.Contains(point)) return i;
            }
            return -1;
        }
        void UpdateFieldPointer()
        {
            if (!repairPreview.IsCreated) return;
            var mouse = Input.mousePosition; bool moved = (mouse - lastFieldMouse).sqrMagnitude > 1; lastFieldMouse = mouse;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(repairRoot, mouse, null, out var local);
            var point = new Vector2(local.x, -local.y);
            bool overPanel = point.x >= RepairSidebarX - 8 || point.y > 705 || point.y < 75;
            if (!overPanel && (moved || Input.GetMouseButtonDown(0)))
            {
                int pick = PickField(point); if (pick >= 0)
                {
                    if (pick != selected && !confirm) { SelectRepairJob(pick); UpdateFieldHighlight(); }
                    if (Input.GetMouseButtonDown(0)) { if (pick != selected) SelectRepairJob(pick); Choose(selected); }
                }
            }
            UpdateFieldHighlight();
        }
        void NavigateField(Vector2 direction)
        {
            if (!repairPreview.IsCreated) return;
            if (direction.y != 0) { NavigateRepairList(direction.y < 0 ? 1 : -1); return; }
            if (selected == 13) { SelectRepairJob(0); return; }
            var origin = FieldRect(selected).center; int best = -1; float score = float.MaxValue;
            var d = new Vector2(direction.x, -direction.y);
            for (int i = indoorPreview ? 14 : 0; i < (indoorPreview ? 14 + RepairModel.InteriorJobs.Length : 13); i++)
            {
                if (i == selected || i < 13 && state.Completed.Contains(RepairModel.Jobs[i].Id)) continue; var delta = FieldRect(i).center - origin; float along = Vector2.Dot(delta, d); if (along < 3) continue;
                float across = Mathf.Abs(delta.x * d.y - delta.y * d.x); float candidate = delta.magnitude + across * 2;
                if (candidate < score) { score = candidate; best = i; }
            }
            if (best >= 0) SelectRepairJob(best); UpdateFieldHighlight();
        }
    }
}
