using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GradualRepair
{
    internal sealed class RepairSessionControls
    {
        string repairPreviousMap;
        string verticalNavigationAxis;
        internal void BeginRepairInputMap()
        {
            var input = PlayerInputs.GetPlayer(1);
            var previous = (string)AccessTools.Field(typeof(PlayerInputs), "_currentMapSelected").GetValue(input);
            // A just-closed native mailbox still reports UI until its next-frame map switch.
            repairPreviousMap = previous == "UI" ? "Game" : previous;
            var module = EventSystem.current == null ? null : EventSystem.current.currentInputModule;
            verticalNavigationAxis = module == null ? null : AccessTools.Property(module.GetType(), "verticalAxis")?.GetValue(module) as string;
            input.SwitchMapCategory("UI");
        }
        internal float VerticalNavigation()
        {
            var input = PlayerInputs.GetPlayer(1);
            return string.IsNullOrEmpty(verticalNavigationAxis) ? input.GetAxis(ActionType.VerticalMove) : input.GetAxis(verticalNavigationAxis);
        }
        internal void MaintainRepairInputMap()
        {
            if (repairPreviousMap == null) return;
            var input = PlayerInputs.GetPlayer(1);
            if ((string)AccessTools.Field(typeof(PlayerInputs), "_currentMapSelected").GetValue(input) != "UI") input.SwitchMapCategory("UI");
        }
        internal void EndRepairInputMap()
        {
            if (repairPreviousMap == null) return;
            var previous = repairPreviousMap; repairPreviousMap = null;
            if (PlayerInputs.GetPlayer(1) != null) PlayerInputs.GetPlayer(1).SwitchMapCategory(previous);
        }
        bool repairPauseOwned;
        float repairPreviousTimeScale;
        internal void BeginRepairPause()
        {
            if (repairPauseOwned) return;
            // Vanilla MainUI.PauseGame also sets timeScale to zero. Preserve the
            // previous value rather than calling ResumeGame's unconditional 1.
            repairPreviousTimeScale = Time.timeScale; repairPauseOwned = true; Time.timeScale = 0;
        }
        internal void MaintainRepairPause() { if (repairPauseOwned) Time.timeScale = 0; }
        internal void EndRepairPause()
        {
            if (!repairPauseOwned) return;
            repairPauseOwned = false;
            if (Mathf.Approximately(Time.timeScale, 0)) Time.timeScale = repairPreviousTimeScale;
        }
    }
}
