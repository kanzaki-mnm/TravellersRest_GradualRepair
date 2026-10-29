# Gradual Repair

A Travellers Rest mod for growing your tavern little by little while running your business.

Repair a window, patch a hole in the floor, or clear away clutter and discover reusable furniture. Enjoy watching the tavern change at your own pace.

**You can postpone repairs and still progress through the normal story.** Mai's material delivery is separate from the mod's cleaning, indoor repairs, and exterior restoration.

Author: s_evans · Version: 1.0.0

## Features

- Repair 13 exterior parts—roofs, chimney, walls, windows, and door—in any order.
- Walk up to damaged indoor floors and walls to repair them individually.
- Hold the interaction button to wipe the counter or clear clutter, without paying, supplying materials, or changing tools.
- Recover reusable furniture and materials from some clutter.
- Select aged floor, aged stone wall, and aged skirting in the construction menu's decoration tab.
- Uncleaned counter and counter-side clutter contribute to vanilla cleanliness calculations.
- Start with a quieter tavern, then gradually recover normal visitor traffic as you accumulate opening hours.
- Japanese and English mod text, selected automatically from the game's language setting. Other languages use English unless an external translation is installed. Native item names and native dialog buttons keep the game's own translations.

Cleaning and repairs are **optional**. Deliver the normal 20 planks, 25 nails, and 5 stone blocks to Mai to advance the quest. Unrepaired floors, walls, and exterior parts remain visibly damaged afterward.

Changing floor or wall decoration in construction mode leaves unrepaired damage visible. Repairs and redecorating are separate activities.

## Requirements

- Windows version of Travellers Rest; tested with v0.7.6.14.0, including its hotfix.
- BepInEx 5.4.23.5 x64.
- Single-player only. Online and local co-op are outside the supported scope.

Mod cleaning and repair actions are hidden during the tutorial.

Compatibility checks resolve the native methods and fields the mod uses. A changed game DLL hash alone does not disable the mod; this does not guarantee compatibility with every future update.

## Installation

1. Exit the game.
2. Install BepInEx 5 x64 in the folder containing the game executable, if it is not already installed.
3. If this is your first BepInEx installation, launch and exit the game once and confirm that `BepInEx/plugins` exists.
4. Place `GradualRepair.dll` in `BepInEx/plugins`.
5. Launch the game. The first launch creates `BepInEx/config/GradualRepair.cfg`.

Example Steam layout:

```text
Travellers Rest/
└─ Windows/
   ├─ TravellersRest.exe
   └─ BepInEx/
      ├─ plugins/
      │  └─ GradualRepair.dll
      └─ config/
         └─ GradualRepair.cfg
```

Mod images and translations are embedded in the DLL. You do not need separate image or translation folders, investigation tools, or test supply plugins. The game, its DLLs, and BepInEx are not included with this mod.

## Playing

### Exterior repairs

Open the mailbox outside the tavern and choose **Tavern repairs** at the bottom. Press **R** on the keyboard, or use the displayed gamepad button.

Hover over a list entry, or select it with the gamepad's up/down controls, to highlight the corresponding part. Click or confirm to open the payment dialog. Accepting consumes the materials and money. Repaired entries disappear from the list.

Materials are taken from **your inventory**; move materials out of storage first. Game time and background animations pause while the repair screen is open and resume when you close it.

You can also press **F8** near the tavern.

### Indoor cleaning and repairs

Approach a target and follow its interaction guide.

- Counter and clutter: hold the interaction button to clean. Releasing the button interrupts the action.
- Floors and stone walls: interact to display the required materials and fee, then accept to repair.

Repairing damage preserves the aged floor and wall colors. Use construction mode to choose a different finish.

## Configuration

**Exit the game before editing** `BepInEx/config/GradualRepair.cfg` with a text editor. Changes take effect on the next launch.

Defaults are shown below. Settings apply to the entire game installation using this mod, rather than to individual saves.

```ini
[General]
Enabled = true

[RepairCosts]
MaterialCostPercent = 100
LaborCostPercent = 100

[Hygiene]
Enabled = true
CounterDirtScore = 50
CornerDirtScore = 30

[Visitors]
Enabled = true
InitialAdmissionRate = 0.05
HoursToFullTraffic = 100
```

### Repair costs

| Setting | Default | Meaning |
| --- | ---: | --- |
| `MaterialCostPercent` | `100` | Material cost percentage for indoor and exterior repairs |
| `LaborCostPercent` | `100` | Labor cost percentage for indoor and exterior repairs |

Set both to `20` for 20% of the standard cost, or `150` for 150%. Change only `MaterialCostPercent` if you want cheaper materials while keeping the usual labor fees.

- Accepted range: `0`–`1000`. Negative values become `0`; values above the limit become `1000`.
- `0` makes the corresponding materials or labor free.
- Costs round to the nearest whole item or copper coin, with half values rounded up. Positive percentages retain a minimum of one item or one copper for costs that were originally nonzero. Materials that were not required remain unnecessary.
- Vanilla Mai delivery requirements and salvaged items are unaffected.
- Completed repairs do not receive additional charges or refunds.

### Cleanliness

| Setting | Default | Meaning |
| --- | ---: | --- |
| `[Hygiene] Enabled` | `true` | Enable the mod's additional dirt score |
| `CounterDirtScore` | `50` | Score for the uncleaned counter (`0`–`1000`) |
| `CornerDirtScore` | `30` | Score for uncleared counter-side bricks and cobwebs (`0`–`1000`) |

Cleaning removes that target's contribution. Mai's delivery also resolves the counter contribution. Damaged floors, damaged walls, and aged finishes do not add dirt score.

One native floor stain contributes 20 points. The mod uses vanilla reactions to the combined cleanliness score, rather than applying its own reputation multiplier. Set `Enabled = false` in `[Hygiene]` to disable this addition.

### Visitor traffic

| Setting | Default | Meaning |
| --- | ---: | --- |
| `[Visitors] Enabled` | `true` | Enable reduced early traffic |
| `InitialAdmissionRate` | `0.05` | Initial share of ordinary visitor admission opportunities; `0.05` means 5% (range `0.01`–`1`) |
| `HoursToFullTraffic` | `100` | Cumulative in-game opening hours until vanilla traffic is restored (range `0`–`1000`) |

Traffic starts slowly and recovers more quickly later. Restoration does not require repairs or a particular number of served customers. Closed or paused time does not count.

At `100` hours, this is roughly five days of 20-hour openings, or eight to nine days of 12-hour openings. These are **in-game hours**, not real hours.

This setting controls **admission opportunities**; it does not guarantee the same percentage of customers or occupied seats. Special guests and seating capacity are unchanged.

To disable reduced traffic, set `Enabled = false` in `[Visitors]`, or set `HoursToFullTraffic = 0`.

## Translations

The mod creates editable UTF-8 translation files on its first enabled launch:

```text
BepInEx/config/GradualRepair/Localization/
├─ en.json
└─ ja.json
```

To add a language, copy `en.json`, name the copy after the game's language code (for example `fr.json` or `de.json`), and translate the values. Keep the keys unchanged. You can also edit `ja.json` or `en.json` to customize existing text. A file may contain only the entries you want to translate.

- The game language selects the file automatically; no separate language setting is needed. Regional codes such as `pt-BR.json` are supported. Underscores and letter case are normalized.
- Lookup order: exact regional file → base language file → English. Japanese uses the embedded Japanese catalog before falling back to English. English fallback also respects edits to external `en.json`.
- Keep placeholders such as `{0}` and dialogue tags such as `<shake>...</shake>`. `\n` represents a line break inside a JSON string. Use valid JSON: quoted keys and values, with commas between entries.
- Existing files are never overwritten, including during mod updates. New translation keys automatically use embedded defaults until you add them to your file. The source `en.json` contains the current complete key list.
- Invalid JSON files, duplicate keys, empty/non-text values, and incompatible format placeholders produce warnings in `BepInEx/LogOutput.log` and fall back to defaults.
- Keys are grouped by purpose: `action.*` for interaction labels, `exterior.*` and `interior.*` for repair locations, `building.*` for building materials, `mai.*` for dialogue, `save.*` for save warnings, and `repair.*` for repair messages. Location keys describe the place rather than a numbered asset. Translation keys do not control save IDs or repair costs.
- **Restart the game after editing translations.** Files are loaded once, avoiding filesystem polling during play.
- The catalogs cover mod-owned text only. Native item names, button labels, and fonts still come from the game. New languages may need visual checks for wrapping and font glyph coverage.

For example, a minimal French override can contain:

```json
{
  "action.close": "Fermer",
  "repair.question-exterior": "Réparer {0} ?"
}
```

These are optional mod-only files. Removing the localization folder restores embedded defaults on the next launch; removing the DLL stops the folder from being read.

## Saving and removal

Repairs, cleaning, salvage, aged decoration choices, and accumulated opening hours are saved **alongside a successful normal game save**, such as sleeping in bed. Confirming construction changes does not replace a normal save. Exiting without saving rolls mod progress back to the same saved point as the game's inventory and money.

Mod data locations:

```text
BepInEx/config/GradualRepairData/
%USERPROFILE%/AppData/LocalLow/Louqou/TravellersRest/GradualRepairRecovery/
```

Back up the game save and both mod data folders together.

To uninstall, exit the game and remove `BepInEx/plugins/GradualRepair.dll`. Configuration and mod data can remain on disk; they will not be loaded without the mod.

- Before Mai's delivery: vanilla sees the tavern as unrepaired.
- After Mai's delivery: vanilla sees the tavern as repaired. Mod-specific damage and clutter displays disappear.
- Aged finishes fall back to the native wooden floor, basic stone wall, and basic skirting respectively.
- Saved salvaged items remain in your possession.

If you play and save in vanilla after delivery, reinstalling the mod does not restore the older partially repaired state. Delete mod data only when you intend to reset its recorded repair and salvage progress.

## Reporting issues

Include the game and mod versions, the actions that triggered the issue, other installed mods, and relevant logs:

- BepInEx log: `BepInEx/LogOutput.log`
- Mod event log: `BepInEx/config/GradualRepairData/events.jsonl`

Logs and saves may include local paths and gameplay information. Review them before sharing publicly.

## Building from source

You need the .NET SDK and a local installation of the supported game and BepInEx. From the repository root:

```powershell
dotnet build src/GradualRepair/GradualRepair.csproj -c Release '-p:GameDir=C:\path\to\Travellers Rest\Windows'
```

Output: `src/GradualRepair/bin/Release/netstandard2.1/GradualRepair.dll`.

The build requires `src/GradualRepair` and the images referenced from `art/GradualRepair`. Embedded fallback text is maintained in `src/GradualRepair/Localization/ja.json` and `en.json`, using matching stable keys. Rebuild the DLL after editing these source catalogs.

The development checks project targets .NET 10 and uses Newtonsoft.Json from your local game installation:

```powershell
dotnet run --project src/GradualRepairChecks -c Release '-p:GameDir=C:\path\to\Travellers Rest\Windows'
```

Game assemblies, BepInEx binaries, saves, logs, and investigation tools are excluded from this repository.

See the [source layout](src/GradualRepair/README.md) for development details. `sandbox`, `research`, native game DLLs, and test supply plugins are not required for normal use.

## Mod Manager health reporting

Supports the optional Travellers Rest Mod Manager health API V1. No manager dependency is required. Diagnostics inspect required game references and owned patches; runtime errors are included in the cached report. See [diagnostic scope and limits](src/GradualRepair/HEALTH.md).
