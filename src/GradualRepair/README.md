# GradualRepair source layout

The runtime entry point is `Core/Plugin.cs`. Most runtime files remain parts of
the same `Plugin` partial class; folders describe features, not isolated services.
This organization deliberately preserves lifecycle and Harmony callback ordering.

| Folder | Responsibility |
| --- | --- |
| Core | Startup/update orchestration, repair model and costs, save ledger and cached health report |
| Localization | Japanese/English JSON catalogs and mod-owned display text lookup |
| Compatibility | Native game contract resolution |
| Interaction | Native/mod input ownership and gesture state |
| Interior | Counter/debris cleanup, salvage delivery, indoor repair targets and geometry |
| Building | Aged decoration choices, initial materials, floor edges and door geometry |
| Exterior | Exterior rendering, preview, repair list/panel, controls, mailbox entry and feedback |
| UI | Confirmation dialog shared by indoor and exterior repairs, including template warmup |
| Gameplay | Hygiene/traffic integration and pure rules, Mai dialogue/visits |

Pure rules and persistence remain separate files so `GradualRepairChecks` can
compile them without the game runtime. That project explicitly links their paths;
update its project file when moving them.

## Safety boundaries

- Keep native save IDs and mod-only state/recovery storage separate.
- Preserve vanilla fallback IDs for aged building decorations.
- Preserve interaction ownership and dialog close/cleanup ordering.
- Reuse cloned native UI presentation; do not attach mod listeners to live vanilla controls.
- Avoid global object scans in recurring update paths.

## Validation

Optional Mod Manager V1 diagnostics and their limits are documented in [HEALTH.md](HEALTH.md).

From the workspace root:

```powershell
dotnet run --project src/GradualRepairChecks/GradualRepairChecks.csproj --no-restore
dotnet build src/GradualRepair/GradualRepair.csproj -c Release --no-restore '-p:GameDir=C:\Program Files (x86)\Steam\steamapps\common\Travellers Rest\Windows'
```

The explicit game directory uses the current Steam assemblies rather than the
older sandbox default. Historical runtime probes must be checked against the
current game contracts before use.

The first structural refactor maps 47 files to 35 without changing method bodies,
types, namespaces, configuration, save schema or runtime behavior. Evidence and
the old-to-new file map are in `research/gradual-repair/refactor-structure/`.
Whitespace formatting was applied after the structural change and checked against
the original declaration/body text. Extracting feature classes from `Plugin`
is being performed incrementally.

The shared confirmation dialog now has its own `UI/RepairConfirmationView.cs`.
It owns cloned native UI, template warmup, display refresh and disposal.
`RepairConfirmationData` supplies a display snapshot; the small
`RepairConfirmationCoordinator` adapter keeps eligibility, payment and repair
progress in `Plugin`. Closing the view never writes save data or pays costs.
Evidence is in `research/gradual-repair/refactor-confirmation/`.

The exterior plan now has its own `UI/RepairPlanView.cs` for native list,
wallet, compact cost, guide and modal-background presentation. `RepairPlanData`
is a display snapshot; `Exterior/RepairPlanCoordinator.cs` retains selection,
eligibility and callbacks into the existing repair flow. Camera rendering,
input-map ownership, time-scale restoration, payment and persistence remain
outside the view. Historical `RepairPanel.cs` and `RepairList.cs` are consolidated
into this view/coordinator boundary. Evidence: `research/gradual-repair/refactor-plan-ui/`.

Repair input-map switching and time-scale ownership now live in the independent
`Interaction/RepairSessionControls.cs` class. Plugin owns one instance and retains
the existing open/close, movement-lock and WorldTime ordering. The six native
control method bodies are unchanged, including the mailbox UI-to-Game restoration
rule and conditional restoration of the previous time scale. The old exterior
partial file was moved, so this extraction does not increase the file count.
Evidence: `research/gradual-repair/refactor-session-controls/`.

Preview camera, RenderTexture, RawImage, outline sprite cache and temporary
material/sprite restoration are owned by `UI/RepairPreviewView.cs`.
`Exterior/FieldRepairView.cs` now coordinates target bounds, dirty revisions,
mouse/controller selection and calls into the existing repair flow. Interior
framing supplies bounds to the view; no camera/resource fields remain in Plugin.
The existing repaint gate still exits before resolving the native outline material.
Evidence: `research/gradual-repair/refactor-preview/`.

## Localization

Edit `Localization/ja.json` and `Localization/en.json` together, preserving keys,
format placeholders, and dialogue tags. Both catalogs are embedded in the DLL.
`RepairText` reads the current language code through a runtime callback set to
`I2.Loc.LocalizationManager.CurrentLanguageCode`. Startup exports missing defaults to `BepInEx/config/GradualRepair/Localization`
and caches validated optional overrides. Lookup uses exact locale, base locale,
then embedded Japanese (for `ja`) or external/embedded English. Filesystem access
occurs only during initialization; locale normalization is cached between changes. No OS locale,
new configuration value, or native translation database mutation is involved.

Repair IDs, asset paths, item IDs and save keys stay language-independent. Job
names resolve on access, and all three custom Mai lines resolve through their
conversation/entry IDs so a conversation registered in another language does not
retain stale text. Native item labels and yes/no buttons retain native localization.

Existing translation files are never rewritten. Invalid files or values produce warnings and use fallbacks without changing gameplay or save state.
