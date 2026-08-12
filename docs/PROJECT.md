# DSP Plugin Manager - Project Steering

## Authority

This document is the authority for current product status, accepted product
decisions, scope boundaries, and open steering decisions.

It is not a work log, roadmap, design specification, implementation guide, or
validation record. Active planning belongs in [`ROADMAP.md`](ROADMAP.md), and
completed story history belongs in the
[roadmap archive](archive/ROADMAP-MILESTONE-1.md). Detailed behavior and
implementation documentation belong in separate topic documents listed by
[`INDEX.md`](INDEX.md).

## Current status

| Area | State |
| --- | --- |
| Roadmap status | Milestone 3 active; RM-24 through RM-29 accepted, and RM-30 acceptance gate met awaiting project-owner acceptance |
| Milestone 1 | Completed and accepted by project owner |
| Milestone 2 | Completed and accepted by project owner |
| Repository versioning and temporary package automation | Implemented and validated as infrastructure |
| Milestone 1 installed exit | Completed and validated against installed DSP |
| Milestone 2 installed exit | Completed and validated against installed DSP |
| Managed Harmony dependency ownership | Exact pinned closure is acquired, integrity-checked, bundled with notices, narrowly resolved, and installed-runtime validated through plugin activation and cleanup |
| Product contract | Minimal discovery, lifecycle, plugin-logging, writable-root, configuration, and shortcut slices implemented; configuration ownership, parsing, typed binding, deterministic persistence, shortcut scalar conversion, and exact Unity input polling are implemented while configuration lifecycle integration remains pending |
| Plugin discovery, activation, and lifecycle host | Selected candidates are supervised independently through activation and orderly cleanup; startup and cleanup failures are isolated, and ordinary Unity delivery plus both orderly terminal outcomes are installed-runtime validated |
| Public source-migration contract | Minimal discovery, lifecycle activation, plugin-logging, writable-root, configuration, and exact shortcut-polling slices implemented; configuration awaits lifecycle preparation |
| Consumer migrations | Mirror selected first; migration not started |
| Installable or publishable product package | Not available |

Milestones 1 and 2 established the deterministic pre-activation host and
supervised Unity activation lifecycle, then validated both in installed DSP.
Their accepted stories and exit summaries are retained in the
[roadmap archive](archive/ROADMAP-MILESTONE-1.md); detailed installed outcomes
are retained in the [Milestone 1](archive/MILESTONE-1-EXIT-EVIDENCE.md) and
[Milestone 2](MILESTONE-2-EXIT-EVIDENCE.md) evidence records.

The manager currently provides bounded candidate discovery and reconciliation,
selected-only activation as independent persistent Unity components,
source-scoped logging, plugin writable roots, isolated lifecycle failures,
orderly cleanup, the bounded configuration and shortcut compile contract,
manager-owned per-plugin configuration scopes, and the pinned manager-owned
Harmony closure. The configuration document parser retains valid unbound scalar
values for later binding, and the keyboard-shortcut scalar has its bounded
canonical codec. The late host can claim those values into stable Boolean,
string, and shortcut entries with isolated default retention, then autosave a
deterministic complete snapshot through same-directory atomic replacement.
The late host now installs exact, non-consuming keyboard polling on its admitted
Unity main thread. Configuration lifecycle integration is not implemented yet.
The temporary Thunderstore package remains internal automation evidence rather
than an installable product. Consumer migration has not started. The approved
Milestone 3 roadmap is active with RM-24 through RM-29 accepted and RM-30's
acceptance gate met, awaiting project-owner acceptance.

## Purpose and success

DSP Plugin Manager will host and manage the lifecycle of managed plugins inside
Dyson Sphere Program so the owner's retained DSP mods can remove their runtime
and package dependency on BepInEx.

The project succeeds when at least one retained consumer can build, package,
start, operate, diagnose failures, and shut down through the project-owned host
without BepInEx. Compatibility claims must identify the consumer and validation
performed.

Broad BepInEx feature parity is not a goal. New capabilities require consumer
evidence or an explicit product decision.

## Accepted decisions

### Product boundary

- The initial product is specific to Dyson Sphere Program on Windows in the
  game's supported Unity Mono environment.
- Initial consumers target .NET Framework 4.7.2 and C# 7.3-compatible contracts.
- Plugins are trusted in-process code; the manager is not a sandbox.
- The manager does not manage saves or add gameplay behavior merely by hosting
  plugins.

### Migration

- Initial migration is source adaptation to a project-owned contract.
- BepInEx assembly identity, binary compatibility, and directory structure are
  not preserved merely to avoid downstream changes.
- Build references, tests, manifests, dependency declarations, installation
  paths, and artifact checks will migrate after the source contract is selected.

### Bootstrap and lifecycle

- The default native boundary is the official UnityDoorstop 3.4.0.0 x64
  `winhttp.dll` release used by the proven DSP/BepInEx 5.4.17 installation.
- UnityDoorstop targets a project-owned minimal managed entrypoint.
- The supported Unity handoff is one validated in-memory Cecil insertion into
  `UnityEngine.Application`'s static constructor. The external
  `RuntimeInitializeOnLoadMethod` alternative is rejected because it received
  no callback in the recorded DSP probe.
- Selected plugins participate as real persistent Unity `MonoBehaviour`
  components on the main thread.
- The Unity object arrangement is one persistent host root with one owned child
  object per selected plugin. Each plugin component is attached to its own
  child so failure and orderly removal can be scoped without disturbing an
  unrelated plugin.
- The accepted lifecycle surface extends `PluginBehaviour` with two synchronous
  callbacks:
  `public abstract void Activate()` and
  `public abstract void Deactivate()`. The host invokes them explicitly on the
  Unity main thread; the concrete instance remains a real `MonoBehaviour` and
  therefore retains ordinary Unity coroutine access.
- Normal return from `Activate()` acknowledges successful startup. An exception
  means startup failed and is retained with complete plugin and phase context;
  `AddComponent` return alone never acknowledges activation.
- The supported orderly stop moves an active plugin to stopping, calls
  `Deactivate()` exactly once while its component and services remain usable,
  and treats normal return as successful cleanup or an exception as failed
  cleanup. Component destruction follows the callback and is not the cleanup
  acknowledgement.
- Private Unity `Awake` and `OnDestroy` messages remain ambient Unity behavior,
  not supervised lifecycle callbacks. Required startup or cleanup work must use
  the explicit callbacks; no process-exit or crash-cleanup guarantee is made.
- Each selected candidate owns one authoritative lifecycle record whose initial
  state is `Selected`, meaning it is eligible but no runtime activation attempt
  has begun.
- `Activating` means the one activation attempt has been admitted but has not
  been acknowledged. `Active` means explicit `Activate()` returned normally.
  `Failed` is the terminal outcome of a runtime-load, construction, or explicit
  activation failure.
- `Stopping` means the one orderly cleanup attempt has been admitted while the
  component and services remain available. `Stopped` means explicit
  `Deactivate()` returned normally; `StopFailed` means it threw.
- The only accepted transitions are `Selected → Activating`,
  `Selected → Failed`, `Activating → Active`, `Activating → Failed`,
  `Active → Stopping`, `Stopping → Stopped`, and
  `Stopping → StopFailed`. Duplicate, backward, skipped, and terminal-state
  transitions are rejected without changing the prior record.
- `Failed` and `StopFailed` require identifier, version, assembly path, type,
  phase, and complete exception context. `Stopped` and `StopFailed` are logical
  cleanup outcomes only and never claim that the managed assembly was unloaded.
- The host reports lifecycle failures without claiming hot reload or managed
  assembly unloading.
- Managed entry and the selected Unity handoff are each admitted at most once.
- The early managed entrypoint validates Doorstop's DSP paths, installs the
  reserved dependency resolver, and does not reference or call Unity APIs.
- Unity-referencing container code remains in a late-loaded assembly reached
  only through the admitted main-thread handoff; the early entry assembly does
  not acquire a Unity or Unity-container assembly reference.

### Discovery and candidate selection

- Discovery is bounded and metadata inspection avoids executing candidate code.
- Plugin identity uses a stable identifier and canonical three-part semantic
  version.
- Path aliases are inspected once.
- Byte-identical placements of one version are reduced to one documented
  ordinal path and reported as redundant.
- The highest valid version of one identifier is selected; lower versions are
  reported as superseded.
- Equal-version candidates with different content, type, or assembly identity
  reject the identity group as ambiguous.
- Failure after selection never silently falls back to an older version.
- Only a reconciliation entry in the `Selected` state may cross the runtime
  loading boundary. Rejected, redundant, superseded, and ambiguous entries are
  refused without a runtime load attempt.
- Before loading, the selected file's path, SHA-256 content, and assembly
  identity must still match its static inspection record. The loaded assembly
  must originate from that path and expose the exact recorded concrete type.
- One selected assembly path has one retained runtime-load outcome for the
  process. Success is reused; failure is not retried and never selects an older
  candidate.
- Runtime-load failures retain candidate identifier, version, path, type,
  phase, diagnostic detail, and the complete exception when one exists.
- The initial product has no plugin dependency, incompatibility, cycle, or
  cross-plugin load-order contract.

### Minimal discovery contract

- The public contract assembly simple name and root namespace are
  `DSPPluginManager.Contracts`; the assembly is culture-neutral and unsigned.
  Its assembly version follows the manager build version and is not a claim of
  binary compatibility across releases.
- `DSPPluginManager.Contracts.PluginAttribute` is a sealed, non-inherited,
  single-use class marker. Its constructor takes exactly three strings in this
  order: stable identifier, display name, and canonical version.
- The implemented `DSPPluginManager.Contracts.PluginBehaviour` remains an
  abstract `UnityEngine.MonoBehaviour`. Its accepted public service surface is
  limited to the read-only `Logger`, `WritableRoot`, and `Config` properties
  plus the parameterless abstract `Activate()` and `Deactivate()` lifecycle
  callbacks recorded above.
- Identifiers are non-empty ASCII strings containing only letters, digits,
  `.`, `_`, and `-`. Identity comparison is ordinal and case-insensitive.
- Versions contain exactly three non-negative decimal integer components
  separated by periods. Leading zeroes are rejected except for the single
  component `0`; labels and fourth components are not accepted.
- The contract is a source-migration target. It does not preserve BepInEx
  identity or claim BepInEx binary compatibility.
- Unity assemblies are external compilation inputs. The repository, bootstrap
  bundle, and package artifacts do not redistribute them.

### Services and paths

- The initial consumer service set is limited to source-scoped disk logging,
  per-plugin persistent configuration, configurable exact-combination keyboard
  shortcuts, and a host-derived writable root per plugin.
- Service ownership follows the stable plugin identifier.
- Service failures remain local and observable where the process can continue
  safely.
- Host and plugin loggers retain immutable stable-identifier and display-name
  attribution and support only information, warning, and error records.
- The exact public plugin-logging slice is the read-only
  `DSPPluginManager.Contracts.PluginBehaviour.Logger` property and the sealed
  `DSPPluginManager.Contracts.PluginLogger` type with exactly
  `void Information(object payload)`, `void Warning(object payload)`, and
  `void Error(object payload)` emission methods.
- `PluginLogger` has no public constructor, attribution member, or replacement
  operation. The host prepares one stable handle before supported plugin
  startup; plugins may retain and pass that handle but cannot select its
  attribution or dispatch destination.
- The exact public writable-path slice is the read-only string
  `DSPPluginManager.Contracts.PluginBehaviour.WritableRoot`. No other host or
  environment path is exposed.
- A plugin's writable root is the normalized absolute direct child named by
  the canonical lowercase stable identifier beneath an explicitly configured
  writable parent. The host creates it before supported plugin startup and
  provisions it once for the process.
- The writable root establishes ownership and collision avoidance for trusted
  plugins; it is not a filesystem confinement or access-control boundary.
- The selected public configuration slice is the read-only
  `DSPPluginManager.Contracts.PluginBehaviour.Config` property of sealed type
  `DSPPluginManager.Contracts.PluginConfiguration`. Plugins cannot construct or
  replace this host-prepared handle.
- `PluginConfiguration` exposes exactly three closed `Bind` overloads with
  `(string section, string key, <value> defaultValue, string description)` for
  `bool`, `string`, and
  `DSPPluginManager.Contracts.KeyboardShortcut`. Each returns the corresponding
  closed `DSPPluginManager.Contracts.PluginConfigurationEntry<T>`. There is no
  public unrestricted generic bind or converter-registration surface.
- `PluginConfigurationEntry<T>` is sealed, has no public constructor, and
  exposes only a public readable/writable `T Value`. A returned entry may be
  retained and passed to helpers. `PluginConfiguration` additionally exposes
  exactly `void Save()` for explicit complete-snapshot persistence.
- `DSPPluginManager.Contracts.KeyboardShortcut` is an immutable readonly value
  type implementing `IEquatable<KeyboardShortcut>`. Its public surface is
  exactly `KeyboardShortcut(UnityEngine.KeyCode mainKey, params
  UnityEngine.KeyCode[] heldKeys)`, the get-only static property
  `KeyboardShortcut Unset`, `bool IsDown()`,
  `bool Equals(KeyboardShortcut other)`, `override bool Equals(object obj)`,
  `override int GetHashCode()`, `override string ToString()`, and the `==` and
  `!=` operators. It exposes no mutable key collection, input provider,
  registration, or event surface.
- The host guarantees `Config` during supported `Activate()` and keeps the
  handle and entries usable through `Deactivate()` return. It does not
  guarantee configuration during ambient private Unity `Awake`; migrated
  required setup must use `Activate()`.
- Accessing `Config` before host preparation, polling a configured shortcut
  before the Unity input bridge is prepared or away from its supported Unity
  thread, invalid definitions, conflicting repeated bind types, null string
  values/defaults or descriptions, and unsupported values are synchronous
  contract errors. Empty string values and descriptions remain valid.
  Malformed stored values and configuration read/write failures are operational
  failures: they are diagnosed with plugin/definition context, retain a usable
  default or in-memory value, do not escape into plugin lifecycle code, and do
  not imply successful persistence.
- One validated canonical lowercase plugin identifier owns the human-editable
  section/key file `<canonical-identifier>.cfg` beneath the project-owned,
  configured host configuration directory. The manager never discovers,
  imports, moves, or writes an existing BepInEx configuration file; migration
  uses the separate manager file and documents any values users or migrators
  must carry forward.
- The bounded document grammar uses `[section]` headers, `key = scalar`
  assignments, blank lines, and full-line `#` comments. Surrounding structural
  whitespace is ignored, the first `=` separates key from scalar text, and
  scalar text remains untyped until a matching bind.
- Definition identity within that file is the ordinal case-sensitive
  `(section, key)` pair. The first successful bind establishes that definition's
  type, default, and description; a repeated bind of the same type returns the
  authoritative entry. Valid unbound serialized definitions are retained
  through every save so Guide's late current and legacy save keys remain
  claimable; source comments, whitespace, and byte layout are not preservation
  contracts.
- Malformed lines and invalid definitions are isolated with line-number and
  source-line context. A repeated definition is diagnosed and its last textual
  value is retained deterministically. A malformed section header clears the
  active section so following entries cannot be attributed to the preceding
  valid section accidentally.
- The only initial configuration codecs are Boolean, non-null string, and
  `KeyboardShortcut`. Missing values use the supplied default. Malformed stored
  values are scoped warnings and also retain the supplied default.
- Boolean input is case-insensitive and its canonical text is lowercase.
  String scalars keep Unicode, `;`, and `=` literally and use `\\`, `\n`, `\r`,
  `\t`, and `\uXXXX` escapes for backslash, control characters, surrogates, and
  boundary whitespace. Unknown or incomplete escapes are malformed stored
  values rather than literal text.
- Binding claims a matching unbound scalar. The first successful bind owns its
  type, default, and description; rebinding the same type returns the identical
  entry without replacing those attributes, while another type is a synchronous
  contextual contract error. Assigning an equal value is an in-memory no-op;
  assigning a different value updates the authoritative entry for later
  persistence.
- A new bind and a changed value autosave; `Save()` explicitly writes the whole
  current snapshot. Bound and unbound definitions are emitted deterministically
  as human-readable UTF-8. Persistence publishes a completed same-directory
  temporary file atomically and never truncates the final file in place or
  falls back to a non-atomic rewrite.
- Configuration operations are serialized per plugin. Each save request has a
  monotonically increasing requested version, while the persisted version
  advances only after atomic publication succeeds. Failures identify the
  publication stage and both states while retaining usable in-memory values.
- A scope whose source file was unavailable at open remains write-blocked for
  that process. Autosave and explicit save diagnose the block without touching
  the final path; recovery requires reopening the scope after the operator
  corrects the source condition.
- Shortcut construction defensively normalizes one main keyboard `KeyCode` and
  its held keyboard keys. `KeyCode.None` with no held keys is the unset value
  and is not a held key; a null held-key array, an unset main key with held keys,
  mouse, joystick, and other unsupported key families are not initial shortcut
  values.
- The persisted unset shortcut is an empty scalar. Configured shortcut text is
  one or more case-sensitive Unity `KeyCode` names separated by `+` with
  optional surrounding ASCII spaces; the first name is the main key. Canonical
  persisted and configured display text uses ` + ` between the main key and
  normalized held keys, while unset display text is `Not set`. The additional
  comma, semicolon, and pipe separators accepted by BepInEx are deliberately
  not supported because its files are not reused. The literal `None` is also
  rejected as stored text so unset has exactly one persisted representation.
- `KeyboardShortcut.IsDown()` is a non-consuming Unity-main-thread query. It
  checks the main-key edge before held state, requires every configured held
  key, rejects any additional supported keyboard key, permits unrelated mouse
  state, and leaves game/save/UI applicability and DSP input-context policy to
  the plugin. The contract assembly does not reference
  `UnityEngine.InputLegacyModule`; the late-loaded Unity host owns that internal
  adapter.
- Payload formatting, timestamp acquisition, or sink failure never escapes a
  logging call, and concurrent source calls reach the internal sink as complete
  records.
- Listener registration, source registration, filtering, structured queries,
  and plugin-selected sinks are not part of the initial logging surface.
- The authoritative current-run file is `DSPPluginManager.log` in the host log
  directory. A new run replaces it and permits concurrent read access.
- If the primary cannot open, the only alternate attempt is
  `DSPPluginManager-fallback.log` in the same directory. The host records the
  selected fallback in that file; failure of both destinations goes directly
  to the independent bootstrap emergency record.
- The disk format is human-readable UTF-8 without a byte-order mark and retains
  complete multiline messages. Buffered records flush every two seconds and
  synchronously on sink disposal; lifecycle integration must dispose the sink
  only after orderly plugin cleanup.
- There is no append history, previous-run retention, rotation, suffix search,
  or plugin-selected disk destination.

### Harmony provisioning

- The manager owns provisioning and narrow resolution of HarmonyX 2.5.5,
  MonoMod.RuntimeDetour 21.9.19.1, MonoMod.Utils 21.9.19.1, and Mono.Cecil 0.10.4.
- The distribution retains the required third-party MIT notices.
- Plugins do not bundle or select competing copies of the managed stack.
- Patch identifiers, target selection, installation, failure handling, and
  removal remain plugin responsibilities.
- MonoMod and Mono.Cecil are not public plugin contracts.

### Distribution and provenance

- Game and Unity assemblies are read-only development inputs and are not
  committed or redistributed.
- Third-party code and binaries require recorded provenance, license review,
  integrity validation, and required notices before distribution.
- The temporary Thunderstore package is automation evidence and must not be
  published as working software.

## Explicit non-goals

- general BepInEx compatibility across games or runtimes;
- IL2CPP, Linux, macOS, ARM, or non-DSP support;
- native plugin loading;
- hot reload or reliable in-process assembly unloading;
- a mod browser, installer, updater, profile manager, or Thunderstore client;
- sandboxing hostile plugins;
- reimplementing Harmony, MonoMod, UnityDoorstop, or another specialized
  third-party component;
- automatic compatibility guarantees across future DSP releases.

## Open steering decisions

- final host, plugin, configuration, log, and writable-parent locations;
- Mirror's migration acceptance matrix;
- final Thunderstore dependency, installation layout, and publication policy.

These decisions require focused evidence. An implementation task must not settle
one incidentally.

## Release steering gate

The first consumer-ready release must demonstrate:

- reproducible installation and recovery/removal;
- managed startup without BepInEx providing the lifecycle;
- bounded discovery and deterministic candidate reconciliation;
- actionable diagnostics and isolated plugin failure;
- the accepted per-plugin services;
- successful startup, core behavior, and supported shutdown of one
  source-migrated real consumer;
- removal of BepInEx from that consumer's runtime references and package
  manifest;
- no committed or redistributed game or Unity assemblies.

Until this gate is met, the repository must describe the product as unavailable.
