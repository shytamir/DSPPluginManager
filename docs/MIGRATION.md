# Mirror and Guide Migration Instructions

## Purpose and boundary

This is the authoritative source-migration guide for moving
DSP Mirror Blueprint first, and DSP Guide Check afterward, from BepInEx 5 to
DSP Plugin Manager. It describes the implemented manager contract and the
known behavior drift that downstream maintainers must account for.

Neither consumer has migrated yet. The manager's final Thunderstore dependency
identity, plugin payload path, and publication procedure are not selected, so
this document marks those substitutions as blocked instead of guessing them.
The migration-reference kit is a compile input, not an installable manager or
plugin package.

## Reference-kit acquisition

Pin both a full 40-character manager commit and the positive build sequence
used for the reviewed kit. Never build consumer releases from `main`, a moving
tag, or an unverified DLL. A local or CI job can reproduce the kit as follows:

```powershell
$ManagerRevision = '<FULL_MANAGER_COMMIT>'
$ManagerSequence = <POSITIVE_BUILD_SEQUENCE>
git clone https://github.com/shytamir/DSPPluginManager.git .manager-source
git -C .manager-source checkout --detach $ManagerRevision
& .\.manager-source\build.cmd `
  -Commit $ManagerRevision `
  -Sequence $ManagerSequence
& .\.manager-source\scripts\Test-MigrationReferenceKit.ps1 `
  -KitRoot .\.manager-source\artifacts\migration-reference-kit `
  -ExpectedCommit $ManagerRevision `
  -ExpectedSequence $ManagerSequence
```

Copy or cache the complete validated
`artifacts/migration-reference-kit` directory. Commit the selected manager
revision and sequence to the consumer's dependency lock or CI configuration.
The kit's `INTEGRITY.json` records that revision, sequence, contract identity,
and SHA-256/length of every other kit file. Do not take
`DSPPluginManager.Contracts.dll` or `0Harmony.dll` from an installed game or a
BepInEx directory.

The kit contains only:

- the built `DSPPluginManager.Contracts.dll` compile reference;
- the manager-approved plugin-facing `0Harmony.dll` compile reference;
- the managed dependency notices required by this reviewed stack;
- this migration document; and
- `INTEGRITY.json`.

The manager owns and installs HarmonyX's full pinned runtime closure. A plugin
references the kit's `0Harmony.dll` when it uses Harmony, marks it non-private,
and does not copy Harmony, MonoMod, or Cecil into its output or package.

## Contract mapping

| Existing BepInEx wiring | Manager migration | Ownership after migration |
| --- | --- | --- |
| `[BepInPlugin(id, name, version)]` | `[DSPPluginManager.Contracts.PluginAttribute(id, name, version)]` | Plugin supplies the same stable identifier, display name, and canonical three-part version. |
| `BaseUnityPlugin` | `DSPPluginManager.Contracts.PluginBehaviour` | Manager constructs and supervises the Unity component. |
| required work in private `Awake()` | override `public void Activate()` | Plugin moves required startup here; services are prepared before the call. |
| cleanup in private `OnDestroy()` | override `public void Deactivate()` | Plugin performs orderly cleanup here; ambient destruction is not an acknowledgement. |
| `Logger.LogInfo/LogWarning/LogError` | `Logger.Information/Warning/Error` | Manager owns the attributed sink; plugin owns message content. |
| `Config.Bind(...)` | `Config.Bind(...)` | Manager owns persistence; plugin retains typed entries. |
| `ConfigEntry<T>` | `PluginConfigurationEntry<T>` | The public `Value` usage remains read/write. |
| BepInEx `KeyboardShortcut` | `DSPPluginManager.Contracts.KeyboardShortcut` | Manager owns conversion and exact polling; plugin decides when polling is applicable. |
| `Paths.BepInExRootPath` | `WritableRoot` | Plugin chooses only descendants inside its prepared root. |
| `StartCoroutine` / `StopCoroutine` | unchanged Unity calls on `PluginBehaviour` | Plugin remains a real `MonoBehaviour` and owns coroutine state and cleanup. |
| `new Harmony(id)`, patch selection, `UnpatchSelf()` | same Harmony API using manager-provided `0Harmony.dll` | Manager provisions/resolves the pinned assembly; Mirror still owns patches and removal. |

The migrated entry-point shape is:

```csharp
using DSPPluginManager.Contracts;

[PluginAttribute(PluginGuid, PluginName, BuildVersion.PluginVersion)]
public sealed class Plugin : PluginBehaviour
{
    public override void Activate()
    {
        // Bind configuration, initialize helpers, install patches, then log.
    }

    public override void Deactivate()
    {
        // Stop coroutines, release UI, and remove owned patches.
    }
}
```

`Logger`, `WritableRoot`, and `Config` are guaranteed during `Activate()` and
remain usable until `Deactivate()` returns. They are not guaranteed during a
private Unity `Awake`. Ordinary Unity messages such as `Update` continue, but
startup success means `Activate()` returned normally and orderly cleanup means
`Deactivate()` returned normally. Process crashes and forced termination have
no cleanup guarantee.

## Configuration and shortcut drift

The manager does not import, move, or overwrite BepInEx configuration. Existing
values must be copied deliberately from the old file to the corresponding
section/key in the new file after the manager has created it once. Do not copy
the whole BepInEx file: retain the manager-generated definitions and translate
only supported scalar values under the rules below.

| Consumer | Existing BepInEx file | Manager file |
| --- | --- | --- |
| Mirror | `BepInEx/config/com.shytamir.dspmirrorblueprint.cfg` | `DSPPluginManager/config/com.shytamir.dspmirrorblueprint.cfg` |
| Guide | `BepInEx/config/local.dsp.progressionstatusexporter.cfg` | `DSPPluginManager/config/local.dsp.progressionstatusexporter.cfg` |

Those manager paths describe the current bootstrap-bundle layout. They do not
choose the final consumer package payload path.

Known differences are contractual:

- Definition identity is the ordinal, case-sensitive `(section, key)` pair.
  Preserve exact capitalization and spaces when copying values.
- Supported values are Boolean, non-null string, and the manager shortcut.
  Boolean text is case-insensitive and rewrites as lowercase.
- Valid unbound entries survive autosaves. Guide may bind fixed settings early,
  then claim the current and legacy save keys later without erasing either.
- A new bind and a changed value autosave. `Config.Save()` requests an explicit
  whole-file save. Output is deterministic UTF-8, not a byte-for-byte rewrite
  of comments or formatting.
- A malformed stored scalar retains the supplied default and emits an
  identified warning. Read or write failures leave in-memory values usable;
  failed persistence is diagnosed and must not be described as saved.
- A configuration source that could not be opened is write-blocked for that
  process. Correct the file condition and restart before expecting writes.
- Unset shortcut persistence is an empty scalar and displays as `Not set`.
  Configured text uses case-sensitive Unity `KeyCode` names separated by `+`,
  for example `F9 + LeftShift`. Comma, semicolon, pipe, mouse, and joystick
  forms accepted elsewhere are not supported.
- `IsDown()` is an exact, non-consuming Unity-main-thread query: the main key
  must have a down edge, all configured held keyboard keys must be held, and no
  additional supported keyboard key may be held. Unrelated mouse state is
  ignored.
- The manager does not apply DSP input-context or UI suppression. Each consumer
  remains responsible for deciding whether its shortcut should act in the
  current game, save, panel, or text-input state.

The consumer bind pattern is otherwise direct:

```csharp
private PluginConfigurationEntry<bool> enabled;
private PluginConfigurationEntry<KeyboardShortcut> shortcut;

enabled = Config.Bind("Diagnostics", "Enabled", false, "Enable diagnostics.");
shortcut = Config.Bind(
    "Diagnostics",
    "Shortcut",
    new KeyboardShortcut(KeyCode.F9),
    "Diagnostic shortcut."
);
```

## Source and user-documentation substitutions

Make all source and public text changes together. A migration is incomplete if
the code no longer uses BepInEx but its errors or instructions still direct
users to BepInEx paths.

- Replace `using BepInEx`, `BepInEx.Configuration`, and `BepInEx.Logging` with
  the manager contract namespace. Mirror retains `using HarmonyLib`.
- Replace BepInEx attribute/version names in generated-version validation with
  the manager `PluginAttribute` version argument and canonical `M.m.p` rules.
- Rename BepInEx-specific variables and report fields. In particular, Guide's
  exported `bepInExRoot` field must become a manager-neutral field such as
  `pluginWritableRoot`; this is consumer-owned output-schema drift.
- Replace messages such as `Check BepInEx LogOutput.log` with the current-run
  manager log location: `DSPPluginManager/logs/DSPPluginManager.log`.
- Mirror diagnostic geometry moves from
  `BepInEx/DSP-Mirror-Blueprint/Diagnostics` to
  `Path.Combine(WritableRoot, "Diagnostics")`.
- Guide's `DSP-Status` output moves from the BepInEx root to
  `Path.Combine(WritableRoot, "DSP-Status")`. Tests and user documentation must
  use that consumer-owned location.
- Remove claims that BepInEx supplies lifecycle, logging, configuration,
  Harmony, installation, or troubleshooting. Do not claim a final manager
  package dependency or install path until those blocked substitutions are
  selected.

## Mirror-first checklist

### Source

1. Change `Plugin` to derive from `PluginBehaviour` and replace
   `BepInPlugin` with `Plugin` metadata using
   `com.shytamir.dspmirrorblueprint`, `DSP Mirror Blueprint`, and the generated
   three-part product version.
2. Change the three fields to
   `PluginConfigurationEntry<bool>`,
   `PluginConfigurationEntry<KeyboardShortcut>`, and
   `PluginConfigurationEntry<bool>` without changing `[Diagnostics]
   EnableGeometryDump`, `GeometryDumpKey`, or `EnableInputDiagnostics`.
3. Move all required `Awake()` work to `Activate()`: binds, Harmony creation,
   patch installation, helper wiring, and startup logging.
4. Move `OnDestroy()` cleanup to `Deactivate()`: call
   `BlueprintRuntimeMirror.Uninstall()` and `harmony.UnpatchSelf()` once when
   applicable. Keep Harmony patch identifier and target selection plugin-owned.
5. Change logger helper parameters from BepInEx `ManualLogSource` to
   `PluginLogger`, and map the three emission method names.
6. Replace `Paths.BepInExRootPath` in `BlueprintGeometryDumper` with a path
   supplied from `WritableRoot`; do not make helpers rediscover the game or host
   root.
7. Keep `Update()` and the configured `IsDown()` call, including Mirror's own
   blueprint-deployment applicability check.

### Build and deterministic tests

1. Remove every `BepInEx.dll` reference and validation branch.
2. Add non-private references to the kit's
   `DSPPluginManager.Contracts.dll` and `0Harmony.dll`; retain the required
   Unity/game references. Do not reference manager implementation assemblies.
3. Make local and CI acquisition use the same pinned manager revision,
   sequence, and kit validator described above.
4. Update metadata tests to require `PluginAttribute`, `PluginBehaviour`, and
   the exact identifier/version. Reject a `BepInEx` assembly reference.
5. Compile and test fixed default/stored Boolean values, F9 and multi-key
   shortcut display/polling, malformed-value fallback, logger method mapping,
   writable diagnostic path, patch cleanup, and absence of private
   `0Harmony`/MonoMod/Cecil output.
6. Update package artifact validation to reject `BepInEx.dll` and any private
   manager/Harmony closure. Leave the final plugin payload path assertion
   blocked until the installation contract is selected.

### Manifest, package, and documentation

- **Blocked substitution:** replace `xiaoye97-BepInEx-5.4.17` with the final
  published manager dependency only after its Thunderstore identity exists.
- **Blocked substitution:** replace
  `BepInEx/plugins/DSP-Mirror-Blueprint/DSPMirrorBlueprint.dll` with the final
  manager plugin payload path only after that installation contract exists.
- README, packaging instructions, release checks, and diagnostics must use the
  manager lifecycle, log, configuration, and writable paths described above.
- Publication remains blocked until both substitutions are selected and their
  package validator has been updated. Source/build/test migration may proceed.

## Guide-following checklist

### Source

1. Change `Plugin` to derive from `PluginBehaviour` and replace metadata using
   `local.dsp.progressionstatusexporter`, `DSP Guide Check`, and the generated
   three-part product version.
2. Replace BepInEx configuration and shortcut types while preserving fixed
   `[General] SnapshotKey`, `[General] IncludeDiagnostics`, and every exact
   current/legacy `[Phase Selection]` key.
3. Move required `Awake()` initialization to `Activate()`: logger capture,
   fixed binds, reflected game type discovery, panel actions, and startup log.
4. Move coroutine stop and panel destruction from `OnDestroy()` to
   `Deactivate()`. Keep ordinary coroutine calls; they remain available through
   the Unity base class.
5. Replace static `ManualLogSource` storage with `PluginLogger`, or pass the
   prepared manager logger explicitly to helpers. Map emission method names.
6. Replace status-output and exported-root uses of `Paths.BepInExRootPath` with
   `WritableRoot` supplied to the relevant helpers. Rename the exported field
   and document the schema change.
7. Preserve the current late-bind sequence and explicit `Config.Save()` after
   selection mutation. Do not pre-bind all possible save keys.
8. Keep `Update()` polling but retain Guide-owned save/UI applicability and
   coroutine-state rules.

### Build and deterministic tests

1. Remove every `BepInEx.dll` reference and CI download/validation path.
2. Add a non-private reference to the kit's
   `DSPPluginManager.Contracts.dll`; Guide does not add a Harmony reference.
   Retain required Unity/game/UI references.
3. Use the same pinned kit acquisition and validation procedure as Mirror.
4. Update metadata/artifact tests to require the manager attribute/base class,
   reject `BepInEx`, and reject copied manager/Harmony/MonoMod/Cecil binaries.
5. Exercise fixed defaults/stored values, unset and F8 shortcuts, current and
   legacy save-key retention through early autosaves, late claim/mutation,
   explicit save, reopen, malformed values, write failure, coroutine cleanup,
   and the renamed writable-root report field.
6. Keep gameplay/guide model tests independent of the host; add only focused
   adapter/contract tests around the new entry-point wiring.

### Manifest, package, and documentation

- **Blocked substitution:** replace `xiaoye97-BepInEx-5.4.17` only after the
  manager's published dependency identity exists.
- **Blocked substitution:** replace
  `BepInEx/plugins/DSP-Guide-Check/DspGuideCheck.dll` only after the manager's
  final plugin payload path exists.
- Update README, runtime testing, package documentation, messages, and snapshot
  schema documentation for the manager log/config/writable paths and the
  renamed root field.
- Publication remains blocked on the same two installation-contract choices;
  source/build/test migration may follow Mirror after its adapter pattern is
  accepted.

## Completion boundary for downstream migrations

A consumer source migration is ready for installed validation when it compiles
only against its listed kit and Unity/game references, deterministic tests pass,
its DLL contains no BepInEx reference, its build output contains no private
manager-owned dependency, and all user-facing BepInEx service/path language has
been replaced or explicitly marked blocked.

That state is not publication readiness. Manifest dependency, final plugin
payload path, installation instructions, and publication checks remain blocked
until DSP Plugin Manager establishes its installation and distribution
contract.
