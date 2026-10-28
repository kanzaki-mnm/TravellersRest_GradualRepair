using System;
using System.Linq;
using UnityEngine;
namespace GradualRepair
{
    public sealed partial class Plugin
    {
        float nextSalvage;
        bool salvageFailed;
        void QueueDebrisSalvage(string id)
        {
            var rewards = SalvageRules.Rewards.Where(r => r.Source == id).ToArray();
            if (rewards.Length == 0) return;
            foreach (var reward in rewards) state.PendingSalvage.Add(reward.Key, reward.Amount);
            dirty = true; nextSalvage = 0;
            Log("salvage_queued", new { id, rewards = rewards.Select(r => new { r.Key, r.ItemId, r.Amount }).ToArray() });
        }
        void UpdateSalvage()
        {
            if (salvageFailed || !bound || !projectInitialized || state.PendingSalvage.Count == 0 || !IndoorCleanupProject() || open || debrisWorking || counterWorking || !CanOpen() || Time.unscaledTime < nextSalvage) return;
            nextSalvage = Time.unscaledTime + 2;
            try
            {
                foreach (var key in state.PendingSalvage.Keys.ToArray()) DeliverSalvage(key);
            }
            catch (Exception e) { salvageFailed = true; Log("salvage_error", e.ToString()); Logger.LogError(e); }
        }
        void DeliverSalvage(string key)
        {
            int id = SalvageItemId(key), requested = state.PendingSalvage[key];
            var item = ItemDatabaseAccessor.GetItem(id) ?? throw new InvalidOperationException("Salvage item missing: " + id);
            var inventory = PlayerInventory.GetPlayer(1); int before = inventory.NumberOfItems(id), remaining = -1;
            try { remaining = inventory.AddItems(CreateSalvageInstance(item), requested, false, true, true, false); }
            finally
            {
                // Account for items actually inserted even if the native notification throws.
                int added = inventory.NumberOfItems(id) - before;
                if (added < 0 || added > requested) throw new InvalidOperationException("Unexpected salvage inventory change");
                if (added > 0) { if (added == requested) state.PendingSalvage.Remove(key); else state.PendingSalvage[key] = requested - added; dirty = true; Log("salvage_received", new { key, id, added, remaining = requested - added }); }
            }
            int pending = state.PendingSalvage.TryGetValue(key, out var value) ? value : 0;
            if (remaining != pending) throw new InvalidOperationException("Salvage inventory result mismatch");
        }
        static ItemInstance CreateSalvageInstance(Item item) => (ItemInstance)NativeGame.Hook("NewItemInstance").Invoke(item, null) ?? throw new InvalidOperationException("Native item instance unavailable");
        static int SalvageItemId(string key) => (SalvageRules.Find(key) ?? throw new InvalidOperationException("Unknown salvage reward")).ItemId;
    }
}

