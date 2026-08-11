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
- ordering plugins around dependencies and incompatibilities;
- constructing Unity plugin components at the correct point in startup;
- providing per-plugin logging, configuration, and paths;
- making Harmony available as a separately consumed patching library;
- supplying a conventional installation and packaging dependency.

That bundle creates an upstream dependency even when each mod uses only a small
part of the framework. DSP Plugin Manager will separate those needs, implement
only the accepted subset, and provide a controlled migration path.

## Primary users

1. **Players** need DSP to start predictably, useful diagnostics when a plugin
   cannot load, and no silent effect on saves or game behavior from the manager
   itself.
2. **Plugin authors in the owner's DSP projects** need a stable, small contract
   for metadata, startup, shutdown, logging, configuration, paths, and declared
   relationships with other plugins.
3. **Maintainers** need deterministic behavior, isolated version-sensitive
   integration, focused tests, and a migration process that can be validated
   one consumer at a time.

## Product goals

- Start a managed plugin host in the supported DSP Unity Mono process.
- Discover plugins only from documented, bounded roots.
- Inspect plugin metadata without executing plugin code where practical.
- Give every plugin a stable identifier and version.
- Validate duplicate identifiers, malformed metadata, unsupported contracts,
  dependencies, incompatibilities, and dependency cycles before activation.
- Produce one deterministic activation plan from the validated plugin set.
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

- The same plugin set and configuration produce the same validation result and
  activation order.
- Filesystem enumeration order never determines lifecycle order.
- A plugin is identified by a stable, case-defined identifier rather than a
  filename or display name.
- Duplicate identifiers are rejected before either duplicate is activated.
- Missing hard dependencies block the dependent plugin. Missing optional
  dependencies do not.
- An incompatibility has an explicit diagnostic and deterministic outcome.
- Dependency cycles are reported as cycles, not disguised as arbitrary order.
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
Validated -----> Blocked
    |
Planned
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

- **Rejected** means the candidate itself is malformed, duplicated, or
  unsupported.
- **Blocked** means a valid candidate cannot be activated because its declared
  environment, dependency, version, cycle, or incompatibility rules are not
  satisfied.
- **Failed** means plugin code threw or otherwise failed during activation.
- **Active** means the host completed its part of activation; it does not prove
  the plugin's gameplay feature works.
- **Stopped** means supported shutdown callbacks completed. It does not promise
  managed assemblies were unloaded from the process.

Every non-success state must carry an actionable diagnostic. A later design
must define which dependent plugins become blocked when another plugin fails
during activation; that policy must be deterministic and tested before the
host is called usable.

## Dependency semantics

The minimum relationship model distinguishes:

- required dependency by stable identifier;
- optional dependency by stable identifier;
- accepted version constraint when version checks are supported;
- explicit incompatibility;
- deterministic tie-breaking for otherwise independent plugins.

The planner must detect duplicate identifiers and cycles. It must not infer a
hard dependency from assembly references alone. Exact version-range syntax and
case sensitivity remain open decisions and must be documented before metadata
is frozen.

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
 Validation and deterministic dependency planner
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

Gets managed control inside the DSP process. The mechanism may use an existing
bootstrap component or a project-owned adapter, but ownership, licensing,
installation, update behavior, and failure reporting must be explicit. The
bootstrap mechanism is not selected by this document.

### Host startup and environment

Establishes the game root, managed-assembly location, plugin roots,
configuration root, log destination, host version, and supported process/runtime
before discovery begins. Path calculation must be centralized and testable.

### Discovery and metadata inspection

Enumerates bounded roots, identifies managed assembly candidates, and reads
plugin metadata. Discovery should avoid loading candidate assemblies into the
game's primary execution context until a candidate is eligible for activation.

### Validation and dependency planning

Owns identity validation, supported contract checks, version constraints,
dependencies, optional dependencies, incompatibilities, cycles, and stable
ordering. This layer must be deterministic and testable without DSP.

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
contracts. It does not own discovery, dependency policy, or lifecycle state.
Compatibility code must remain removable and measurable per migrated consumer.

## Migration strategy

Migration is consumer-driven:

1. inventory the BepInEx surface actually used by one selected mod;
2. classify each use as lifecycle, service, patching, build, packaging, or
   incidental;
3. choose source adaptation or compatibility support explicitly;
4. add the minimum host contract and deterministic tests;
5. build the consumer without its BepInEx dependency;
6. validate startup, behavior, diagnostics, and shutdown in DSP;
7. remove the package dependency only after that evidence passes;
8. repeat for the next consumer without generalizing unsupported APIs.

The first migrated mod should be representative enough to exercise metadata,
logging, configuration, Unity lifecycle, and Harmony integration while being
small enough to diagnose. Migration order is not selected here.

Binary compatibility is not assumed. If it is proposed, acceptance must cover
assembly identity, referenced type and member signatures, metadata behavior,
Unity component construction, configuration persistence, logging, dependency
semantics, exception behavior, and real unchanged plugin binaries.

## Patching boundary

The existing consumers use Harmony to modify DSP behavior. Harmony patch
creation, ownership, and cleanup belong to each plugin unless a later contract
establishes a narrowly shared service.

The manager may make an approved Harmony binary available through installation
or packaging, but doing so is a distribution and licensing decision. It does
not make Harmony part of the manager's implementation.

## Configuration and logging principles

The initial service contracts should preserve outcomes the consumers need,
without reproducing an entire upstream API:

- configuration is namespaced per stable plugin identifier;
- defaults, parsing failures, persistence timing, and file locations are
  deterministic and documented;
- malformed configuration fails locally and visibly;
- log records include timestamp, severity, source/plugin identity, and message;
- startup diagnostics identify discovery, rejection, blocking, ordering, and
  activation outcomes;
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

1. metadata fixtures for valid, malformed, duplicate, and unsupported plugins;
2. dependency fixtures for hard, optional, versioned, incompatible, cyclic,
   independent, and deterministic-order cases;
3. lifecycle fixtures for activation failure, dependent blocking, shutdown,
   and service isolation;
4. a sample plugin exercising the public contract;
5. an installed-game cold-start check with actionable host logs;
6. one migrated real consumer with its BepInEx build and package dependency
   removed;
7. focused in-game behavior and shutdown validation for that consumer;
8. repeatable package-layout verification.

Compilation alone proves none of the runtime lifecycle or migration claims.
Acceptance evidence must identify the DSP version, host build, plugin build,
installation layout, and exact scenario exercised.

## Initial release acceptance

The first release suitable for a consumer migration must demonstrate:

- reproducible installation into a clean supported DSP layout;
- successful managed startup without BepInEx providing the plugin lifecycle;
- bounded discovery and deterministic dependency planning;
- actionable diagnostics for every rejected, blocked, or failed fixture;
- per-plugin logging, configuration, and path isolation;
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

- process bootstrap mechanism and who owns its distribution;
- public plugin contract name, namespace, and assembly identity;
- source-adaptation versus binary-compatibility strategy;
- metadata encoding and version-range syntax;
- identifier case sensitivity;
- plugin directory and package layout;
- configuration format and any BepInEx `.cfg` migration behavior;
- activation-failure behavior for plugins whose dependency was planned but
  failed at runtime;
- supported shutdown semantics and Unity timing;
- Harmony acquisition and distribution policy;
- first migration consumer;
- final Thunderstore installation layout and publication policy.

Resolve these decisions with a focused proposal and evidence. Do not settle
them accidentally through the first convenient implementation.

## Near-term sequence

1. Inventory the BepInEx API and packaging surface used across candidate
   consumer mods.
2. Select the first migration consumer and define its acceptance matrix.
3. Decide the bootstrap and migration compatibility approach.
4. Freeze the smallest public metadata and lifecycle contract.
5. Implement and test discovery, validation, and dependency planning outside
   the game.
6. Add startup, activation supervision, paths, logging, and configuration.
7. Validate a sample plugin in DSP.
8. Migrate and validate the selected real consumer.
9. Define reproducible installation and package verification.

Each step should produce evidence for the next. A roadmap may divide this work
further, but must preserve the product boundaries and acceptance criteria above.
