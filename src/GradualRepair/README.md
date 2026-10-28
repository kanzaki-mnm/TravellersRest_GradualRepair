# Source layout

The runtime entry point is `Core/Plugin.cs`. Feature folders share the Plugin lifecycle through partial classes; pure rules and persistence are linked into `GradualRepairChecks`.

| Folder | Responsibility |
| --- | --- |
| Core | Lifecycle, costs, save ledger and safety warnings |
| Localization | Editable Japanese/English catalogs and fallback lookup |
| Compatibility | Structural native contract resolution |
| Interaction | Input and pause ownership |
| Interior | Cleaning, salvage and individual repair targets |
| Building | Aged decoration choices, tiles and door geometry |
| Exterior | Repair selection, mailbox entry and feedback |
| UI | Cloned native presentation, confirmation dialog and preview |
| Gameplay | Cleanliness, visitor traffic and Mai conversations |

## Safety boundaries

- Keep native save IDs and mod-only ledger/recovery storage separate.
- Preserve vanilla fallback IDs for aged decorations.
- Commit mod progress only alongside a successful normal game save.
- Clone native UI without adding listeners to live vanilla controls.
- Preserve input ownership, dialog cleanup and the previous time scale.
- Avoid recurring global object scans and filesystem polling.

See the root README for build instructions, configuration and translations.
