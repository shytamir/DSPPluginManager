# DSP Plugin Manager - Project Steering

## Authority

This document is the authority for current product status, accepted product
decisions, scope boundaries, and open steering decisions.

It is not a work log, roadmap, design specification, implementation guide, or
validation record. Work sequence and story history belong in
[`ROADMAP.md`](ROADMAP.md). Detailed behavior and implementation documentation
belong in separate topic documents listed by [`INDEX.md`](INDEX.md).

## Current status

| Area | State |
| --- | --- |
| Roadmap status | Milestone 2: Supervised Unity activation is active |
| RM-13 Unity lifecycle observability decision probe | Accepted by project owner |
| RM-14 selected assembly runtime loader | Accepted by project owner |
| RM-15 deterministic lifecycle state record | Accepted by project owner |
| RM-16 plugin logging contract slice | Accepted by project owner |
| RM-17 plugin writable-root contract slice | Acceptance conditions met; awaiting project-owner acceptance |
| Repository versioning and temporary package automation | Implemented and validated as infrastructure |
| RM-01 compiled host foundation | Accepted by project owner |
| RM-02 immutable host environment paths | Accepted by project owner |
| RM-03 independent bootstrap failure record | Accepted by project owner |
| RM-04 reserved dependency resolver | Accepted by project owner |
| RM-05 Unity main-thread handoff decision probe | Accepted by project owner |
| RM-06 pinned UnityDoorstop managed bootstrap | Accepted by project owner |
| RM-07 source-scoped non-throwing logging core | Accepted by project owner |
| RM-08 authoritative current-run disk log | Accepted by project owner |
| RM-09 minimal discovery contract slice | Accepted by project owner |
| RM-10 bounded deterministic candidate enumeration | Accepted by project owner |
| RM-11 static plugin metadata recognition | Accepted by project owner |
| RM-12 deterministic candidate reconciliation | Accepted by project owner |
| Milestone 1 installed exit | Completed and validated against installed DSP |
| Managed Harmony dependency ownership | Acquisition, integrity lock, and narrow internal runtime resolution implemented; distributable placement pending |
| Product contract | Minimal discovery, lifecycle, plugin-logging, and writable-root slices defined; remaining migration surface not specified |
| Plugin discovery, activation, and lifecycle host | Discovery, reconciliation, selected-candidate runtime loading, and deterministic lifecycle state records implemented; activation not implemented |
| Public source-migration contract | Minimal discovery, plugin-logging, and writable-root slices implemented; lifecycle and remaining service surfaces not implemented |
| Consumer migrations | Not started |
| Installable or publishable product package | Not available |

The temporary Thunderstore package remains a compiled and versioned internal
foundation, not an installable product. The separately generated bootstrap
bundle has a validated managed entrypoint, Unity handoff, internal logging core
with its current-run disk sink, and the minimal public discovery contract. An
installed DSP run entered the host once without BepInEx providing the
lifecycle, enumerated and statically inspected a seven-candidate fixture tree,
and logged the same deterministic reconciliation plan as the offline tests.
That installed exit did not runtime-load or execute a candidate. A later
installed decision probe selected an explicit two-callback lifecycle seam
because private Unity messages did not expose failure or destruction completion
to the host. The selected-candidate runtime boundary and deterministic
per-candidate lifecycle state record are implemented and fixture-validated but
are not yet invoked by the host. The public plugin-logging and writable-root
slices are implemented, but the host does not yet provision those services
because plugin activation is not implemented.

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
  abstract `UnityEngine.MonoBehaviour`. It now exposes only the read-only
  plugin-logging handle recorded below; RM-13 selected the lifecycle extension
  recorded above but did not add production lifecycle members.
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

- remaining public service contracts beyond the implemented plugin-logging
  and writable-root slices;
- final host, plugin, configuration, log, and writable-parent locations;
- configuration format and treatment of existing BepInEx `.cfg` files;
- first migration consumer and its acceptance matrix;
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
