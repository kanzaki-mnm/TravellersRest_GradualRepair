using System;
using System.Linq;
using UnityEngine;

namespace GradualRepair
{
    public sealed partial class Plugin
    {
        bool onsiteRepair, indoorGuide, indoorWasHeld;
        int indoorTarget = -1;
        bool IndoorInteractionReady() => IndoorCleanupProject() && state.InteriorProject && !failed && !nativePreparationPending && !open && !counterWorking && !debrisWorking && !counterGuide && !debrisGuide && CanOpen();
        bool NativeRepairPriority()
        {
            if (NativeCleanupPriority() || InteractObject.GetPlayer(1)?.GetCurrentInteractGO() != null) return true;
            var focused = InputByProximityManager.GetPlayer(1)?.GetCurrentFocusedInputElement()?.mainGameObject;
            // The bar's menu covers its entire rear aisle. Reserve customer
            // service, but allow wall repairs to replace the empty-bar menu here.
            if (focused != null && focused.GetComponent<BarMenuManager>() != null) return CounterHasCustomers();
            return DisplayedCleanupGuide(0);
        }
        Vector3 IndoorGuidePosition() { var part = indoorPatches.FirstOrDefault(p => p.JobIndex == indoorTarget); return part != null && part.FloorHoles != null ? part.HoleCenter : IndoorBounds(indoorTarget).center; }
        // Wall sprites extend above the walkable ground. Approach the foot of
        // their authored wall, rather than requiring feet to touch the crack.
        Vector3 IndoorApproach(IndoorPatch part, Vector3 feet)
        {
            var bounds = part.Bounds;
            if (part.Group.StartsWith("floor-"))
            {
                var point = ClosestFloorHole(part, feet);
                // Counter-side damage is approached from the wall side, never
                // from its lower edge where the serving taps are operated.
                if (part.Group == "floor-05" && feet.y < point.y - .02f) return feet + Vector3.up * 10;
                return point;
            }
            // Separate the lateral extent from the wall-contact strip. The old
            // radial test was centered below the reachable wall and rejected contact.
            if (part.Group == "wall-seat-middle" || part.Group == "wall-hall-left" || part.Group == "wall-hall-right")
            {
                float inset = Mathf.Min(.3f, bounds.size.x * .2f);
                if (feet.x < bounds.min.x + inset || feet.x > bounds.max.x - inset) return feet + Vector3.up * 10;
                return new Vector3(feet.x, Mathf.Clamp(feet.y, part.ApproachY + .15f, part.ApproachY + .8f), feet.z);
            }
            // The hearth blocks the wall foot; approach either open flank.
            if (part.Group == "wall-fireplace") { float left = bounds.min.x - .9f, right = bounds.max.x; bool fromLeft = Mathf.Abs(feet.x - left) <= Mathf.Abs(feet.x - right); return new Vector3(fromLeft ? left : right, fromLeft ? part.ApproachY - .65f : Mathf.Clamp(feet.y, part.ApproachY - .65f, bounds.center.y), feet.z); }
            if (!part.Group.StartsWith("wall-kitchen-"))
            {
                float x = Mathf.Clamp(feet.x, bounds.min.x, bounds.max.x);
                // Keep the existing lateral split at the bar's left end.
                if (part.Group == "wall-seat-left") x = Mathf.Clamp(feet.x, bounds.min.x, bounds.max.x + .6f);
                if (part.Group == "wall-seat-under") x = Mathf.Clamp(feet.x, bounds.center.x + .45f, bounds.max.x);
                return new Vector3(x, Mathf.Clamp(feet.y, part.ApproachY + .15f, part.ApproachY + .8f), feet.z);
            }
            // Kitchen equipment retains the established approach position.
            if (part.Group == "wall-seat-left") return new Vector3(Mathf.Clamp(feet.x, bounds.min.x, bounds.max.x + .6f), part.ApproachY, feet.z);
            if (part.Group == "wall-seat-under") return new Vector3(Mathf.Clamp(feet.x, bounds.center.x + .45f, bounds.max.x), part.ApproachY, feet.z);
            return new Vector3(Mathf.Clamp(feet.x, bounds.min.x, bounds.max.x), part.ApproachY, feet.z);
        }
        int PickIndoorRepair(Vector3 feet) => PickIndoorRepairFacing(feet, PlayerController.GetPlayer(1) != null && NativeGame.Facing(PlayerController.GetPlayer(1).characterAnimation) == Direction.Up);
        int PickIndoorRepairFacing(Vector3 feet, bool wallsFirst)
        {
            float wallDistance = WallRepairReach, floorDistance = FloorRepairReach, wallCenterDistance = float.PositiveInfinity; int wall = -1, floor = -1;
            foreach (var part in indoorPatches)
            {
                if (!part.Visible || state.CompletedInterior.Contains(part.Group)) continue;
                float distance = Vector2.Distance(feet, IndoorApproach(part, feet));
                if (distance >= IndoorReachLimit(part)) continue;
                if (part.Group.StartsWith("floor-")) { if (distance < floorDistance) { floorDistance = distance; floor = part.JobIndex; } }
                else { float centerDistance = Mathf.Abs(feet.x - part.Bounds.center.x); if (distance < wallDistance - .001f || (wall >= 14 && Mathf.Abs(distance - wallDistance) <= .001f && centerDistance < wallCenterDistance)) { wallDistance = distance; wallCenterDistance = centerDistance; wall = part.JobIndex; } }
            }
            return wallsFirst ? (wall >= 14 ? wall : floor) : (floor >= 14 ? floor : wall);
        }
        int[] NearbyIndoorRepairs(Vector3 feet) => indoorPatches.Where(p => p.Visible && !state.CompletedInterior.Contains(p.Group) && Vector2.Distance(feet, IndoorApproach(p, feet)) < IndoorReachLimit(p)).Select(p => p.JobIndex).Distinct().OrderBy(i => i).ToArray();
        void UpdateIndoorInteraction()
        {
            var input = PlayerInputs.GetPlayer(1); if (input == null) return;
            bool held = Input.GetKey(KeyCode.E) || input.GetButton(ActionType.Interact) || input.GetButton(ActionType.UIInteract);
            bool begin = CounterCleaningRules.CanBegin(indoorGuide && DisplayedCleanupGuide(3), held, indoorWasHeld); indoorWasHeld = held;
            if (!begin || !IndoorInteractionReady() || NativeRepairPriority()) return;
            var player = PlayerController.GetPlayer(1);
            if (player == null || indoorTarget < 14 || PickIndoorRepair(player.transform.position) != indoorTarget) return;
            if (TryOwnModInteraction()) OpenOnsiteRepair(indoorTarget);
        }
        void OpenOnsiteRepair(int index)
        {
            if (open || index < 14 || index >= 14 + RepairModel.InteriorJobs.Length || !IndoorInteractionReady()) return;
            onsiteRepair = true; open = true; selected = index; openedFrame = Time.frameCount; confirm = false;
            wasTimeBlocked = WorldTime.TickTimeIsBlocked(); WorldTime.BlockTickTime(true);
            PlayerInputs.DisableKeyboardInputsAndMovement(1, this); ClearIndoorGuide();
            try { ShowNativeRepairDialog(); Log("onsite_repair_open", new { job = JobAt(index).Id }); }
            catch (Exception ex) { Close(); Log("onsite_repair_error", ex.ToString()); Logger.LogError(ex); }
        }
        void ApplyIndoorGuide()
        {
            if (!IndoorInteractionReady() || NativeRepairPriority()) { ClearIndoorGuide(); return; }
            var player = PlayerController.GetPlayer(1); int next = player == null ? -1 : PickIndoorRepair(player.transform.position);
            if (next < 14) { ClearIndoorGuide(); return; }
            indoorTarget = next; indoorGuide = true;
            ButtonsContext.GetPlayer(1)?.SoftResetButtons();
            ButtonsContext.AddButton(1, ActionType.Interact, JobAt(next).Id.StartsWith("floor-") ? RepairText.Get("action.repair-floor") : RepairText.Get("action.repair-wall"));
            SetCleanupGuidePosition(IndoorGuidePosition());
        }
        void ClearIndoorGuide()
        {
            if (indoorGuide && DisplayedCleanupGuide(3)) ButtonsContext.GetPlayer(1)?.ResetButtons();
            indoorGuide = false; indoorTarget = -1;
        }
    }
}




