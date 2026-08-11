# DSP Plugin Manager - Project Definition

## Product state

DSP Plugin Manager is in bootstrap and contract-definition phase. No plugin
host, compatibility surface, installer, or release package has been implemented
yet. Semantic version generation and a temporary Thunderstore package pipeline
are implemented as repository infrastructure. Their payload is an intentionally
empty DLL, so a passing package build is not evidence of product behavior.

This document is the active product and engineering contract. It records the
accepted direction, the boundaries that protect that direction, and the
decisions that still require evidence. Future roadmaps and completed validation
records may live elsewhere, but they do not override this document.

## Purpose

DSP Plugin Manager will host and manage the lifecycle of managed plugins inside
Dyson Sphere Program. It exists so a focused family of DSP mods can replace
their BepInEx dependency with a smaller product owned and maintained alongside
those mods.

The objective is dependency independence for known consumers, not feature
parity with BepInEx. BepInEx demonstrates the broad problem space; the existing
DSP mods define the initial compatibility demand.

## Problem statement

The owner's established mods currently rely on BepInEx 5 for several distinct
jobs:

- entering the game's Unity Mono process;
- finding plugin assemblies;
- identifying plugin classes and versions;
- reconciling duplicate placements and plugin versions safely;
- constructing Unity plugin components at the correct point in startup;
- providing per-plugin logging, configuration, and paths;
- provisioning the exact Harmony runtime used by the retained patching consumer;
- supplying a conventional installation and packaging dependency.

That bundle creates an upstream dependency even when each mod uses only a small
part of the framework. DSP Plugin Manager will separate those needs, implement
only the accepted subset, and provide a controlled migration path.

## Primary users

1. **Players** need DSP to start predictably, useful diagnostics when a plugin
   cannot load, and no silent effect on saves or game behavior from the manager
   itself.
2. **Plugin authors in the owner's DSP projects** need a stable, small contract
   for metadata, startup, shutdown, logging, configuration, and paths.
3. **Maintainers** need deterministic behavior, isolated version-sensitive
   integration, focused tests, and a migration process that can be validated
   one consumer at a time.

## Product goals

- Start a managed plugin host in the supported DSP Unity Mono process.
- Discover plugins only from documented, bounded roots.
- Inspect plugin metadata without executing plugin code where practical.
- Give every plugin a stable identifier and version.
- Validate malformed metadata and unsupported contracts before activation.
- Reconcile path aliases, duplicate placements, duplicate identities, and
  competing versions through one deterministic, observable policy.
- Activate eligible plugins and isolate/report individual startup failures.
- Provide the minimum logging, configuration, and path services required by
  migrated consumer mods.
- Define observable lifecycle states and clear failure outcomes.
- Let initial consumer mods remove their runtime and package dependency on
  BepInEx after focused build and in-game acceptance.
- Keep the public contract narrow enough for this repository to maintain.

## Non-goals

- General BepInEx compatibility across Unity, IL2CPP, .NET, XNA, or other games.
- Supporting every BepInEx attribute, service, extension, patcher, or loader.
- Reimplementing Harmony, MonoMod, Doorstop, or another third-party component
  merely because BepInEx distributes or integrates it.
- Loading native-code plugins in the initial product.
- Hot reload or reliable managed-assembly unloading in the initial product.
- Sandboxing hostile or untrusted in-process plugin code.
- A graphical mod browser, installer, updater, profile manager, or loadout UI.
- Downloading from or publishing to Thunderstore or another package registry.
- Managing saves, changing factory state, or adding gameplay behavior itself.
- Automatic compatibility guarantees across future DSP releases.

Any proposal to add one of these capabilities requires an explicit scope and
maintenance-cost decision.

## Initial compatibility target

The initial target is the pattern shared by the owner's existing DSP mods:

- Dyson Sphere Program on Windows;
- the game's Unity Mono environment;
- mod assemblies targeting .NET Framework 4.7.2 (`net472`);
- C# 7.3-compatible consumer contracts;
- a stable plugin identifier, display name, and semantic version;
- Unity component lifecycle participation;
- per-plugin logging and configuration;
- Harmony-based patches owned by the plugin;
- local builds that use installed game and Unity assemblies as read-only
  references;
- packages that currently declare `xiaoye97-BepInEx-5.4.17` as a dependency.

This list defines investigation and migration demand. It does not establish
binary compatibility with BepInEx types or assembly identity.

## Product invariants

- The same plugin files and configuration produce the same inspection,
  reconciliation, and selection results.
- Filesystem enumeration order never determines candidate selection.
- A plugin is identified by a stable, case-defined identifier rather than a
  filename or display name.
- Path aliases are inspected once. Byte-identical placements of one version are
  reduced to one documented ordinal path with a warning.
- When valid versions compete for one identifier, the highest three-part
  semantic version is selected and every lower version is reported as
  superseded.
- Equal-version candidates with different content, type, or assembly identity
  are rejected as an ambiguous identity group before either is activated.
- Failure after candidate selection never silently falls back to an older
  version.
- Failure to discover, inspect, validate, or activate one plugin is visible and
  does not silently alter unrelated plugin behavior.
- Plugin code is not executed merely to learn metadata when static inspection
  can provide it safely.
- The manager does not modify DSP save or gameplay state as part of hosting.
- Logs do not expose secrets or unnecessarily collect player/save data.
- Game and Unity assemblies are never committed or redistributed.
- Compatibility claims identify the exact consumer and validation performed.

## Lifecycle contract

The initial lifecycle vocabulary is:

```text
Discovered
    |
Inspected -----> Rejected
    |
Validated ----> Suppressed
    |
Selected
    |
Activating ----> Failed
    |
Active
    |
Stopping ------> StopFailed
    |
Stopped
```

These names describe product states, not yet a public API. Implementation may
refine the representation while preserving the observable distinctions.

- **Rejected** means the candidate itself is malformed or unsupported, or its
  identifier group is ambiguous.
- **Suppressed** means a valid candidate is a redundant placement or an older
  version that the deterministic reconciliation policy did not select.
- **Failed** means plugin code threw or otherwise failed during activation.
- **Active** means the host completed its part of activation; it does not prove
  the plugin's gameplay feature works.
- **Stopped** means supported shutdown callbacks completed. It does not promise
  managed assemblies were unloaded from the process.

Every non-success state must carry an actionable diagnostic. Failure of one
selected plugin must not silently change the selected version or destroy the
lifecycle state and services of an unrelated plugin.

## Candidate reconciliation semantics

The initial product has no plugin relationship metadata or dependency planner.
It does not interpret assembly references as plugin dependencies and does not
promise a relative activation order that plugins may depend upon.

Candidate reconciliation uses the stable plugin identifier and canonical
three-part semantic version:

- one canonical file reached through aliases is inspected once;
- byte-identical copies of one version are reduced to the ordinal-first
  canonical path and diagnosed as redundant;
- among different valid versions, the highest version is selected and the rest
  are diagnosed as superseded;
- equal-version candidates with different bytes, plugin types, or assembly
  identities make the identifier group ambiguous and reject the group;
- a selected candidate's later failure never causes fallback to a suppressed
  version.

Identifier case sensitivity remains open and must be fixed before metadata is
frozen. Hard and optional dependencies, incompatibilities, version ranges,
cycles, dependent blocking, and cross-plugin load-order contracts require new
consumer evidence or a separate explicit product decision.

## Architecture

```text
Native/process bootstrap boundary
              |
       Host startup adapter
              |
     Environment and path model
              |
  Assembly discovery and metadata inspection
              |
  Candidate validation and deterministic selection
              |
       Plugin activation supervisor
              |
  +-----------+------------+-------------+
  |                        |             |
Logging                Configuration   Diagnostics
              |
    Optional compatibility facade
              |
       Migrated DSP plugins
```

### Bootstrap boundary

Gets managed control inside the DSP process. The default is the pinned Windows
UnityDoorstop 3.4 generation used by the proven DSP/BepInEx 5.4.17 installation,
targeting a project-owned minimal managed entrypoint. The evidence-backed
Unity-main-thread handoff is narrow in-memory injection into one validated Unity
method. A bounded `RuntimeInitializeOnLoadMethod` experiment may replace only
that handoff if it proves equally reliable in the supported DSP build.
Ownership, licensing, installation, collision behavior, disable/removal, and
early-failure reporting remain explicit manager distribution responsibilities.

### Host startup and environment

Establishes the game root, managed-assembly location, plugin roots,
configuration root, log destination, host version, and supported process/runtime
before discovery begins. Path calculation must be centralized and testable.

### Discovery and metadata inspection

Enumerates bounded roots, identifies managed assembly candidates, and reads
plugin metadata. Discovery should avoid loading candidate assemblies into the
game's primary execution context until a candidate is eligible for activation.

### Validation and candidate selection

Owns identity validation, supported-contract checks, path-alias handling,
duplicate placement recognition, version reconciliation, ambiguity rejection,
and selected-candidate records. This layer must be deterministic and testable
without DSP.

### Activation supervisor

Loads eligible assemblies, creates plugin instances through the accepted Unity
integration seam, records state transitions, supplies services, and isolates
startup/shutdown failures. It owns lifecycle policy, not plugin-specific game
behavior.

### Services

Logging, configuration, and paths are per-plugin services with explicit
ownership. Service contracts should be small and should not expose host-global
mutable state without a demonstrated requirement.

### Compatibility facade

If selected, adapts an established mod-facing surface to the host's internal
contracts. It does not own discovery, candidate-selection policy, or lifecycle
state. Compatibility code must remain removable and measurable per migrated
consumer.

## Migration strategy

Migration is consumer-driven:

1. inventory the BepInEx surface actually used by one selected mod;
2. classify each use as lifecycle, service, patching, build, packaging, or
   incidental;
3. define the project-owned source-migration contract explicitly;
4. add the minimum host contract and deterministic tests;
5. build the consumer without its BepInEx dependency;
6. validate startup, behavior, diagnostics, and shutdown in DSP;
7. remove the package dependency only after that evidence passes;
8. repeat for the next consumer without generalizing unsupported APIs.

The first migrated mod should be representative enough to exercise metadata,
logging, configuration, Unity lifecycle, and Harmony integration while being
small enough to diagnose. Migration order is not selected here.

The initial migration is source adaptation to a project-owned contract. The
manager does not preserve BepInEx assembly identity, directory names, or binary
compatibility merely to avoid downstream changes. Any future binary facade is a
separate product proposal requiring exact unchanged-binary and in-game evidence.

## Patching boundary

The existing consumers use Harmony to modify DSP behavior. Harmony patch
creation, ownership, and cleanup belong to each plugin unless a later contract
establishes a narrowly shared service.

The manager distribution provisions and narrowly resolves exactly HarmonyX
2.5.5, MonoMod.RuntimeDetour 21.9.19.1, MonoMod.Utils 21.9.19.1, and Mono.Cecil
0.10.4, including their required MIT notices. Plugins do not bundle or select a
competing copy. Provisioning does not make patch target selection, patch state,
or cleanup manager responsibilities, and MonoMod/Cecil are not public plugin
contracts.

## Configuration and logging principles

The initial service contracts should preserve outcomes the consumers need,
without reproducing an entire upstream API:

- configuration is namespaced per stable plugin identifier;
- defaults, parsing failures, persistence timing, and file locations are
  deterministic and documented;
- malformed configuration fails locally and visibly;
- log records include timestamp, severity, source/plugin identity, and message;
- startup diagnostics identify discovery, rejection, suppression, selection,
  and activation outcomes;
- one plugin cannot accidentally claim another plugin's default configuration
  or logging identity.

Whether existing BepInEx `.cfg` files are read or migrated is an open decision.

## Security and trust model

Managed plugins run inside the DSP process and can access the game, filesystem,
network APIs available to the process, and other loaded assemblies. The manager
is therefore not a sandbox.

The initial product assumes plugins are deliberately installed and trusted.
Bounded discovery, metadata inspection, deterministic activation, and clear
diagnostics reduce accidental risk but do not make hostile code safe. Any future
signature, permission, quarantine, or sandbox language requires a separate
security design.

## Reference and provenance policy

The local BepInEx clone is used to understand the breadth of lifecycle hosting
and the semantics currently experienced by consumers. The two established mod
repositories are used to identify actual dependency and packaging patterns.

New code should be independently authored against this project's contract and
focused behavioral tests. BepInEx code must not be copied into the repository
without an explicit provenance and license review. The repository's Apache 2.0
license does not erase third-party license obligations.

Installed DSP and Unity assemblies may be inspected as read-only compatibility
evidence. They must not be modified, committed, or redistributed.

## Version and temporary package contract

`VERSION` supplies manually selected major and minor values. GitHub Actions uses
its run number as the patch value. Package and product semantic versions are
`M.m.N`, future assembly/file metadata is `M.m.N.0`, and the diagnostic release
label is `M.m.N.<short-commit>`.

The current Thunderstore ZIP is automation evidence only. It contains the
platform's required root metadata and a generated empty DLL, icon, and version
record. It must not be published as working software. Generic structure and
metadata validation are active; product-specific installation, dependency,
content, and executable-identity checks remain deferred until their contracts
exist. See [the temporary package contract](THUNDERSTORE-PACKAGE.md).

## Validation strategy

Validation should progress from deterministic logic to the live runtime:

1. metadata fixtures for valid, malformed, and unsupported plugins;
2. reconciliation fixtures for aliases, identical placements, competing
   versions, equal-version ambiguity, and varied filesystem enumeration order;
3. lifecycle fixtures for activation failure, shutdown, and unrelated-plugin
   service isolation;
4. a sample plugin exercising the public contract;
5. an installed-game cold-start check with actionable host logs;
6. one source-migrated real consumer with its BepInEx build and package
   dependency removed;
7. focused in-game behavior and shutdown validation for that consumer;
8. repeatable package-layout verification.

Compilation alone proves none of the runtime lifecycle or migration claims.
Acceptance evidence must identify the DSP version, host build, plugin build,
installation layout, and exact scenario exercised.

## Initial release acceptance

The first release suitable for a consumer migration must demonstrate:

- reproducible installation into a clean supported DSP layout;
- successful managed startup without BepInEx providing the plugin lifecycle;
- bounded discovery and deterministic candidate reconciliation;
- actionable diagnostics for every rejected, suppressed, or failed fixture;
- per-plugin logging and configuration plus documented writable-path ownership;
- startup and supported shutdown of a sample plugin;
- successful build, package, startup, and core behavior of one real migrated
  consumer;
- no BepInEx dependency in that consumer's runtime references or package
  manifest, except any explicitly documented transitional compatibility
  artifact;
- no committed or redistributed DSP or Unity assemblies;
- documented recovery or removal steps if startup fails.

This milestone establishes one credible migration path. It does not establish
compatibility with all owner projects or all BepInEx plugins.

## Open decisions

The following decisions remain intentionally unresolved:

- public plugin contract name, namespace, and assembly identity;
- metadata encoding;
- identifier case sensitivity;
- plugin directory and package layout;
- configuration format and any BepInEx `.cfg` migration behavior;
- the exact Unity-main-thread activation acknowledgement and supported shutdown
  observation seam;
- first migration consumer;
- final Thunderstore installation layout and publication policy.

Resolve these decisions with a focused proposal and evidence. Do not settle
them accidentally through the first convenient implementation.

## Near-term sequence

1. Inventory the BepInEx API and packaging surface used across candidate
   consumer mods.
2. Select the first migration consumer and define its acceptance matrix.
3. Validate the selected UnityDoorstop handoff and early-failure recovery path.
4. Freeze the smallest public metadata and lifecycle contract.
5. Implement and test discovery, validation, and candidate reconciliation
   outside the game.
6. Add startup, activation supervision, paths, logging, and configuration.
7. Validate a sample plugin in DSP.
8. Migrate and validate the selected real consumer.
9. Define reproducible installation and package verification.

Each step should produce evidence for the next. A roadmap may divide this work
further, but must preserve the product boundaries and acceptance criteria above.
