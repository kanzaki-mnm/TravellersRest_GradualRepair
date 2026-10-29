using System.Linq;
// Native access is resolved once, before patching. Structural fallback requires unique exact matches.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using HarmonyLib;
namespace GradualRepair
{
    internal static class NativeGame
    {
        internal const string OldHash = "2EEEC7A684B07ED55B21E03E3B7B57197D5D546A51D047EDF9A7017F22CB51EC";
        internal const string CurrentHash = "1F436352772CDFC29C1321C112A695A543582E3A0CF27746B9A9385A6E560CAC";
        static readonly Dictionary<string, string> hotfixNames = new Dictionary<string, string>{
{"Item.DNDLBCBNIIA","HJOGLLDMJGC"},
{"WorldGrid.OANIHJGGBFG","NLGMFDJAOPP"},
{"DecorationTile.GIPNGINGGMC","FBMPMOGKAGP"},
{"Utils.GIFFGJACLCO","OGOKJJMODBJ"},
{"WorldTile.JBAJFGBMKKB","KDJDDHOMGEJ"},
{"WorldGrid.ILBKKPFDBKN","HOBCAACPIFI"},
{"WorldGrid.HOGKCMELOGO","GDJENLJAJMG"},
{"WorldGrid.DCHIPHNCGAN","FHFHDFNFPJE"},
{"WorldGrid.GKCOFDIGDOJ","HFAOKNJHHIN"},
{"WorldGrid.LGCMAPBKJKF","EGFOGCHAOEA"},
{"WorldGrid.HGBKAOHLBHO","HOBBBELDFKP"},
{"WorldGrid.IOEFEOEFIAL","IGCGODDCKFD"},
{"Utils.PBCJHNPKLEH","MGPEKLFFCJC"},
{"WorldGrid.GOCNKDLCFLM","LHIPHIFKEPM"},
{"WorldGrid.JKGHOPIILPJ","LJLEFEJEODH"},
{"WorldGrid.CNKKDCOIDFO","GDOFNFEMFPJ"},
{"FloorLimitsManager.JGCDFKNOFGM","KJOAFCLDHGI"},
{"FloorLimitsManager.IMNMDKPFCFJ","NOIJLHIAJMP"},
{"TavernConstructionUI.GPKBBNLLGDA","DLGAPCLEABK"},
{"DecorationTile.NCCCDENANMF","LBCCEMLFEIE"},
{"SaveData.GKABKNKACBN","OHHENIOLFCE"},
{"Q301ManoDePintura_Paso2.PGKPCJDLMJI","BNLCHJFLDPM"},
{"GameManager.LNEIIDPIDFP","GIJAPHGLOHM"},
{"CommonReferences.LNEIIDPIDFP","GIJAPHGLOHM"},
{"SeasonManager.LMINKIOLECO","BJPKKHMIPEK"},
{"GameManager.PAAHJHJKKBN","OBPBHOBKEPB"},
{"CharacterAnimation.PKGDJCDLADP","MHAFALLMHHK"},
{"NewTutorialManager.AODHDJFHPLJ","EFHPIELJILD"},
{"CustomerStateSpawning.JLBKENOBLND","ICBLLIHDDMK"},
{"WorldTime.get_currentGameDateMin","get_currentGameDateMin"},
{"TavernManager.MBGCOJHGANE","BIMPKPHKBPD"},
{"TavernManager.IGENDKACIKK","PLMEGPBPMKC"},
{"NewTutorialManager.DirtLevelBlocked","DirtLevelBlocked"},
{"TavernManager.LNEIIDPIDFP","GIJAPHGLOHM"},
{"HoverObject.LAOBLFKKALG","NNMBHNOKJON"},
{"CustomerStateBase.ALGFFFCODAN","OMMILFKDIDM"},
};
        static readonly Dictionary<string, string> laterNames = new Dictionary<string, string> {
            {"Item.DNDLBCBNIIA", "BDONINELJBG"},
            {"WorldGrid.OANIHJGGBFG", "CIFMBADOKCJ"},
            {"DecorationTile.GIPNGINGGMC", "OAMGIOAABPJ"},
            {"Utils.GIFFGJACLCO", "PIHOHCDOCGD"},
            {"WorldTile.JBAJFGBMKKB", "ENGDIGIHBHK"},
            {"WorldGrid.ILBKKPFDBKN", "OOMPLGODFAA"},
            {"WorldGrid.HOGKCMELOGO", "LAJANKHGGHG"},
            {"WorldGrid.DCHIPHNCGAN", "PHJJFBEMICE"},
            {"WorldGrid.GKCOFDIGDOJ", "JCBCNIMAGLM"},
            {"WorldGrid.LGCMAPBKJKF", "DDOKDNHGEBL"},
            {"WorldGrid.HGBKAOHLBHO", "DIDJLGKGDNH"},
            {"WorldGrid.IOEFEOEFIAL", "LFEFFONCOFM"},
            {"Utils.PBCJHNPKLEH", "MHJJGJFAMBC"},
            {"WorldGrid.GOCNKDLCFLM", "DECNJHLKFGM"},
            {"WorldGrid.JKGHOPIILPJ", "NPAGAMDIDEJ"},
            {"WorldGrid.CNKKDCOIDFO", "JKOAIILLFHJ"},
            {"FloorLimitsManager.JGCDFKNOFGM", "PODCLJFCFAO"},
            {"FloorLimitsManager.IMNMDKPFCFJ", "OIBIHOKNJJM"},
            {"TavernConstructionUI.GPKBBNLLGDA", "HFDMGPGNOAG"},
            {"DecorationTile.NCCCDENANMF", "NDMLGJJAFOG"},
            {"SaveData.GKABKNKACBN", "IDMCKMPCAFN"},
            {"Q301ManoDePintura_Paso2.PGKPCJDLMJI", "PECBGNINEDF"},
            {"GameManager.LNEIIDPIDFP", "IDACOPPOKLB"},
            {"CommonReferences.LNEIIDPIDFP", "IDACOPPOKLB"},
            {"SeasonManager.LMINKIOLECO", "FKNIGFGOBHO"},
            {"GameManager.PAAHJHJKKBN", "EJIANNKMNII"},
            {"CharacterAnimation.PKGDJCDLADP", "CKLJOLPBBAM"},
            {"NewTutorialManager.AODHDJFHPLJ", "EOBAGAFEBLJ"},
            {"CustomerStateSpawning.JLBKENOBLND", "ICCEAECHCKF"},
            {"WorldTime.get_currentGameDateMin", "get_currentGameDateMin"},
            {"TavernManager.MBGCOJHGANE", "HCLALBLPIJC"},
            {"TavernManager.IGENDKACIKK", "PBOJHHEACHL"},
            {"NewTutorialManager.DirtLevelBlocked", "DirtLevelBlocked"},
            {"TavernManager.LNEIIDPIDFP", "IDACOPPOKLB"},
            {"HoverObject.LAOBLFKKALG", "FLABELIHEGB"},
            {"CustomerStateBase.ALGFFFCODAN", "AKNBMCBIIIB"},
            {"MaiNPC.MOOHALBHKBC", "JKONHIGIKKO"},
            {"MaiNPC.GBAIJFGGCCB", "BPMGMBPAMFI"},
            {"MaiNPC.PBDHLHCNKNG", "NLMBGKBFGCI"},
            {"MaiNPC.DONJOEHCNHD", "GEECOCBMLAB"},
        };
        static readonly Dictionary<string, string> structuralNames = new Dictionary<string, string>();
        static string Name(Type type, string oldName, string newName) => Profile == "structural" ? structuralNames[type.Name + "." + newName] : Profile == "0.7.6.14.0-hotfix-later" ? laterNames[type.Name + "." + newName] : Profile == "0.7.6.13.0" ? oldName : Profile == "0.7.6.14.0-hotfix-20261028" ? hotfixNames[type.Name + "." + newName] : newName;
        internal static bool Ready { get; private set; }
        internal static string Profile { get; private set; }
        static readonly Dictionary<string, MethodInfo> hooks = new Dictionary<string, MethodInfo>();
        internal static string ReadHash(string path) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        internal static string AssemblyHash { get; private set; }
        internal static void Initialize()
        {
            Ready = false;
            // Hashes are diagnostics only. An unrelated patch must not disable the mod.
            try { AssemblyHash = ReadHash(typeof(Save).Assembly.Location); } catch { AssemblyHash = "unavailable"; }
            var failures = new List<Exception>();
            foreach (var profile in new[] { "0.7.6.14.0-hotfix-later", "0.7.6.14.0-hotfix-20261028", "0.7.6.14.0", "0.7.6.13.0", "structural" })
            {
                hooks.Clear(); Ready = false; Profile = profile;
                try { if (profile == "structural") ResolveStructuralContracts(); BindContracts(); InitializeMaiContracts(); return; }
                catch (Exception ex) { failures.Add(new InvalidOperationException(profile + ": " + ex.Message, ex)); }
            }
            hooks.Clear(); Ready = false;
            throw new AggregateException("Required native member contracts could not be resolved. No save data was changed.", failures);
        }
        static void ResolveStructuralContracts()
        {
            structuralNames.Clear();
            using (var stream = typeof(NativeGame).Assembly.GetManifestResourceStream("GradualRepair.NativeContracts.json"))
            using (var reader = new StreamReader(stream ?? throw new MissingMemberException("Native structural contracts resource missing")))
            using (var native = Mono.Cecil.AssemblyDefinition.ReadAssembly(typeof(Save).Assembly.Location))
            {
                var contracts = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NativeContract>>(reader.ReadToEnd());
                if (contracts == null || contracts.Count != 40) throw new InvalidDataException("Native structural contracts are incomplete");
                var resolver = new NativeContractResolver(native.MainModule);
                foreach (var contract in contracts) structuralNames.Add(contract.Key, resolver.Resolve(contract));
            }
        }
        static void BindContracts()
        {
            Bind("NewItemInstance", typeof(Item), "LPAAGMDGOKD", "DNDLBCBNIIA", typeof(ItemInstance), false, Type.EmptyTypes);
            Bind("TryGetWorldTile", typeof(WorldGrid), "KKLLAIGKKCK", "OANIHJGGBFG", typeof(System.Boolean), true, new Type[] { typeof(UnityEngine.Vector3), typeof(WorldTile).MakeByRefType() });
            TryGetWorldTileImpl = (TryGetWorldTileCall)Delegate.CreateDelegate(typeof(TryGetWorldTileCall), hooks["TryGetWorldTile"]);
            Bind("DrawDecoration", typeof(DecorationTile), "KPEEMDDHLHJ", "GIPNGINGGMC", typeof(void), false, new Type[] { typeof(UnityEngine.Vector3), typeof(UnityEngine.Tilemaps.Tilemap), typeof(System.Single), typeof(System.Single), typeof(System.Boolean), typeof(System.Boolean) });
            DrawDecorationImpl = (DrawDecorationCall)Delegate.CreateDelegate(typeof(DrawDecorationCall), hooks["DrawDecoration"]);
            Bind("GetScene", typeof(Utils), "EKBBPGDJDAA", "GIFFGJACLCO", typeof(TilemapScene), true, new Type[] { typeof(Location) });
            GetSceneImpl = (GetSceneCall)Delegate.CreateDelegate(typeof(GetSceneCall), hooks["GetScene"]);

            Bind("RefreshWorldTile", typeof(WorldGrid), "JNKBAMGCPMI", "ILBKKPFDBKN", typeof(void), true, new Type[] { typeof(WorldTile), typeof(UnityEngine.Vector3), typeof(TilemapScene), typeof(Season), typeof(System.Boolean) });
            RefreshWorldTileImpl = (RefreshWorldTileCall)Delegate.CreateDelegate(typeof(RefreshWorldTileCall), hooks["RefreshWorldTile"]);
            Bind("HasWall", typeof(WorldGrid), "BHAHGBNDIBP", "HOGKCMELOGO", typeof(System.Boolean), true, new Type[] { typeof(UnityEngine.Vector3), typeof(TilemapScene) });
            HasWallImpl = (HasWallCall)Delegate.CreateDelegate(typeof(HasWallCall), hooks["HasWall"]);
            Bind("RefreshTile", typeof(WorldGrid), "FIBEJCMNCNH", "DCHIPHNCGAN", typeof(void), true, new Type[] { typeof(UnityEngine.Vector3), typeof(TileType), typeof(TilemapScene) });
            RefreshTileImpl = (RefreshTileCall)Delegate.CreateDelegate(typeof(RefreshTileCall), hooks["RefreshTile"]);
            Bind("RefreshWalls", typeof(WorldGrid), "POCMCIGDMHP", "GKCOFDIGDOJ", typeof(void), true, new Type[] { typeof(TilemapScene), typeof(UnityEngine.Vector3Int[]) });
            RefreshWallsImpl = (RefreshWallsCall)Delegate.CreateDelegate(typeof(RefreshWallsCall), hooks["RefreshWalls"]);
            Bind("GetFloorDecoration", typeof(WorldGrid), "GONFDINNAII", "LGCMAPBKJKF", typeof(System.Int32), true, new Type[] { typeof(UnityEngine.Vector3), typeof(TilemapScene) });
            GetFloorDecorationImpl = (GetFloorDecorationCall)Delegate.CreateDelegate(typeof(GetFloorDecorationCall), hooks["GetFloorDecoration"]);
            Bind("GetWallDecoration", typeof(WorldGrid), "DMOIKFIFAEK", "HGBKAOHLBHO", typeof(System.Int32), true, new Type[] { typeof(UnityEngine.Vector3), typeof(TilemapScene) });
            GetWallDecorationImpl = (GetWallDecorationCall)Delegate.CreateDelegate(typeof(GetWallDecorationCall), hooks["GetWallDecoration"]);
            Bind("GetGroundType", typeof(WorldGrid), "JFFKAFGFAEO", "IOEFEOEFIAL", typeof(GroundType), true, new Type[] { typeof(UnityEngine.Vector3) });
            GetGroundTypeImpl = (GetGroundTypeCall)Delegate.CreateDelegate(typeof(GetGroundTypeCall), hooks["GetGroundType"]);
            Bind("SnapToGrid", typeof(Utils), "JECBBILCELI", "PBCJHNPKLEH", typeof(UnityEngine.Vector3), true, new Type[] { typeof(UnityEngine.Vector3) });
            SnapToGridImpl = (SnapToGridCall)Delegate.CreateDelegate(typeof(SnapToGridCall), hooks["SnapToGrid"]);
            Bind("PaintWall", typeof(WorldGrid), "BENMAGFBJCA", "GOCNKDLCFLM", typeof(void), true, new Type[] { typeof(UnityEngine.Vector3), typeof(System.Int32) });
            Bind("PaintFloor", typeof(WorldGrid), "JBNBBPFBNFM", "JKGHOPIILPJ", typeof(void), true, new Type[] { typeof(UnityEngine.Vector3), typeof(System.Int32), typeof(System.Boolean) });
            Bind("PaintTrim", typeof(WorldGrid), "JPCODJEHIBD", "CNKKDCOIDFO", typeof(void), true, new Type[] { typeof(UnityEngine.Vector3), typeof(System.Int32) });
            Bind("UpdateFloorEdge", typeof(FloorLimitsManager), "BODEKBIHIBG", "JGCDFKNOFGM", typeof(void), true, new Type[] { typeof(UnityEngine.Vector3), typeof(TilemapScene) });
            Bind("FloorEdgeNeighbor", typeof(FloorLimitsManager), "HADOCFMOHJH", "IMNMDKPFCFJ", typeof(System.Boolean), true, new Type[] { typeof(UnityEngine.Vector3), typeof(DecorationTile), typeof(TilemapScene) });
            Bind("SelectBuildingFloor", typeof(TavernConstructionUI), "NDLFCJBGOMB", "GPKBBNLLGDA", typeof(void), false, new Type[] { typeof(TavernFloor) });
            Bind("DecorationName", typeof(DecorationTile), "LHFPDMKCOJH", "NCCCDENANMF", typeof(System.String), false, new Type[] { });
            Bind("LoadWorld", typeof(SaveData), "PBJCPCPHIKD", "GKABKNKACBN", typeof(void), false, new Type[] { });
            Bind("FinishMai", typeof(Q301ManoDePintura_Paso2), "GHNCNAIFHAF", "PGKPCJDLMJI", typeof(void), true, new Type[] { typeof(System.Boolean) });
            GameImpl = (Func<GameManager>)BindProperty(typeof(GameManager), "GDCGNEJHDOO", "LNEIIDPIDFP", typeof(GameManager), true, typeof(Func<GameManager>));
            ReferencesImpl = (Func<CommonReferences>)BindProperty(typeof(CommonReferences), "GDCGNEJHDOO", "LNEIIDPIDFP", typeof(CommonReferences), true, typeof(Func<CommonReferences>));
            CurrentSeasonImpl = (Func<Season>)BindProperty(typeof(SeasonManager), "EELAPGAJIMM", "LMINKIOLECO", typeof(Season), true, typeof(Func<Season>));
            WorldLoadedImpl = (Func<GameManager, bool>)BindProperty(typeof(GameManager), "BBEMCMDNHIJ", "PAAHJHJKKBN", typeof(bool), false, typeof(Func<GameManager, bool>));
            FacingImpl = (Func<CharacterAnimation, Direction>)BindProperty(typeof(CharacterAnimation), "BAGJHGCEKCP", "PKGDJCDLADP", typeof(Direction), false, typeof(Func<CharacterAnimation, Direction>));
            TutorialRunningImpl = (Func<bool>)BindProperty(typeof(NewTutorialManager), "DGODGEMBCFJ", "AODHDJFHPLJ", typeof(bool), true, typeof(Func<bool>));
            hovered = AccessTools.Property(typeof(HoverObject), Name(typeof(HoverObject), "BHEEAGLANHE", "LAOBLFKKALG")) ?? throw new MissingMemberException("Hovered object property");
            if (hovered.PropertyType != typeof(IHoverable) || hovered.GetGetMethod(true) == null || hovered.GetGetMethod(true).IsStatic || hovered.GetIndexParameters().Length != 0) throw new MissingMemberException("Hovered object property contract");
            Bind("VisitorAdmission", typeof(CustomerStateSpawning), "CMLNGFBHKGA", "JLBKENOBLND", typeof(bool), false, Type.EmptyTypes);
            Bind("GameMinutes", typeof(WorldTime), "get_currentGameDateMin", "get_currentGameDateMin", typeof(ulong), true, Type.EmptyTypes);
            spawningCustomer = AccessTools.DeclaredField(typeof(CustomerStateBase), Name(typeof(CustomerStateBase), "GFGCDHJLCLJ", "ALGFFFCODAN"));
            if (spawningCustomer == null || spawningCustomer.FieldType != typeof(Customer) || spawningCustomer.IsStatic) throw new MissingFieldException("Spawning customer binding failed");
            BindSetter("SetDirtScore", typeof(TavernManager), "BIANMIBOGLA", "MBGCOJHGANE", typeof(float));
            BindSetter("SetDirtLevel", typeof(TavernManager), "JKHPFPAGKLO", "IGENDKACIKK", typeof(DirtLevel));
            Bind("DirtLevelBlocked", typeof(NewTutorialManager), "DirtLevelBlocked", "DirtLevelBlocked", typeof(bool), true, Type.EmptyTypes);
            DirtBlockedImpl = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), hooks["DirtLevelBlocked"]);
            DirtLevelSetter = (Action<TavernManager, DirtLevel>)Delegate.CreateDelegate(typeof(Action<TavernManager, DirtLevel>), hooks["SetDirtLevel"]);
            TavernImpl = (Func<TavernManager>)BindProperty(typeof(TavernManager), "GDCGNEJHDOO", "LNEIIDPIDFP", typeof(TavernManager), true, typeof(Func<TavernManager>));
            DirtScoreImpl = (Func<TavernManager, float>)BindProperty(typeof(TavernManager), "BIANMIBOGLA", "MBGCOJHGANE", typeof(float), false, typeof(Func<TavernManager, float>));
            DirtLevelImpl = (Func<TavernManager, DirtLevel>)BindProperty(typeof(TavernManager), "JKHPFPAGKLO", "IGENDKACIKK", typeof(DirtLevel), false, typeof(Func<TavernManager, DirtLevel>));
            Ready = true;
        }
        static void BindSetter(string key, Type type, string oldName, string newName, Type valueType)
        {
            var property = AccessTools.DeclaredProperty(type, Name(type, oldName, newName));
            var setter = property?.GetSetMethod(true);
            if (property?.PropertyType != valueType || setter == null || setter.IsStatic || setter.ReturnType != typeof(void) || setter.GetParameters().Length != 1 || setter.GetParameters()[0].ParameterType != valueType) throw new MissingMemberException("Native compatibility setter failed: " + key);
            hooks.Add(key, setter);
        }
        static void Bind(string key, Type type, string oldName, string newName, Type result, bool isStatic, Type[] parameters)
        {
            var name = Name(type, oldName, newName);
            var method = AccessTools.DeclaredMethod(type, name, parameters);
            if (method == null || method.ReturnType != result || method.IsStatic != isStatic || method.ContainsGenericParameters || !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters)) throw new MissingMethodException("Native compatibility binding failed: " + key + " / " + type.FullName + "." + name);
            hooks.Add(key, method);
        }
        static Func<MaiNPC> maiInstance;
        internal static MaiNPC Mai => maiInstance();
        static MethodInfo maiConversation, maiAvailable, maiOpened;
        static FieldInfo maiFlag;
        internal static void InitializeMaiContracts()
        {
            string Resolve(string name) => Profile == "structural" ? structuralNames["MaiNPC." + name] : Profile == "0.7.6.14.0-hotfix-later" ? laterNames["MaiNPC." + name] : name;
            MethodInfo Require(string name, Type result, params Type[] args)
            {
                var method = AccessTools.DeclaredMethod(typeof(MaiNPC), Resolve(name), args);
                if (method == null || method.ReturnType != result || method.IsStatic) throw new MissingMethodException("Mai contract: " + name);
                return method;
            }
            var singleton = AccessTools.DeclaredProperty(typeof(MaiNPC), Profile == "structural" ? structuralNames["MaiNPC.Singleton"] : Profile == "0.7.6.14.0-hotfix-later" ? "IDACOPPOKLB" : "GIJAPHGLOHM");
            var getter = singleton?.GetGetMethod(true);
            if (singleton?.PropertyType != typeof(MaiNPC) || getter == null || !getter.IsStatic || getter.GetParameters().Length != 0) throw new MissingMemberException("Mai singleton contract");
            maiInstance = (Func<MaiNPC>)Delegate.CreateDelegate(typeof(Func<MaiNPC>), getter);
            maiConversation = Require("MOOHALBHKBC", typeof(void));
            maiAvailable = Require("GBAIJFGGCCB", typeof(bool));
            maiOpened = Require("PBDHLHCNKNG", typeof(void), typeof(int));
            maiFlag = AccessTools.DeclaredField(typeof(MaiNPC), Resolve("DONJOEHCNHD"));
            if (maiFlag == null || maiFlag.FieldType != typeof(bool) || maiFlag.IsStatic) throw new MissingFieldException("Mai visit flag contract");
        }
        internal static MethodInfo MaiConversation => maiConversation;
        internal static MethodInfo MaiAvailable => maiAvailable;
        internal static MethodInfo MaiOpened => maiOpened;
        internal static bool MaiFlag(MaiNPC mai) => (bool)maiFlag.GetValue(mai);
        internal static void ClearMaiFlag(MaiNPC mai) => maiFlag.SetValue(mai, false);
        static FieldInfo spawningCustomer;
        internal static Customer SpawningCustomer(CustomerStateSpawning state) => (Customer)spawningCustomer.GetValue(state);
        internal static ulong GameMinutes => (ulong)hooks["GameMinutes"].Invoke(null, null);
        internal static MethodInfo Hook(string key) => Ready ? hooks[key] : throw new InvalidOperationException("Native compatibility has not passed validation.");
        delegate System.Boolean TryGetWorldTileCall(UnityEngine.Vector3 p0, out WorldTile p1);
        static TryGetWorldTileCall TryGetWorldTileImpl;
        internal static System.Boolean TryGetWorldTile(UnityEngine.Vector3 p0, out WorldTile p1) => TryGetWorldTileImpl(p0, out p1);
        delegate void DrawDecorationCall(DecorationTile instance, UnityEngine.Vector3 p0, UnityEngine.Tilemaps.Tilemap p1, System.Single p2, System.Single p3, System.Boolean p4, System.Boolean p5);
        static DrawDecorationCall DrawDecorationImpl;
        internal static void DrawDecoration(DecorationTile instance, UnityEngine.Vector3 p0, UnityEngine.Tilemaps.Tilemap p1, System.Single p2 = 0f, System.Single p3 = 0f, System.Boolean p4 = true, System.Boolean p5 = true) => DrawDecorationImpl(instance, p0, p1, p2, p3, p4, p5);
        delegate TilemapScene GetSceneCall(Location p0);
        static GetSceneCall GetSceneImpl;
        internal static TilemapScene GetScene(Location p0) => GetSceneImpl(p0);
        internal static TilemapScene GetTileScene(WorldTile instance) => GetScene(instance.location);
        delegate void RefreshWorldTileCall(WorldTile p0, UnityEngine.Vector3 p1, TilemapScene p2, Season p3, System.Boolean p4);
        static RefreshWorldTileCall RefreshWorldTileImpl;
        internal static void RefreshWorldTile(WorldTile p0, UnityEngine.Vector3 p1, TilemapScene p2, Season p3, System.Boolean p4 = true) => RefreshWorldTileImpl(p0, p1, p2, p3, p4);
        delegate System.Boolean HasWallCall(UnityEngine.Vector3 p0, TilemapScene p1);
        static HasWallCall HasWallImpl;
        internal static System.Boolean HasWall(UnityEngine.Vector3 p0, TilemapScene p1 = null) => HasWallImpl(p0, p1);
        delegate void RefreshTileCall(UnityEngine.Vector3 p0, TileType p1, TilemapScene p2);
        static RefreshTileCall RefreshTileImpl;
        internal static void RefreshTile(UnityEngine.Vector3 p0, TileType p1, TilemapScene p2) => RefreshTileImpl(p0, p1, p2);
        delegate void RefreshWallsCall(TilemapScene p0, UnityEngine.Vector3Int[] p1);
        static RefreshWallsCall RefreshWallsImpl;
        internal static void RefreshWalls(TilemapScene p0, UnityEngine.Vector3Int[] p1 = null) => RefreshWallsImpl(p0, p1);
        delegate System.Int32 GetFloorDecorationCall(UnityEngine.Vector3 p0, TilemapScene p1);
        static GetFloorDecorationCall GetFloorDecorationImpl;
        internal static System.Int32 GetFloorDecoration(UnityEngine.Vector3 p0, TilemapScene p1) => GetFloorDecorationImpl(p0, p1);
        delegate System.Int32 GetWallDecorationCall(UnityEngine.Vector3 p0, TilemapScene p1);
        static GetWallDecorationCall GetWallDecorationImpl;
        internal static System.Int32 GetWallDecoration(UnityEngine.Vector3 p0, TilemapScene p1 = null) => GetWallDecorationImpl(p0, p1);
        delegate GroundType GetGroundTypeCall(UnityEngine.Vector3 p0);
        static GetGroundTypeCall GetGroundTypeImpl;
        internal static GroundType GetGroundType(UnityEngine.Vector3 p0) => GetGroundTypeImpl(p0);
        delegate UnityEngine.Vector3 SnapToGridCall(UnityEngine.Vector3 p0);
        static SnapToGridCall SnapToGridImpl;
        internal static UnityEngine.Vector3 SnapToGrid(UnityEngine.Vector3 p0) => SnapToGridImpl(p0);
        static Func<TavernManager> TavernImpl;
        static Func<bool> DirtBlockedImpl;
        internal static bool DirtLevelBlocked => DirtBlockedImpl();
        internal static TavernManager Tavern => TavernImpl();
        static Func<TavernManager, float> DirtScoreImpl;
        internal static float DirtScore(TavernManager tavern) => DirtScoreImpl(tavern);
        static Func<TavernManager, DirtLevel> DirtLevelImpl;
        internal static DirtLevel DirtLevel(TavernManager tavern) => DirtLevelImpl(tavern);
        static Action<TavernManager, DirtLevel> DirtLevelSetter;
        internal static void SetDirtLevel(TavernManager tavern, DirtLevel level) => DirtLevelSetter(tavern, level);
        static Func<GameManager> GameImpl;
        internal static GameManager Game => GameImpl();
        static Func<CommonReferences> ReferencesImpl;
        internal static CommonReferences References => ReferencesImpl();
        static Func<Season> CurrentSeasonImpl;
        internal static Season CurrentSeason => CurrentSeasonImpl();
        static Func<GameManager, bool> WorldLoadedImpl;
        internal static bool WorldLoaded(GameManager instance) => WorldLoadedImpl(instance);
        static Func<CharacterAnimation, Direction> FacingImpl;
        internal static Direction Facing(CharacterAnimation instance) => FacingImpl(instance);
        static Func<bool> TutorialRunningImpl;
        internal static bool TutorialRunning => TutorialRunningImpl();
        static PropertyInfo hovered; internal static object Hovered(HoverObject instance) => instance == null ? null : hovered.GetValue(instance, null);
        static Delegate BindProperty(Type type, string oldName, string newName, Type result, bool isStatic, Type delegateType)
        {
            var property = AccessTools.Property(type, Name(type, oldName, newName));
            var getter = property?.GetGetMethod(true);
            if (getter == null || getter.IsStatic != isStatic || getter.ReturnType != result || getter.GetParameters().Length != 0) throw new MissingMemberException("Native compatibility property failed: " + type.Name + "." + oldName);
            return Delegate.CreateDelegate(delegateType, getter);
        }
    }
}


