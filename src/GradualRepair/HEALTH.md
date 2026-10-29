# Optional Mod Manager health report (V1)

GradualRepair 1.0.0 exposes `public string[] GetModHealthV1()` on its plugin.
The three entries are status, summary and plain-text details. The getter only
clones a cached snapshot. No Mod Manager assembly reference or dependency is
required. There is no separate warning UI.

## Inspection scope

Startup checks native compatibility methods, properties and fields, 25 cached
UI/input/save fields, and compiled references to game assemblies. Checks include
declaring types, parameter and return types, generic arity where applicable,
field types and static/instance shape. Reflected runtime operations share the
validated cached handles. Direct compiled calls retain the audited CLR references.
Every required Harmony prefix/postfix/finalizer is checked against its target,
patch method, role and owner; patch integrity is checked again every five unscaled
seconds outside the report getter.

- **Healthy:** startup references and all required patches passed; no recorded
  warning or runtime failure. This does not certify gameplay at the title screen.
- **Warning:** an alternative native compatibility profile was actually selected,
  or an optional/runtime resource warning was recorded. Structural alternatives
  must have exactly one matching candidate; ambiguity is never accepted.
- **Failed:** a required reference is missing, ambiguous or has an incompatible
  signature; a required owned patch is absent; or a runtime operation failed.
  Startup failures unpatch this plugin and stop initialization. Runtime feature
  guards stop affected operations; missing required patches stop and unpatch the
  plugin. Missing exterior sprite targets stop repairs before payment.
- **Pending:** startup diagnostics have not completed.
- **Disabled:** disabled intentionally by configuration, without a recorded failure.

Failures take precedence over warnings and remain in the report for the plugin
lifetime, including after a recoverable operation succeeds. Restart to obtain a
fresh report. DLL hashes are informational, never a reason to disable the mod.

## Limits and verification

Title checks inspect metadata and patch registration, not scene instances.
Scene sprites, UI templates/fonts, input-module instances, inventory capacity,
dialogue/event priority, building edits, cleanliness, traffic, repair/cleanup,
salvage and ledger save/load require normal gameplay. Detected runtime failures
are added to the report. Multiplayer remains unsupported; tutorial repair
controls are intentionally unavailable.

Diagnosis never invokes game functions as probes and never reads or writes saves.
The normal gameplay persistence path remains unchanged. Native building IDs keep
their vanilla fallbacks; removing the DLL and mod-only data restores vanilla.

Controlled validation: 73 health checks (including unique/ambiguous structural
matching, signature mismatches, field shapes, patch ownership, cached getter and
no manager dependency), 582 existing rule/persistence checks and 6 save-safety
checks. Current installed game metadata resolves 514 compiled member uses.
The cached report was read 100,000 times in under 100 ms in the controlled test.
Unity startup, real Harmony installation and the Manager's title-screen display
have not been exercised by these tests; gameplay is also unverified for this build.

Health tests: `research/gradual-repair/health-v1/HealthChecks.csproj`.
