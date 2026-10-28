using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        InteractionGesture interactionGesture = new InteractionGesture();
        bool TryOwnModInteraction()
        {
            var input = PlayerInputs.GetPlayer(1); if (input == null) return false;
            bool held = Input.GetKey(KeyCode.E) || input.GetButton(ActionType.Interact) || input.GetButton(ActionType.UIInteract);
            bool down = Input.GetKeyDown(KeyCode.E) || input.GetButtonDown(ActionType.Interact) || input.GetButtonDown(ActionType.UIInteract);
            return interactionGesture.Claim(held, down, Time.frameCount);
        }
        bool ModInteractionCandidate()
        {
            if ((!Active() && !IndoorCleanupProject()) || failed) return false;
            if (counterGuide && DisplayedCleanupGuide(1) && CounterReady() && NearCounter()) return true;
            if (debrisGuide && DisplayedCleanupGuide(2) && DebrisReady() && !NativeCleanupPriority()) return true;
            if (indoorGuide && DisplayedCleanupGuide(3) && IndoorInteractionReady() && !NativeRepairPriority())
            {
                var player = PlayerController.GetPlayer(1);
                return player != null && indoorTarget >= 14 && PickIndoorRepair(player.transform.position) == indoorTarget;
            }
            return false;
        }
        static bool BeforeNativeInteractInput(InteractObject __instance, ref bool __result)
        {
            if (self == null || __instance != InteractObject.GetPlayer(1) || !NativeGame.Ready) return true;
            return self.RouteNativeInteraction(ref __result);
        }
        static bool BeforeNativeToolUpdate(UseObject __instance)
        {
            if (self == null || __instance != UseObject.GetPlayer(1) || !NativeGame.Ready) return true;
            bool ignored = false; return self.RouteNativeInteraction(ref ignored);
        }
        bool RouteNativeInteraction(ref bool result)
        {
            var input = PlayerInputs.GetPlayer(1); if (input == null) return true;
            bool held = Input.GetKey(KeyCode.E) || input.GetButton(ActionType.Interact) || input.GetButton(ActionType.UIInteract);
            bool down = Input.GetKeyDown(KeyCode.E) || input.GetButtonDown(ActionType.Interact) || input.GetButtonDown(ActionType.UIInteract);
            bool working = self.open || self.counterWorking || self.debrisWorking;
            bool allow = self.interactionGesture.Route(held, down, down && self.ModInteractionCandidate(), working, Time.frameCount);
            if (!allow) result = false;
            return allow;
        }
    }
}
