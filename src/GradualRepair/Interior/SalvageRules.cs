using System.Linq;
namespace GradualRepair
{
    public sealed class SalvageReward
    {
        public readonly string Key, Source; public readonly int ItemId, Amount;
        public SalvageReward(string key, string source, int itemId, int amount) { Key = key; Source = source; ItemId = itemId; Amount = amount; }
    }
    public static class SalvageRules
    {
        public static readonly SalvageReward[] Rewards ={
   new SalvageReward("bedroom-stool","bedroom-clutter",660,1),
   new SalvageReward("bedroom-firewood","bedroom-clutter",1040,3),
   new SalvageReward("bricks-a-block","bricks-a",1050,1),
   new SalvageReward("bricks-a-stone","bricks-a",1049,2),
   new SalvageReward("bricks-b-block","bricks-b",1050,1),
   new SalvageReward("bricks-b-stone","bricks-b",1049,2),
   new SalvageReward("corner-block","bar-bricks",1050,1),
   new SalvageReward("corner-stone","bar-bricks",1049,1),
   new SalvageReward("bottles-decoration","bricks-c",662,2),
   new SalvageReward("bottles-stone","bricks-c",1049,1),
   new SalvageReward("fallen-table-furniture","fallen-table",1133,1),
   new SalvageReward("fallen-table-stone","fallen-table",1049,2),
   new SalvageReward("covered-clock","covered-boxes",1872,1),
   new SalvageReward("covered-stone","covered-boxes",1049,2),
   new SalvageReward("covered-planks","covered-boxes",1036,2)
  };
        public static SalvageReward Find(string key) => Rewards.FirstOrDefault(r => r.Key == key);
    }
}

