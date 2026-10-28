using System;
using System.IO;
using System.Linq;
using GradualRepair;
int checks=0;
void Check(bool b,string label){if(!b)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
void Reject(Action action,string label){try{action();}catch{Check(true,label);return;}throw new Exception(label);}
Check(CounterCleaningRules.InReach(2,0f,0,4,0,1),"counter front contact is reachable");
Check(!CounterCleaningRules.InReach(2,-.4f,0,4,0,1),"distant front position cannot clean");
Check(CounterCleaningRules.InReach(2,.7f,0,4,0,1),"counter inside contact is reachable");
Check(!CounterCleaningRules.InReach(2,1.5f,0,4,0,1),"behind-counter wall position cannot clean");
Check(!CounterCleaningRules.InReach(5,0,0,4,0,1),"outside counter width cannot clean");
Check(!CounterCleaningRules.CanWork(false,true),"waiting customers take priority before cleaning starts");
Check(CounterCleaningRules.CanWork(true,true),"customer arrival does not interrupt cleaning already started");
Check(CounterCleaningRules.CanWork(false,false),"empty counter allows cleaning to start");
Check(CounterCleaningRules.CanBegin(true,true,false),"fresh press on cleaning guide can start cleaning");
Check(!CounterCleaningRules.CanBegin(true,true,true),"holding service button cannot turn into cleaning");
Check(!CounterCleaningRules.CanBegin(false,true,false),"press without cleaning guide is left to native interaction");
Check(!CounterCleaningRules.InReach(2,-.2f,0,4,0,1),"previous distant front guide position is excluded");
Check(!CounterCleaningRules.InReach(2,.95f,0,4,0,1),"previous distant inside guide position is excluded");
Check(CounterCleaningRules.InReach(3.9f,.7f,0,4,0,1),"right edge is reachable again within counter width");
Check(CounterCleaningRules.InReach(3.5f,.7f,0,4,0,1),"usable counter width remains reachable before right end");
var jobs=RepairModel.Jobs.Concat(RepairModel.InteriorJobs).ToArray();
Check(RepairModel.InteriorJobs.Length==28,"twelve floors and sixteen wall jobs");Check(jobs.Select(j=>j.Id).Distinct().Count()==41,"unique repair IDs");
Check(jobs.Sum(j=>j.Planks)==380,"combined plank budget 380");Check(jobs.Sum(j=>j.Nails)==170,"combined nail budget 170");Check(jobs.Sum(j=>j.Blocks)==114,"combined stone budget 114");Check(jobs.Sum(j=>j.Mortar)==52,"exterior mortar budget 52");Check(jobs.Sum(j=>j.Silver)==2414,"combined labor budget 24 gold 14 silver");
foreach(var job in jobs) {
 Check(RepairCostRules.Materials(job,0).All(x=>x==0)&&RepairCostRules.FeeCopper(job,0)==0,"free repair "+job.Id);
 foreach(int percent in new[]{1,20,100,150,1000}) {
  var amounts=RepairCostRules.Materials(job,percent);var bases=new[]{job.Planks,job.Nails,job.Blocks,job.Mortar};
  Check(amounts.Where((n,i)=>bases[i]==0?n!=0:n<1).Count()==0,"positive minimum and absent material "+job.Id+" "+percent);
 }
 Check(RepairCostRules.FeeCopper(job,100)==job.Silver*100,"standard fee in copper "+job.Id);
}
Check(RepairCostRules.Percent(-1)==0&&RepairCostRules.Percent(1001)==1000,"percent range clamps both ends");
Check(RepairCostRules.Amount(5,150)==8&&RepairCostRules.Amount(4,20)==1&&RepairCostRules.Amount(80,20)==16,"round nearest away from zero");
Check(RepairCostRules.FeeCopper(new RepairJob("test","test",5,0,0,0,1,null,null),20)==20,"fee supports copper precision");
var walls=RepairModel.InteriorJobs.Where(j=>j.Id.StartsWith("wall-")).ToArray();
Check(walls.Length==16&&walls.All(j=>j.Nails==0&&j.Planks==0),"walls use stone and money only");Check(walls.Sum(j=>j.Blocks)==34&&walls.Sum(j=>j.Silver)==54,"wall estimate 34 stone and 54 silver");
var paths=walls.SelectMany(j=>j.OldPath.Split('|')).ToArray();Check(paths.Length==28&&paths.Distinct().Count()==28,"all twenty-eight wall parts assigned once");
var old=new RepairState();old.CompletedInterior.Add("indoor-wall-1");old.CompletedInterior.Add("floor-00");RepairModel.UpgradeWallLayout(old);RepairModel.Validate(old);Check(old.CompletedInterior.Count==5&&old.CompletedInterior.Contains("floor-00"),"old paid guest wall expands without losing floor");Check(!RepairModel.UpgradeWallLayout(old)&&old.CompletedInterior.Count==5,"wall expansion is idempotent");
var kitchenOld=new RepairState{WallLayout=1};kitchenOld.CompletedInterior.Add("wall-kitchen-left");RepairModel.UpgradeWallLayout(kitchenOld);RepairModel.Validate(kitchenOld);Check(kitchenOld.CompletedInterior.Count==3&&kitchenOld.CompletedInterior.All(id=>id.StartsWith("wall-kitchen-left-")),"paid kitchen group expands into three paid repairs");
var kitchen=walls.Where(j=>j.Id.StartsWith("wall-kitchen-left-")).ToArray();Check(kitchen.Length==3&&kitchen.All(j=>j.OldPath.Split('|').Length==1)&&kitchen.Sum(j=>j.Blocks)==4&&kitchen.Sum(j=>j.Silver)==6,"three independent kitchen repairs keep original budget");
var state=new RepairState();state.CounterCleaned=true;state.CleanedInitialDirt.AddRange(RepairModel.InitialDirtIds);state.ClearedDebris.AddRange(new[]{"bar-bricks","bricks-a"});Check(state.Copy().CounterCleaned&&state.Copy().CleanedInitialDirt.Count==4,"optional cleaning progress survives copy");
state.Completed.AddRange(RepairModel.Jobs.Select(j=>j.Id));
foreach(var job in RepairModel.InteriorJobs){state.CompletedInterior.Add(job.Id);RepairModel.Validate(state);Check(state.Copy().CompletedInterior.Contains(job.Id),"independent indoor progress "+job.Id);}
Check(RepairModel.AllDone(state),"all exterior repairs complete without final fee");state.FinalFeePaid=true;
var root=Path.GetFullPath("research/gradual-repair/individual-interior-0.9/model/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var save=Path.Combine(root,"native.save");File.WriteAllText(save,"untouched native data");var store=new StateStore(Path.Combine(root,"ledger"));state.IndoorDecorBaseline["Floor/Floor Damage@12,1808"]=10;state.AgedDecorations["F:2,1800"]=true;state.AgedDecorations["W:4,1810"]=false;state.AgedDecorations["T:5,1812"]=true;store.Commit(save,state);var read=store.Load(save);Check(read.CompletedInterior.SequenceEqual(state.CompletedInterior)&&read.IndoorDecorBaseline.Count==1,"per-place and baseline state survives save/load");Check(File.ReadAllText(save)=="untouched native data","native bytes unchanged");
Check(read.AgedDecorations["F:2,1800"]&&!read.AgedDecorations["W:4,1810"],"aged and explicit normal styles survive ledger save/load");Check(read.AgedDecorations["T:5,1812"],"aged trim survives ledger roundtrip");var noSave=state.Copy();noSave.AgedDecorations["F:2,1800"]=false;Check(store.Load(save).AgedDecorations["F:2,1800"],"unsaved style changes do not alter saved ledger");
var copied=state.Copy();copied.CompletedInterior.Clear();Check(state.CompletedInterior.Count==28,"snapshots do not share repair lists");
var invalid=state.Copy();invalid.CompletedInterior.Add("floor-00");Reject(()=>RepairModel.Validate(invalid),"duplicate indoor progress rejected");invalid=state.Copy();invalid.CompletedInterior.Add("unknown");Reject(()=>RepairModel.Validate(invalid),"unknown indoor progress rejected");
invalid=state.Copy();invalid.AgedDecorations["bad"]=true;Reject(()=>RepairModel.Validate(invalid),"invalid style grid key rejected");
invalid=state.Copy();invalid.Schema=3;Reject(()=>RepairModel.Validate(invalid),"old schemas not migrated in this prototype");
File.WriteAllText(save,"saved without mod");read=store.Load(save);RepairModel.ReconcileProject(read,true);Check(!read.ExteriorProject&&!read.InteriorProject,"untracked completed vanilla save stays clean");
Check(CounterCleaningRules.CanWork(true,true)&&!CounterCleaningRules.CanWork(false,true),"customer arrival priority unchanged");

Check(HygieneRules.ExtraDirt(true,false,false,60,40)==100,"both dirty areas add one hundred dirt points");
Check(HygieneRules.ExtraDirt(true,true,false,60,40)==40,"clean counter leaves forty points");
Check(HygieneRules.ExtraDirt(true,false,true,60,40)==60,"clean corner leaves sixty points");
Check(HygieneRules.ExtraDirt(true,true,true,60,40)==0,"both clean remove extra dirt");
Check(HygieneRules.ExtraDirt(false,false,false,60,40)==0,"disabled or inapplicable state contributes zero dirt");
Check(HygieneRules.ExtraDirt(true,false,false,-50,int.MaxValue)==1000,"area configuration bounds prevent overflow");
Check(HygieneRules.LevelIndex(0,100)==2,"initial unclean areas are messy but not Dirty");
Check(HygieneRules.LevelIndex(80,100)==2&&HygieneRules.LevelIndex(100,100)==3,"fifth native floor stain reaches Dirty when both areas remain");
Check(HygieneRules.LevelIndex(100,0)==2&&HygieneRules.LevelIndex(200,0)==3,"cleaned areas restore vanilla Dirty threshold");
Check(HygieneRules.LevelIndex(0,0)==0&&HygieneRules.LevelIndex(1,0)==1&&HygieneRules.LevelIndex(100,0)==2&&HygieneRules.LevelIndex(200,0)==3&&HygieneRules.LevelIndex(500,0)==4&&HygieneRules.LevelIndex(1000,0)==5,"native classification thresholds preserved");
Check(HygieneRules.LevelIndex(100,100)==3&&HygieneRules.LevelIndex(100,40)==2&&HygieneRules.LevelIndex(100,0)==2,"cleanup changes classification without clearing actual dirt");
float rawDirt=100;for(int i=0;i<1000;i++)HygieneRules.EffectiveScore(rawDirt,100);Check(rawDirt==100,"classification queries never accumulate dirt or mutate native score");
Check(HygieneRules.EffectiveScore(-1,0)==-1&&HygieneRules.LevelIndex(-1,0)==0,"native negative reset values remain untouched");
Check(HygieneRules.EffectiveScore(200,0)==200,"disabling modifier leaves actual floor and table dirt unchanged");
var materials=new RepairState();materials.AgedDecorations["F:1,2"]=false;materials.AgedDecorations["W:3,4"]=true;
Check(AgedStyleRules.ImportDefaults(materials,new[]{"F:1,2","W:3,4","T:3,4","F:5,6","F:5,6"})==2,"import deduplicates initial defaults and preserves explicit choices");
Check(!materials.AgedDecorations["F:1,2"]&&materials.AgedDecorations["W:3,4"],"normal repaint and existing aged wall are preserved");
Check(materials.AgedMaterialsInitialized&&materials.Copy().AgedMaterialsInitialized,"initial-material marker survives snapshots");
materials.AgedDecorations.Remove("F:5,6");Check(AgedStyleRules.ImportDefaults(materials,new[]{"F:5,6"})==0&&!materials.AgedDecorations.ContainsKey("F:5,6"),"removed construction cell cannot reappear from initial defaults");
var brokenImport=new RepairState();Reject(()=>AgedStyleRules.ImportDefaults(brokenImport,new[]{"F:0,0",null}),"invalid import fails");
Check(!brokenImport.AgedMaterialsInitialized&&brokenImport.AgedDecorations.Count==0,"failed import commits no partial material state");
var materialSave=Path.Combine(root,"material.save");File.WriteAllText(materialSave,"native material IDs only");
store.Commit(materialSave,materials);var materialReload=store.Load(materialSave);
Check(materialReload.AgedMaterialsInitialized&&materialReload.AgedDecorations.SequenceEqual(materials.AgedDecorations),"unified materials survive mod-ledger save and reload");
var unsavedMaterials=materialReload.Copy();unsavedMaterials.AgedDecorations["F:1,2"]=true;
Check(!store.Load(materialSave).AgedDecorations["F:1,2"],"unsaved repaint rolls back to saved material");
Check(File.ReadAllText(materialSave)=="native material IDs only","material persistence never changes native save bytes");
Check(Math.Abs(VisitorRules.Rate(0)-.05)<.00001,"initial admission five percent");
Check(Math.Abs(VisitorRules.Rate(720)-.06368)<.00001,"first twelve hours remain quiet");
Check(Math.Abs(VisitorRules.Rate(1440)-.10472)<.00001,"two twelve-hour days reach about ten percent");
Check(Math.Abs(VisitorRules.Rate(2880)-.26888)<.00001,"four twelve-hour days grow gradually");
Check(VisitorRules.Rate(3600)<1&&VisitorRules.Rate(6000)==1&&VisitorRules.Rate(9999)==1,"one hundred open hours restore native traffic");
Check(VisitorRules.Rate(0,.25,60)==.25,"initial share configurable");
Check(VisitorRules.Rate(600,.1,10)==1,"full traffic hours configurable");
Check(VisitorRules.Rate(0,.1,0)==1,"zero duration disables reduction");
double credit=0;int admitted=0;for(int i=0;i<100;i++)if(VisitorRules.Admit(.1,ref credit))admitted++;
Check(admitted==10,"deterministic floor prevents random starvation");
credit=0;admitted=0;for(int i=0;i<100;i++)if(VisitorRules.Admit(.85,ref credit))admitted++;
Check(admitted==85,"steady opportunity share is bounded");
var visits=new RepairState{VisitorOpenMinutes=75,WordOfMouthCustomers=7,VisitorAdmissionCredit=.4};var visitsCopy=visits.Copy();
Check(visitsCopy.VisitorOpenMinutes==75&&visitsCopy.WordOfMouthCustomers==7&&visitsCopy.VisitorAdmissionCredit==.4,"visitor progress copied with save snapshot");
visitsCopy.WordOfMouthCustomers++;Check(visits.WordOfMouthCustomers==7,"visitor snapshot independent");
RepairModel.Validate(visits);
store.Commit(save,visits);var persistedVisits=store.Load(save);
Check(persistedVisits.VisitorOpenMinutes==75&&persistedVisits.WordOfMouthCustomers==7&&persistedVisits.VisitorAdmissionCredit==.4,"visitor ledger save load round trip");
visits.VisitorOpenMinutes=200;visits.WordOfMouthCustomers=25;
Check(store.Load(save).VisitorOpenMinutes==75&&store.Load(save).WordOfMouthCustomers==7,"unsaved visitor progress rolls back with native reload");
var invalidVisits=new RepairState{VisitorAdmissionCredit=double.NaN};Reject(()=>RepairModel.Validate(invalidVisits),"invalid visitor credit rejected");

var gesture=new InteractionGesture();
Check(gesture.Route(true,true,false,false,0),"native press goes to native");
Check(!gesture.Claim(true,false,1),"mod cannot steal held native gesture");
Check(gesture.Route(true,false,true,false,1),"native hold retains owner after guide changes");
Check(gesture.Route(false,false,true,false,2),"native release always reaches native cleanup");
Check(!gesture.Route(true,true,true,false,3),"mod press reserved before native runs");
Check(gesture.Claim(true,true,3),"mod starts reserved gesture");
Check(!gesture.Route(true,false,false,true,4),"mod hold does not leak after guide changes");
Check(!gesture.Route(false,false,false,false,5),"mod release consumed");
Check(!gesture.Route(false,false,false,false,5),"repeat dispatch in release frame consumed");
Check(gesture.Route(true,true,false,false,6),"fresh service press returns to native");
var spam=new InteractionGesture();bool spamOk=true;
for(int i=0;i<1000;i++){int f=i*3;spamOk&=spam.Claim(true,true,f)&&!spam.Route(true,true,false,true,f)&&!spam.Route(false,false,false,false,f+1)&&spam.Route(false,false,false,false,f+2);}
Check(spamOk,"one thousand rapid cleanup starts and releases leave no ownership lock");
var releaseFirst=new InteractionGesture();releaseFirst.Claim(true,true,0);
Check(!releaseFirst.Route(false,false,false,true,1),"release consumed before cleaning Update stops work");
Check(releaseFirst.Route(true,true,false,false,2),"rapid native re-press works after early release");
Check(SalvageRules.Rewards.Select(r=>r.Key).Distinct().Count()==SalvageRules.Rewards.Length,"salvage keys unique");
Check(SalvageRules.Rewards.All(r=>DebrisRules.Ids.Contains(r.Source)&&r.Amount>0),"salvage maps to actual cleanup groups");
Check(SalvageRules.Find("covered-clock").ItemId==1872&&SalvageRules.Find("covered-clock").Amount==1,"covered furniture reveals floor clock");
Check(SalvageRules.Find("covered-stone").Amount==2&&SalvageRules.Find("covered-planks").Amount==2,"covered furniture includes small salvage supplies");
Check(SalvageRules.Find("fallen-table-furniture").ItemId==1133&&SalvageRules.Find("fallen-table-furniture").Amount==1,"large table selected by user");
Check(SalvageRules.Find("bottles-decoration").ItemId==662&&SalvageRules.Find("bottles-decoration").Amount==2,"two intact decorative bottles");
foreach(var source in SalvageRules.Rewards.Select(r=>r.Source).Distinct()) {
 var pending=new RepairState();pending.ClearedDebris.Add(source);
 foreach(var reward in SalvageRules.Rewards.Where(r=>r.Source==source))pending.PendingSalvage.Add(reward.Key,reward.Amount);
 RepairModel.Validate(pending);var snapshot=pending.Copy();pending.PendingSalvage.Clear();
 Check(snapshot.PendingSalvage.Count>0,"independent reward snapshot "+source);
}
var invalidReward=new RepairState();invalidReward.ClearedDebris.Add("fallen-table");invalidReward.PendingSalvage.Add("fallen-table-furniture",2);
Reject(()=>RepairModel.Validate(invalidReward),"excess furniture rejected");
invalidReward.PendingSalvage["fallen-table-furniture"]=1;invalidReward.ClearedDebris.Clear();
Reject(()=>RepairModel.Validate(invalidReward),"reward requires corresponding cleanup");




var maiState=new RepairState {MaiCongratulationsHeard=true};
Check(maiState.Copy().MaiCongratulationsHeard,"Mai heard flag copied with save snapshot");
Check(Newtonsoft.Json.JsonConvert.DeserializeObject<RepairState>(Newtonsoft.Json.JsonConvert.SerializeObject(maiState)).MaiCongratulationsHeard,"Mai heard flag roundtrip");
Check(!Newtonsoft.Json.JsonConvert.DeserializeObject<RepairState>("{}").MaiCongratulationsHeard,"old mod data keeps celebration pending");

// Fault injection: primary ledger failure after a native save must retain paid progress.
var recoveryTest=Path.Combine("research","gradual-repair","review-recovery-checks",Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(recoveryTest);
var nativeTest=Path.Combine(recoveryTest,"native.save");File.WriteAllText(nativeTest,"native debit snapshot");
var primaryTest=Path.Combine(recoveryTest,"primary");var recoveryTestRoot=Path.Combine(recoveryTest,"recovery");
var safeStore=new StateStore(primaryTest,recoveryTestRoot);var paidState=new RepairState();paidState.Completed.Add("roof-left");
var nativeHash=StateStore.HashFile(nativeTest);
Directory.CreateDirectory(Path.Combine(primaryTest,nativeHash+".json"));
Reject(()=>safeStore.Commit(nativeTest,paidState),"primary write fault reported");
var recovered=safeStore.Load(nativeTest);
Check(safeStore.RecoveredLastLoad&&recovered.Completed.Contains("roof-left"),"exact native save recovers paid repair after primary failure");
Check(StateStore.HashFile(nativeTest)==nativeHash,"recovery never edits native save");
Directory.Delete(Path.Combine(primaryTest,nativeHash+".json"));
safeStore.Commit(nativeTest,recovered);
Check(!safeStore.Load(nativeTest).Completed.Except(recovered.Completed).Any()&&!safeStore.RecoveredLastLoad,"retry restores primary ledger");
var blockedTemporary=Path.Combine(primaryTest,nativeHash+".json.tmp");Directory.CreateDirectory(blockedTemporary);
recovered.Completed.Add("roof-right");Reject(()=>safeStore.Commit(nativeTest,recovered),"failed overwrite preserves newer recovery revision");
Check(safeStore.Load(nativeTest).Completed.Contains("roof-right")&&safeStore.RecoveredLastLoad,"new recovery wins valid but stale primary for identical save hash");
Directory.Delete(blockedTemporary);
File.WriteAllText(Path.Combine(primaryTest,nativeHash+".json"),"corrupt");
Check(safeStore.Load(nativeTest).Completed.Contains("roof-left")&&safeStore.RecoveredLastLoad,"corrupt primary uses validated recovery");
File.WriteAllText(nativeTest,"different unsaved or vanilla snapshot");
Check(!safeStore.Load(nativeTest).Completed.Any()&&!safeStore.RecoveredLastLoad,"recovery never attaches to different save bytes");
File.WriteAllText(nativeTest,"native debit snapshot");File.WriteAllText(Path.Combine(recoveryTestRoot,nativeHash+".json"),"{}");
Reject(()=>safeStore.Load(nativeTest),"invalid recovery fails visibly rather than inventing progress");
foreach(var job in RepairModel.Jobs)
{
 Check(RepairSelectionRules.IsRoof(job.Id)==(job.Id=="roof-left"||job.Id=="roof-right"),"roof geometry rule uses stable ID "+job.Id);
 if(job.Id.StartsWith("window-")||job.Id=="door")Check(RepairSelectionRules.Priority(job.Id)<RepairSelectionRules.Priority("wall-a"),"detail wins underlying wall "+job.Id);
}
Check(RepairModel.Jobs.Reverse().OrderBy(j=>RepairSelectionRules.Priority(j.Id)).First().Id!="roof-left","selection priority independent of list order");

var dialogIds=new[]{1036,1045,1050,1052};var dialogRequired=new[]{10,5,0,0};var dialogOwned=new[]{260,146,75,52};
var dialogData=new RepairConfirmationData("window",dialogIds,dialogRequired,dialogOwned,500,87585,true);
dialogIds[0]=0;dialogRequired[0]=99;dialogOwned[0]=0;
Check(dialogData.MaterialIds[0]==1036&&dialogData.Required[0]==10&&dialogData.Owned[0]==260,"dialog snapshot is independent of caller arrays");
Check(dialogData.Fee==500&&dialogData.Money==87585&&dialogData.CanAccept,"dialog preserves supplied payment display without recalculating eligibility");
var unavailableDialog=new RepairConfirmationData("unavailable",new[]{1036},new[]{0},new[]{260},0,87585,false);
Check(!unavailableDialog.CanAccept,"dialog does not infer repair availability from abundant money or materials");
Reject(()=>new RepairConfirmationData("bad",new[]{1036},new[]{1,2},new[]{1},0,0,false),"dialog rejects mismatched required material display");
Reject(()=>new RepairConfirmationData("bad",new[]{1036},new[]{1},new int[0],0,0,false),"dialog rejects mismatched owned material display");
Reject(()=>new RepairConfirmationData("bad",null,new int[0],new int[0],0,0,false),"dialog rejects absent material identifiers");
var planRemaining = new[] { 0, 2, 5 };
var planRequired = new[] { 10, 5, 0, 0 };
var planOwned = new System.Collections.Generic.Dictionary<int,int> { [1036] = 260, [1052] = 52 };
var planData = new RepairPlanData(2, planRemaining, 10, "window", planRequired, 500, 87585, false, false, true, false, false, "", planOwned);
planRemaining[0] = 12; planRequired[0] = 99; planOwned[1036] = 0;
Check(planData.Remaining[0] == 0 && planData.Required[0] == 10 && planData.Owned(1036) == 260, "plan snapshot cannot change when caller collections mutate");
Check(planData.Owned(9999) == 0, "plan unknown wallet material safely displays zero");
Check(!planData.CanPay && planData.Selected == 2 && planData.Fee == 500 && planData.Money == 87585, "plan retains coordinator eligibility and selected cost");
Reject(() => new RepairPlanData(0, new int[0], 0, "bad", new int[3], 0, 0, false, false, false, false, false, "", planOwned), "plan rejects incomplete cost display");
// Localization is a display concern: switching language must not alter ledger IDs or costs.
var originalIds = jobs.Select(j => j.Id).ToArray();
var originalCosts = jobs.Select(j => RepairCostRules.FeeCopper(j,100)).ToArray();
RepairText.LanguageCode = () => "ja";
Check(RepairModel.Jobs[0].Name == "左側の屋根", "Japanese repair name resolves after static model initialization");
Check(RepairText.Format("repair.confirm-question", "屋根") == "屋根を修繕しますか？", "Japanese whole-sentence format");
RepairText.LanguageCode = () => "en";
Check(RepairModel.Jobs[0].Name == "Left roof", "same repair object follows English language setting");
Check(RepairText.Format("repair.confirm-question", "Left roof") == "Repair Left roof?", "English whole-sentence word order");
foreach(var language in new[]{ "ja", "ja-JP", "JA", "ja_JP" }) Check(RepairText.IsJapanese(language), "Japanese locale variant " + language);
foreach(var language in new[]{ "en", "fr", "zh-CN", "", null }) {
 RepairText.LanguageCode = () => language;
 Check(RepairText.Get("action.clean-counter") == "Wipe the counter", "unsupported/unset locale falls back to English " + language);
}
foreach(var language in new[]{ "ja", "en" }) {
 RepairText.LanguageCode = () => language;
 foreach(var job in jobs) Check(job.Name != "job."+job.Id && job.Name != job.Id, "translated repair name " + language + " " + job.Id);
 Check(RepairText.Get("mai.memory").Contains("\n") && RepairText.Get("mai.home").Contains("<shake>"), "dialogue line break and emotion tags " + language);
 Check(jobs.Select(j=>j.Id).SequenceEqual(originalIds) && jobs.Select(j=>RepairCostRules.FeeCopper(j,100)).SequenceEqual(originalCosts), "locale leaves stable repair IDs and costs unchanged " + language);
}
RepairText.LanguageCode = () => "en";

// External catalogs are optional, isolated mod data and loaded only at startup.
var translationRoot = Path.Combine(Path.GetTempPath(), "GradualRepairTranslations-" + Guid.NewGuid().ToString("N"));
var translationWarnings = new System.Collections.Generic.List<string>();
try {
 RepairText.Initialize(translationRoot, translationWarnings.Add);
 Check(File.Exists(Path.Combine(translationRoot,"ja.json")) && File.Exists(Path.Combine(translationRoot,"en.json")), "first initialization exports both embedded catalogs");
 Check(translationWarnings.Count == 0, "exported defaults load without warnings");
 string jaOriginal = File.ReadAllText(Path.Combine(translationRoot,"ja.json"));
 File.WriteAllText(Path.Combine(translationRoot,"en.json"), "{\"action.close\":\"Leave\"}");
 File.WriteAllText(Path.Combine(translationRoot,"fr.json"), "{\"action.close\":\"Fermer\",\"repair.confirm-question\":\"Réparer {0} ?\",\"action.confirm-pay\":3,\"action.clean-counter\":\"\"}");
 File.WriteAllText(Path.Combine(translationRoot,"fr-CA.json"), "{\"action.close\":\"Fermer ici\"}");
 File.WriteAllText(Path.Combine(translationRoot,"it.json"), "{\"repair.confirm-question\":\"Broken {5}\"}");
 File.WriteAllText(Path.Combine(translationRoot,"de.json"), "{bad json");
 File.WriteAllText(Path.Combine(translationRoot,"es.json"), "{\"action.close\":\"a\",\"action.close\":\"b\"}");
 RepairText.Initialize(translationRoot, translationWarnings.Add);
 Check(File.ReadAllText(Path.Combine(translationRoot,"en.json")) == "{\"action.close\":\"Leave\"}" && File.ReadAllText(Path.Combine(translationRoot,"ja.json")) == jaOriginal, "initialization never overwrites existing translations");
 RepairText.LanguageCode = () => "fr_CA";
 Check(RepairText.Get("action.close") == "Fermer ici", "exact regional translation overrides base language");
 Check(RepairText.Format("repair.confirm-question","Mur") == "Réparer Mur ?", "missing regional entry inherits base language");
 Check(RepairText.Get("action.confirm-pay") == "Confirm / pay" && RepairText.Get("action.clean-counter") == "Wipe the counter", "non-string and empty translations fall back safely");
 RepairText.LanguageCode = () => "it";
 Check(RepairText.Format("repair.confirm-question","Roof") == "Repair Roof?", "invalid format placeholder falls back without UI exception");
 RepairText.LanguageCode = () => "de";
 Check(RepairText.Get("action.close") == "Leave", "malformed catalog falls back to external English");
 RepairText.LanguageCode = () => "es";
 Check(RepairText.Get("action.close") == "Leave", "duplicate JSON keys reject the ambiguous catalog");
 RepairText.LanguageCode = () => "ja-JP";
 Check(RepairText.Get("action.close") == "閉じる", "Japanese defaults remain available with external catalogs");
 Check(RepairText.NormalizeLocale("../en") == "" && RepairText.NormalizeLocale("ja/../en") == "", "unsafe locale strings are not filenames");
 Check(translationWarnings.Count >= 5, "invalid external entries and files report diagnostic warnings");
 File.Delete(Path.Combine(translationRoot,"fr-CA.json"));
 RepairText.LanguageCode = () => "fr-CA";
 Check(RepairText.Get("action.close") == "Fermer ici", "display lookup uses cached catalog without filesystem polling");
 var occupied = Path.Combine(translationRoot,"occupied"); File.WriteAllText(occupied,"file");
 RepairText.Initialize(occupied, translationWarnings.Add);
 Check(RepairText.Get("action.close") == "Close", "unavailable translation directory retains embedded fallback");
} finally {
 RepairText.LanguageCode = () => "en";
 // This unique temporary test directory is created and owned above.
 if (Directory.Exists(translationRoot)) Directory.Delete(translationRoot,true);
}

Console.WriteLine($"PASS checks={checks}");

