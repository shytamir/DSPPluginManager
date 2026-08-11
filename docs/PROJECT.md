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
| Active roadmap | Milestone 1: Deterministic pre-activation host |
| Repository versioning and temporary package automation | Implemented and validated as infrastructure |
| RM-01 compiled host foundation | Accepted by project owner |
| RM-02 immutable host environment paths | Accepted by project owner |
| RM-03 independent bootstrap failure record | Accepted by project owner |
| RM-04 reserved dependency resolver | Accepted by project owner |
| RM-05 Unity main-thread handoff decision probe | Accepted by project owner |
| RM-06 pinned UnityDoorstop managed bootstrap | Accepted by project owner |
| RM-07 source-scoped non-throwing logging core | Accepted by project owner |
| RM-08 authoritative current-run disk log | Implementation and acceptance evidence complete; awaiting project-owner acceptance |
| Managed Harmony dependency ownership | Acquisition, integrity lock, and narrow internal runtime resolution implemented; distributable placement pending |
| Product contract | Being defined and reviewed |
| Plugin discovery, activation, and lifecycle host | Not implemented |
| Public source-migration contract | Not specified |
| Consumer migrations | Not started |
| Installable or publishable product package | Not available |

The temporary Thunderstore package remains a compiled and versioned internal
foundation, not an installable product. The separately generated bootstrap
bundle has a validated managed entrypoint, Unity handoff, and internal logging
core with its current-run disk sink, but no plugin discovery, activation,
lifecycle services, or public plugin contract.

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
- The initial product has no plugin dependency, incompatibility, cycle, or
  cross-plugin load-order contract.

### Services and paths

- The initial consumer service set is limited to source-scoped disk logging,
  per-plugin persistent configuration, configurable exact-combination keyboard
  shortcuts, and a host-derived writable root per plugin.
- Service ownership follows the stable plugin identifier.
- Service failures remain local and observable where the process can continue
  safely.
- Host and plugin loggers retain immutable stable-identifier and display-name
  attribution and support only information, warning, and error records.
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

- activation acknowledgement semantics after the selected Unity handoff;
- supported shutdown observation semantics;
- public contract name, namespace, assembly identity, type signatures, and
  metadata encoding;
- identifier case sensitivity;
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
