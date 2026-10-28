using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        RepairPlanView repairPlan;
        RectTransform repairRoot => repairPlan?.Root;
        float RepairSidebarX => fieldWidth - 400;
        RectTransform UiRect(string name, Transform parent, float x, float y, float width, float height)
            => RepairUiLayout.Rect(name, parent, x, y, width, height);

        int[] RemainingExteriorJobs() => Enumerable.Range(0, 13).Where(i => !state.Completed.Contains(RepairModel.Jobs[i].Id)).ToArray();
        void NormalizeRepairSelection()
        {
            var remaining = RemainingExteriorJobs();
            if (remaining.Length > 0 && !remaining.Contains(selected)) { selected = remaining[0]; confirm = false; }
        }
        RepairPlanData ReadRepairPlanData()
        {
            var job = JobAt(selected);
            bool paid = IsInterior(job) ? state.CompletedInterior.Contains(job.Id) : state.Completed.Contains(job.Id);
            var ids = RepairCostRules.MaterialIds.Concat(repairPlan?.WalletResourceIds ?? Array.Empty<int>()).Distinct();
            var owned = ids.ToDictionary(id => id, id => Count(id));
            return new RepairPlanData(selected, RemainingExteriorJobs(), state.Completed.Count,
                job.Name, Needed(job), Fee(job), Money.ToCopper(), !paid && !failed,
                !onsiteRepair && RepairModel.AllDone(state), PlayerInputs.IsGamepadActive(1),
                onsiteRepair, confirm, failed ? message : "", owned);
        }
        void ShowRepairPanel()
        {
            ReleaseExteriorRepairFeedback();
            NormalizeRepairSelection();
            if (repairPlan == null) repairPlan = new RepairPlanView();
            repairPlan.Show(RepairModel.Jobs.Take(13).Select(j => j.Name).ToArray(), ReadRepairPlanData(), HoverRepairJob, ActivateRepairJob, Close);
            fieldWidth = repairPlan.Width;
            if (!onsiteRepair) CreateFieldView();
            RefreshRepairPanel();
            Log("repair_ui_open", new { layout = "field repair mode", freeRepairOrder = true });
        }
        void HoverRepairJob(int index) { SelectRepairJob(index); UpdateFieldHighlight(); }
        void ActivateRepairJob(int index)
        {
            if (exteriorConfirmation) return;
            SelectRepairJob(index); UpdateFieldHighlight(); Choose(index); RefreshRepairPanel();
        }
        void SelectRepairJob(int index)
        {
            if (exteriorConfirmation) return;
            if (index == 13 || index < 13 && state.Completed.Contains(RepairModel.Jobs[index].Id)) return;
            selected = index; confirm = false;
            if (indoorPreview != (index >= 14)) SetInteriorPreview(index >= 14);
            RefreshRepairPanel(); EnsureRepairRowVisible();
        }
        void EnsureRepairRowVisible() => repairPlan?.EnsureVisible();
        void NavigateRepairList(int step)
        {
            var remaining = RemainingExteriorJobs(); if (remaining.Length == 0) return;
            int at = Array.IndexOf(remaining, selected);
            SelectRepairJob(remaining[Mathf.Clamp(at + step, 0, remaining.Length - 1)]);
            EnsureRepairRowVisible(); UpdateFieldHighlight();
        }
        float nextRepairPanelPoll;
        int cachedMoney, cachedPlanks, cachedNails, cachedStone, cachedMortar, cachedSelection, cachedCompleted;
        bool cachedPad;
        void PollRepairPanel()
        {
            if (repairPlan == null || !repairPlan.IsCreated || Time.unscaledTime < nextRepairPanelPoll) return;
            nextRepairPanelPoll = Time.unscaledTime + .2f;
            if (cachedMoney != Money.ToCopper() || cachedPlanks != Count(1036) || cachedNails != Count(1045) || cachedStone != Count(1050) || cachedMortar != Count(1052) || cachedSelection != selected || cachedCompleted != state.Completed.Count || cachedPad != PlayerInputs.IsGamepadActive(1)) RefreshRepairPanel();
        }
        void RefreshRepairPanel()
        {
            if (repairPlan == null || !repairPlan.IsCreated) return;
            NormalizeRepairSelection();
            cachedMoney = Money.ToCopper(); cachedPlanks = Count(1036); cachedNails = Count(1045); cachedStone = Count(1050); cachedMortar = Count(1052); cachedSelection = selected; cachedCompleted = state.Completed.Count; cachedPad = PlayerInputs.IsGamepadActive(1);
            repairPlan.Refresh(ReadRepairPlanData());
        }
        void HideRepairPanel()
        {
            ReleaseFieldView(); ReleaseExteriorRepairFeedback(); repairPlan?.Hide();
        }
    }
}
