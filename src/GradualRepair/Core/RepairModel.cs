using System;
using System.Collections.Generic;
using System.Linq;

namespace GradualRepair
{
    public static class CounterCleaningRules
    {
        public static bool CanWork(bool alreadyWorking, bool customersWaiting) => alreadyWorking || !customersWaiting;
        public static bool CanBegin(bool guideShown, bool held, bool wasHeld) => guideShown && held && !wasHeld;
        public static bool InReach(float x, float y, float minX, float maxX, float minY, float maxY)
        {
            bool front = y >= minY - .03f && y <= minY + .18f;
            bool back = y >= maxY - .7f && y <= maxY - .18f;
            return x >= minX && x <= maxX && (front || back);
        }
    }
    public static class RepairSelectionRules
    {
        public static bool IsRoof(string id) => id == "roof-left" || id == "roof-right";
        public static bool IsDetail(string id) => id.StartsWith("window-", StringComparison.Ordinal) || id == "door" || id == "roof-window" || id == "chimney";
        public static int Priority(string id) => IsDetail(id) ? 0 : IsRoof(id) ? 1 : 2;
    }
    public sealed class RepairJob
    {
        public string Id, OldPath, NewPath;
        readonly string nameKey;
        public string Name => RepairText.Get(nameKey);
        public int Stage, Planks, Nails, Blocks, Silver, Mortar;
        public RepairJob(string id, string name, int stage, int planks, int nails, int blocks, int silver, string oldPath, string newPath, int mortar = 0)
        { Id = id; nameKey = name; Stage = stage; Planks = planks; Nails = nails; Blocks = blocks; Silver = silver; OldPath = oldPath; NewPath = newPath; Mortar = mortar; }
    }
    public sealed class RepairState
    {
        public int Schema = 4;
        public long LedgerRevision;
        public double VisitorOpenMinutes, VisitorAdmissionCredit;
        public int WordOfMouthCustomers;
        public int WallLayout;
        public bool InteriorProject = true, InteriorCostsWaived;
        public bool AgedMaterialsInitialized;
        public Dictionary<string, bool> AgedDecorations = new Dictionary<string, bool>();
        public Dictionary<string, int> IndoorDecorBaseline = new Dictionary<string, int>();
        public List<string> RenovatedIndoorPatches = new List<string>();
        public List<string> CompletedInterior = new List<string>();
        public List<string> CleanedInitialDirt = new List<string>();
        public bool ExteriorProject = true;
        [Newtonsoft.Json.JsonIgnore] public bool LegacyFormat;
        public string SaveHash;
        public List<string> Completed = new List<string>();
        public bool FinalFeePaid; // Historical ledger field only. No longer a completion requirement or charge.
        public bool MaiCongratulationsHeard;
        public bool CounterCleaned;
        public List<string> ClearedDebris = new List<string>();
        public Dictionary<string, int> PendingSalvage = new Dictionary<string, int>();
        public RepairState Copy() => new RepairState { LedgerRevision = LedgerRevision, MaiCongratulationsHeard = MaiCongratulationsHeard, PendingSalvage = new Dictionary<string, int>(PendingSalvage), Schema = Schema, VisitorOpenMinutes = VisitorOpenMinutes, VisitorAdmissionCredit = VisitorAdmissionCredit, WordOfMouthCustomers = WordOfMouthCustomers, WallLayout = WallLayout, AgedMaterialsInitialized = AgedMaterialsInitialized, InteriorProject = InteriorProject, InteriorCostsWaived = InteriorCostsWaived, AgedDecorations = new Dictionary<string, bool>(AgedDecorations), IndoorDecorBaseline = new Dictionary<string, int>(IndoorDecorBaseline), RenovatedIndoorPatches = new List<string>(RenovatedIndoorPatches), CompletedInterior = new List<string>(CompletedInterior), CleanedInitialDirt = new List<string>(CleanedInitialDirt), ExteriorProject = ExteriorProject, LegacyFormat = LegacyFormat, SaveHash = SaveHash, Completed = new List<string>(Completed), FinalFeePaid = FinalFeePaid, CounterCleaned = CounterCleaned, ClearedDebris = new List<string>(ClearedDebris) };
    }
    public static class RepairModel
    {
        public static readonly RepairJob[] Jobs ={
            new RepairJob("roof-left","exterior.roof-left",3,80,50,0,500,"Left Side/Roof Old","Left Side/Roof",12),
            new RepairJob("roof-right","exterior.roof-right",3,60,40,0,400,"Right Side/Roof Right Old","Right Side/Roof Right",8),
            new RepairJob("roof-window","exterior.roof-window",3,25,15,0,150,"Right Side/Roof Window Old","Right Side/Roof Window",2),
            new RepairJob("chimney","exterior.chimney",3,0,0,20,200,"Left Side/Chimenea Old","Left Side/Chimenea",6),
            new RepairJob("wall-a","exterior.wall-upper",2,20,0,20,250,"Left Side/Wall A Old","Left Side/Wall A",8),
            new RepairJob("wall-b","exterior.wall-entrance",2,20,0,20,250,"Left Side/Wall B Old","Left Side/Wall B",8),
            new RepairJob("wall-c","exterior.wall-right",2,20,0,20,250,"Right Side/Wall C Old","Right Side/Wall C",8),
            new RepairJob("window-left","exterior.window-left",1,15,5,0,40,"Left Side/Window A Old Left","Left Side/Window A Left",0),
            new RepairJob("window-middle","exterior.window-middle",1,15,5,0,40,"Left Side/Window A Old Middle","Left Side/Window A Middle",0),
            new RepairJob("window-right","exterior.window-right",1,15,5,0,40,"Left Side/Window A Old Right","Left Side/Window A Right",0),
            new RepairJob("window-large","exterior.window-entrance",1,30,15,0,80,"Left Side/Window D Old","Left Side/Window D",0),
            new RepairJob("window-side","exterior.window-lower-right",1,25,10,0,60,"Right Side/Window C Old","Right Side/Window C",0),
            new RepairJob("door","exterior.door",1,25,15,0,80,"Left Side/Door Old","Left Side/Door",0)
        };
        public static readonly RepairJob[] InteriorJobs ={
            new RepairJob("floor-00","interior.floor-dining-center",5,2,1,0,1,"Floor/Floor Damage",null),
            new RepairJob("floor-01","interior.floor-dining-right",5,2,1,0,1,"Floor/Floor Damage (1)",null),
            new RepairJob("floor-02","interior.floor-entrance-left",5,4,1,0,3,"Floor/Floor Damage (2)",null),
            new RepairJob("floor-03","interior.floor-dining-front",5,2,1,0,1,"Floor/Floor Damage (3)",null),
            new RepairJob("floor-04","interior.floor-entrance-right",5,4,1,0,3,"Floor/Floor Damage (4)",null),
            new RepairJob("floor-05","interior.floor-counter",5,4,1,0,3,"Floor/Floor Damage (5)",null),
            new RepairJob("floor-06","interior.floor-bedroom-entrance",5,3,1,0,2,"Floor/Floor Damage (6)",null),
            new RepairJob("floor-07","interior.floor-hall-right",5,3,1,0,2,"Floor/Floor Damage (7)",null),
            new RepairJob("floor-08","interior.floor-bedroom-back",5,2,1,0,1,"Floor/Floor Damage (8)",null),
            new RepairJob("floor-09","interior.floor-kitchen-right",5,1,0,0,1,"Floor/Floor Damage (9)",null),
            new RepairJob("floor-10","interior.floor-kitchen-left",5,2,1,0,1,"Floor/Floor Damage (10)",null),
            new RepairJob("floor-11","interior.floor-hall-left",5,1,0,0,1,"Floor/Floor Damage (11)",null),
            new RepairJob("wall-seat-left","interior.wall-dining-left",5,0,0,1,2,"Pared 1/Wall Damage (3)",null),
            new RepairJob("wall-seat-middle","interior.wall-dining-middle",5,0,0,4,6,"Pared 1/Wall Damage|Pared 1/Wall Damage (5)",null),
            new RepairJob("wall-seat-under","interior.wall-dining-under",5,0,0,1,2,"Pared 1/Wall Damage (4)",null),
            new RepairJob("wall-seat-door","interior.wall-dining-door",5,0,0,2,4,"Pared 1/Wall Damage (1)|Pared 1/Wall Damage (2)",null),
            new RepairJob("wall-fireplace","interior.wall-fireplace",5,0,0,2,4,"Pared 2/Wall Damage (6)",null),
            new RepairJob("wall-hall-left","interior.wall-hall-left",5,0,0,2,3,"Pared 3/Wall Damage (13)|Pared 3/Wall Damage (7)",null),
            new RepairJob("wall-hall-right","interior.wall-hall-right",5,0,0,4,6,"Pared 3/Wall Damage (9)|Pared 3/Wall Damage (8)|Pared 3/Wall Damage (12)|Pared 3/Wall Damage (10)|Pared 3/Wall Damage (11)",null),
            new RepairJob("wall-bedroom-left","interior.wall-bedroom-left",5,0,0,3,4,"Pared 4/Wall Damage (13)|Pared 4/Wall Damage (10)|Pared 4/Wall Damage (7)",null),
            new RepairJob("wall-bedroom-middle","interior.wall-bedroom-middle",5,0,0,3,4,"Pared 4/Wall Damage (9)|Pared 4/Wall Damage (8)|Pared 4/Wall Damage (12)",null),
            new RepairJob("wall-bedroom-right","interior.wall-bedroom-right",5,0,0,2,4,"Pared 4/Wall Damage (11)",null),
            new RepairJob("wall-kitchen-left-crack","interior.wall-kitchen-left-crack",5,0,0,1,2,"Pared 5/Wall Damage (11)",null),
            new RepairJob("wall-kitchen-left-upper","interior.wall-kitchen-left-upper",5,0,0,1,2,"Pared 5/Wall Damage (8)",null),
            new RepairJob("wall-kitchen-left-lower","interior.wall-kitchen-left-lower",5,0,0,2,2,"Pared 5/Wall Damage (7)",null),
            new RepairJob("wall-kitchen-middle-left","interior.wall-kitchen-middle-left",5,0,0,1,2,"Pared 5/Wall Damage (13)",null),
            new RepairJob("wall-kitchen-middle-right","interior.wall-kitchen-middle-right",5,0,0,2,3,"Pared 5/Wall Damage (10)",null),
            new RepairJob("wall-kitchen-right","interior.wall-kitchen-right",5,0,0,3,4,"Pared 5/Wall Damage (12)|Pared 5/Wall Damage (9)",null),
        };
        public static bool MatchesIndoorPath(RepairJob job, string path) => job.OldPath.Split('|').Contains(path);
        // Expand only the old paid wall groups; native saves and inventory are untouched.
        public static bool UpgradeWallLayout(RepairState state)
        {
            if (state.WallLayout >= 2) return false;
            if (state.WallLayout < 1) for (int i = 1; i <= 5; i++) if (state.CompletedInterior.Remove("indoor-wall-" + i))
                foreach (var job in InteriorJobs.Where(j => j.OldPath.StartsWith("Pared " + i + "/"))) if (!state.CompletedInterior.Contains(job.Id)) state.CompletedInterior.Add(job.Id);
            if (state.CompletedInterior.Remove("wall-kitchen-left"))
                foreach (string id in new[] { "wall-kitchen-left-crack", "wall-kitchen-left-upper", "wall-kitchen-left-lower" }) if (!state.CompletedInterior.Contains(id)) state.CompletedInterior.Add(id);
            state.WallLayout = 2; return true;
        }
        public static readonly string[] InitialDirtIds = { "floor-dirt-0", "floor-dirt-1", "floor-dirt-2", "floor-dirt-3" };
        public static bool InteriorDone(RepairState state) => !state.InteriorProject || InteriorJobs.All(j => state.CompletedInterior.Contains(j.Id));
        public static bool AllDone(RepairState state) => Jobs.All(j => state.Completed.Contains(j.Id));
        public static int Stage(RepairState state) => Jobs.FirstOrDefault(j => !state.Completed.Contains(j.Id))?.Stage ?? 4;
        public static bool Available(RepairState state, RepairJob job) => !state.Completed.Contains(job.Id);
        static bool ValidAgedKey(string key) { if (key == null || key.Length < 5 || (key[0] != 'F' && key[0] != 'W' && key[0] != 'T') || key[1] != ':') return false; var xy = key.Substring(2).Split(','); return xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y) && Math.Abs((long)x) < 1000000 && Math.Abs((long)y) < 1000000; }
        public static void Validate(RepairState state)
        {
            if (state != null && (state.ClearedDebris == null || state.PendingSalvage == null || state.PendingSalvage.Any(p => SalvageRules.Find(p.Key) == null || p.Value < 1 || p.Value > SalvageRules.Find(p.Key).Amount || !state.ClearedDebris.Contains(SalvageRules.Find(p.Key).Source)))) throw new InvalidOperationException("Invalid salvage data");
            if (state != null && (state.ClearedDebris == null || state.ClearedDebris.Distinct().Count() != state.ClearedDebris.Count || state.ClearedDebris.Any(x => !DebrisRules.Ids.Contains(x)))) throw new InvalidOperationException("Unknown debris cleanup data");
            if (state != null && (double.IsNaN(state.VisitorOpenMinutes) || double.IsInfinity(state.VisitorOpenMinutes) || state.VisitorOpenMinutes < 0 || double.IsNaN(state.VisitorAdmissionCredit) || state.VisitorAdmissionCredit < 0 || state.VisitorAdmissionCredit >= 1 || state.WordOfMouthCustomers < 0 || state.WordOfMouthCustomers > 1000000)) throw new InvalidOperationException("Invalid visitor progress");
            if (state == null || state.Schema != 4 || state.Completed == null || state.Completed.Distinct().Count() != state.Completed.Count || state.Completed.Any(id => !Jobs.Any(j => j.Id == id))) throw new InvalidOperationException("Unknown repair data");
            if (state.CompletedInterior == null || state.CompletedInterior.Distinct().Count() != state.CompletedInterior.Count || state.CompletedInterior.Any(x => !InteriorJobs.Any(j => j.Id == x)) || state.CleanedInitialDirt == null || state.CleanedInitialDirt.Distinct().Count() != state.CleanedInitialDirt.Count || state.CleanedInitialDirt.Any(x => !InitialDirtIds.Contains(x))) throw new InvalidOperationException("Invalid indoor repair or hygiene data");
            if (state.IndoorDecorBaseline == null || state.RenovatedIndoorPatches == null || state.IndoorDecorBaseline.Count > 20000 || state.RenovatedIndoorPatches.Count > 100 || state.RenovatedIndoorPatches.Distinct().Count() != state.RenovatedIndoorPatches.Count) throw new InvalidOperationException("Invalid renovation data");
            if (state.AgedDecorations == null || state.AgedDecorations.Count > 200000 || state.AgedDecorations.Keys.Any(k => !ValidAgedKey(k))) throw new InvalidOperationException("Invalid aged building data");
            if (state.FinalFeePaid && !AllDone(state)) throw new InvalidOperationException("Invalid final fee state");
        }
        public static void ReconcileProject(RepairState state, bool vanillaComplete)
        {
            if (vanillaComplete && !state.ExteriorProject) state.InteriorProject = false;
            if (!vanillaComplete) state.ExteriorProject = true;
            UpgradeWallLayout(state); state.Schema = 4; state.LegacyFormat = false;
        }
        public static int Scale(int amount, float multiplier) => (int)Math.Ceiling(amount * (double)multiplier);
    }
    public static class DebrisRules
    {
        public static readonly string[] Ids = { "bricks-a", "bricks-b", "bricks-c", "fallen-table", "covered-boxes", "bar-bricks", "bedroom-clutter" };
        public static float Duration(float normalFloorSeconds, bool large) => normalFloorSeconds * (large ? 3f : 1f);
    }
}








