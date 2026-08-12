# DSP Plugin Manager Roadmap History

## Purpose

This roadmap was the ordered implementation plan for Milestone 1 and remains
its story history. Current product decisions and status remain authoritative in
[`PROJECT.md`](../PROJECT.md).

The first milestone was deliberately bounded. It ended after the manager
started inside DSP, established paths and diagnostics, discovered plugin
candidates without executing them, and deterministically selected at most one
candidate per plugin identity. Plugin activation, configuration, keyboard
shortcuts, consumer migration, and final publication remained outside it.

## Completion status

- **Roadmap:** Completed
- **Completed milestone:** Milestone 1 — Deterministic pre-activation host
- **Stories:** RM-01 through RM-12 accepted by the project owner
- **Exit check:** Completed; see
  [`MILESTONE-1-EXIT-EVIDENCE.md`](MILESTONE-1-EXIT-EVIDENCE.md)
- **Succeeded by:** Milestone 2 roadmap below

## Working rules

- Stories were implemented one at a time in the listed order.
- A story could use completed outcomes from earlier stories but did not require
  parallel implementation in another repository.
- Each story ended with its focused automated checks and any stated DSP check.
- No story expanded the public contract or compatibility claim beyond its
  explicit acceptance criteria.
- Completed stories remain in this document as concise history.

## Existing foundation

At the start of this roadmap, the repository already provided three-part build
versioning, temporary Thunderstore package validation, and reproducible
acquisition and integrity validation of the accepted
HarmonyX/MonoMod/Mono.Cecil stack. These were inputs to the stories below,
alongside the compiled `net472` foundation,
immutable environment paths, independent bootstrap failure record, and reserved
dependency resolver. They did not alone establish a working host.

## Milestone 1: Deterministic pre-activation host

### RM-01 — Compiled host foundation

**Status:** Accepted

**Story:** As a maintainer, I want real versioned `net472` product assemblies
and a focused test project so every later feature is compiled and exercised on
the supported consumer baseline.

**Delivers:** A minimal host/entry build replacing the zero-byte artifact, with
deterministic version stamping and CI validation.

**Acceptance:**

- The product builds for `net472` with C# 7.3-compatible public code.
- Build validation inspects the real assembly identity and version.
- The existing package pipeline packages the compiled artifact while still
  describing the product as unavailable.
- No game, Unity, or third-party binary is committed.

**Depends on:** Existing repository automation only.

**Not included:** Plugin APIs, Unity startup, discovery, or runtime behavior.

**Requirements:** Technical baseline and FM-10 build scaffold.

### RM-02 — Immutable host environment paths

**Status:** Accepted

**Story:** As host code, I want one validated immutable path model so every
subsystem uses explicit absolute locations instead of the process working
directory.

**Delivers:** A deterministic environment component built from trusted
bootstrap inputs and an explicitly supplied host root.

**Acceptance:**

- Executable, managed, host, plugin, configuration, log, dependency, and
  writable-output paths are normalized once and immutable.
- Relative paths, empty inputs, traversal outside an allowed root, and a file
  where a required directory belongs fail with actionable context.
- Tests prove results are independent of the current working directory.
- No plugin-facing path surface or final installation default is selected.

**Depends on:** RM-01.

**Not included:** Directory cleanup, filesystem watching, or consumer file
management.

**Requirements:** R-04 and the internal scaffold of FM-07.

### RM-03 — Independent bootstrap failure record

**Status:** Accepted

**Story:** As an operator, I want an early plain-text failure record so a host
startup failure remains diagnosable before normal logging is available.

**Delivers:** A bounded, synchronous emergency diagnostic writer usable from
the minimal managed entrypoint.

**Acceptance:**

- The record identifies the bootstrap phase, target assembly, available
  bootstrap paths, and complete exception.
- Its location is derived without normal logging or Unity APIs.
- Diagnostic-write failure never hides or replaces the original startup
  failure.
- Tests cover success, unavailable directories, denied writes, and safe
  formatting of multiline exceptions.

**Depends on:** RM-02.

**Not included:** Normal plugin logging, log rotation, or telemetry.

**Requirements:** R-02 and FM-01's necessary failure scaffold.

### RM-04 — Reserved dependency resolver

**Status:** Accepted

**Story:** As the host, I want the approved Harmony/Cecil identities resolved
only from manager-owned files so an accidental or plugin-local copy cannot win
by load order.

**Delivers:** A narrow early resolver over the existing locked dependency
staging directory.

**Acceptance:**

- Approved requests resolve to the validated host-owned path and exact assembly
  identity.
- Missing, corrupt, wrong-version, already-loaded-conflict, and plugin-local
  duplicate cases fail deterministically with requesting-assembly context.
- Unrelated assembly names are left to the runtime's ordinary resolution.
- Fixture tests cover the full success and rejection matrix without DSP.

**Depends on:** RM-01 through RM-03 and the existing dependency lock.

**Not included:** General plugin probing, highest-version binding, public
resolver hooks, or Harmony patch ownership.

**Requirements:** The unresolved runtime portion of R-17 and FM-08/09.

### RM-05 — Unity main-thread handoff decision probe

**Status:** Accepted

**Story:** As a maintainer, I want both bounded handoff candidates exercised in
the supported DSP build so the production bootstrap uses evidence rather than
an assumed Unity callback.

**Delivers:** A disposable probe comparing `RuntimeInitializeOnLoadMethod` with
the default narrow in-memory Cecil handoff.

**Acceptance:**

- Each candidate is tested from a Doorstop-preloaded external assembly against
  the recorded DSP and Unity build.
- Evidence records callback count, Unity-main-thread execution, timing, normal
  game startup, disabled startup, and complete early-failure diagnostics.
- Exactly one mechanism is selected and the decision is recorded in
  `PROJECT.md` before production implementation.
- Probe-only files and behavior do not enter the public plugin contract.

**Depends on:** RM-01 through RM-04.

**Not included:** Plugin discovery, activation, or broad IL patching.

**Requirements:** R-03 and FM-01's sole remaining bootstrap experiment.

### RM-06 — Pinned UnityDoorstop managed bootstrap

**Status:** Accepted

**Story:** As an operator, I want a reversible manager-owned bootstrap bundle so
DSP can enter the compiled host exactly once without BepInEx.

**Delivers:** Reproducible UnityDoorstop 3.4 acquisition, reviewed provenance,
manager-owned configuration, the minimal managed entrypoint, and the selected
RM-05 main-thread handoff.

**Acceptance:**

- The pinned Windows artifact, architecture, hash, CC0 notice, and source are
  validated during the build.
- `doorstop_config.ini` targets the manager entry assembly and has documented
  collision, disable, and removal behavior.
- The early entrypoint validates its environment, installs only required
  resolution, avoids Unity APIs, and starts the host at most once.
- An installed DSP check proves enabled startup, disabled startup, clean
  removal, one main-thread callback, and no on-disk modification of game or
  Unity managed assemblies.

**Depends on:** RM-01 through RM-05.

**Not included:** Final Thunderstore layout, automatic installation, other
platforms, runtime fixes, or preloader plugins.

**Requirements:** R-01 through R-03 and FM-01.

### RM-07 — Source-scoped non-throwing logging core

**Status:** Accepted

**Story:** As host and plugin code, I want retained source-bound logger handles
so every diagnostic has stable attribution and logging failure cannot alter a
lifecycle outcome.

**Delivers:** Immutable host/plugin source context, information/warning/error
records, safe payload formatting, and a small internal sink boundary.

**Acceptance:**

- Every record contains timestamp, severity, stable identifier, display name,
  and message.
- Unicode, null values, multiline exceptions, and formatting failures remain
  valid attributable records.
- Overlapping callers produce whole records and sink failure never escapes the
  logger call.
- No public listener, source-registration, filtering, or structured-query API
  is introduced.

**Depends on:** RM-01.

**Not included:** Disk file lifecycle or Unity/trace/Harmony log capture.

**Requirements:** R-11 and the formatting boundary of FM-04.

### RM-08 — Authoritative current-run disk log

**Status:** Accepted

**Story:** As an operator, I want one readable current-run log so bootstrap,
discovery, and later plugin failures can be diagnosed in chronological context.

**Delivers:** A synchronized UTF-8 disk sink beneath the host environment.

**Acceptance:**

- The primary file is readable while DSP runs and is replaced for a new run.
- A bounded alternate filename is used when the primary cannot open, and total
  sink failure is reported through RM-03 without recursion.
- The selected filename and bounded fallback rule are recorded in `PROJECT.md`
  before implementation.
- Buffered output flushes periodically and synchronously on orderly disposal.
- Tests cover overlapping writes, fallback selection, write/flush failures,
  and continued non-throwing logger behavior.

**Depends on:** RM-02, RM-03, and RM-07.

**Not included:** Unbounded append, rotation, previous-run retention, console
output, or plugin-selected sinks.

**Requirements:** R-12 and FM-04.

### RM-09 — Minimal discovery contract slice

**Status:** Accepted

**Story:** As a plugin author, I want one exact metadata and base-type contract
so a compiled plugin can declare its identity and be recognized without
depending on BepInEx.

**Delivers:** The smallest public assembly slice needed by static discovery and
a consumer-shaped compile fixture.

**Acceptance:**

- A focused decision records the contract assembly identity, namespace,
  metadata marker encoding, base-type identity, identifier comparer, and valid
  identifier/version rules in `PROJECT.md`.
- The contract represents stable identifier, display name, and canonical
  three-part version only.
- A fixture compiles for `net472` and its binary metadata can be read without
  executing its code.
- Unity reference inputs are supplied externally for compilation and are absent
  from repository and package artifacts.
- No lifecycle services, configuration, logging members, BepInEx identity, or
  binary-compatibility claim is added.

**Depends on:** RM-01.

**Not included:** A complete consumer migration contract or plugin activation.

**Requirements:** The contract prerequisite for R-05 and R-06.

### RM-10 — Bounded deterministic candidate enumeration

**Status:** Accepted

**Story:** As the host, I want one configured plugin tree enumerated
deterministically so filesystem order and path aliases cannot change which
files are inspected.

**Delivers:** A pure candidate-file enumerator with path normalization and local
filesystem diagnostics.

**Acceptance:**

- Only `.dll` paths beneath the configured root are returned in ordinal
  deterministic order.
- Canonical aliases are inspected once and links outside the root are not
  followed.
- Unreadable entries do not abort unrelated enumeration.
- Reordered fixture creation produces identical results and diagnostics.

**Depends on:** RM-02 and RM-08.

**Not included:** Metadata recognition, candidate selection, arbitrary scan
roots, caching, or hot discovery.

**Requirements:** The enumeration portion of R-05 and FM-02.

### RM-11 — Static plugin metadata recognition

**Status:** Accepted

**Story:** As the host, I want candidate metadata inspected without runtime
loading so rejected or superseded plugin code never contaminates the game
application domain.

**Delivers:** Bounded Cecil-based recognition producing immutable candidate
records from RM-09 contract fixtures.

**Acceptance:**

- The reader recognizes only a concrete supported base type with valid metadata
  and records identifier/comparison key, display name, version, assembly
  identity/path, and type name.
- Resolution is bounded to approved contract, dependency, DSP/Unity input, and
  relevant candidate locations.
- Non-managed, malformed, missing-reference, invalid-metadata, abstract, and
  multiple-eligible-type cases are diagnosed locally without code execution.
- Tests prove candidate inspection does not load fixture assemblies into the
  execution context.

**Depends on:** RM-04, RM-09, and RM-10.

**Not included:** Plugin activation, dependency attributes, process filters,
multiple plugin models, or public loader APIs.

**Requirements:** R-05, R-06, and FM-02 recognition scaffold.

### RM-12 — Deterministic candidate reconciliation

**Status:** Accepted

**Story:** As an operator, I want duplicate placements and versions reconciled
before activation so the selected plugin set is stable, explainable, and never
depends on filesystem order.

**Delivers:** A pure reconciliation result containing selected, redundant,
superseded, ambiguous, and rejected candidate states.

**Acceptance:**

- Canonical aliases are inspected once; identical same-version copies retain
  one ordinal path and report the others as redundant.
- The highest valid version is selected and every lower version is reported as
  superseded.
- Equal-version candidates with different content, type, or assembly identity
  reject the whole identity group as ambiguous.
- Multiple eligible types are rejected, and failure after selection never
  falls back to an older candidate.
- Varied fixture creation order produces identical states, selected paths, and
  diagnostics.

**Depends on:** RM-11.

**Not included:** Activation, plugin dependency graphs, incompatibilities,
load-order contracts, retry, or fallback.

**Requirements:** R-07 and FM-02 reconciliation policy.

## Milestone 1 exit — completed

An installed DSP run reached the host once without BepInEx providing the
lifecycle, opened the documented current-run log, discovered a fixture tree
without executing or runtime-loading candidate code, and reported the same
ordered candidate plan as the deterministic test suite. The result is recorded
in [`MILESTONE-1-EXIT-EVIDENCE.md`](MILESTONE-1-EXIT-EVIDENCE.md).

This exit did not claim that any plugin could be activated or migrated. Those
capabilities remained for a later roadmap.

---

## Milestone 2 Roadmap

### Purpose

This roadmap was the ordered implementation plan for Milestone 2 and remains
its story history. Current product decisions and status remain authoritative in
[`PROJECT.md`](../PROJECT.md).

Milestone 2 was deliberately bounded around supervised Unity activation. It
started from the accepted deterministic candidate plan and stopped before
configuration, configurable shortcuts, real-consumer migration, final package
layout, or publication.

### Completion status

- **Roadmap:** Completed
- **Completed milestone:** Milestone 2 — Supervised Unity activation
- **Stories:** RM-13 through RM-23 accepted by the project owner
- **Exit check:** Completed; see
  [`MILESTONE-2-EXIT-EVIDENCE.md`](../MILESTONE-2-EXIT-EVIDENCE.md)
- **Follow-up:** Next-roadmap planning was pending at archival

### Working rules

- Stories were implemented one at a time in the listed order.
- A story could use completed outcomes from earlier stories but did not require
  parallel implementation in another repository.
- Each story ended with its focused automated checks and any stated DSP check.
- No story expanded the public contract or compatibility claim beyond its
  explicit acceptance criteria.
- Only the project owner accepted a story or declared it completed.
- Completed stories remain in this document as concise history.

### Existing foundation

Milestone 1 delivered the compiled `net472` host, immutable environment paths,
independent bootstrap diagnostics, reserved dependency resolution, the validated
UnityDoorstop handoff, source-scoped current-run logging, the minimal discovery
contract, and deterministic static candidate reconciliation. Its installed exit
proved that DSP reached the host once without BepInEx providing the lifecycle
and produced the same seven-entry candidate plan as the offline tests without
runtime-loading or executing candidate code.

Those accepted outcomes and the pinned manager-owned HarmonyX/MonoMod/Mono.Cecil
stack were inputs to the stories below. At the start of the roadmap, they were
not evidence that a plugin could be runtime-loaded, activated, supervised, or
stopped.

### Milestone 2: Supervised Unity activation

Milestone 2 picked up at the accepted Milestone 1 reconciliation plan. It ended
when selected fixture plugins could be loaded and supervised as real persistent
Unity components, receive the first two required plugin services before
startup, continue through the exercised Unity runtime behavior, and follow one
observable orderly stop path. It also exercised the pinned manager-owned
Harmony stack through that lifecycle.

Configuration, configurable shortcuts, real-consumer migration, final package
layout, and publication remained outside this milestone. The stories were
ordered so each implementation depended only on accepted earlier outcomes.

#### RM-13 — Unity lifecycle observability decision probe

**Status:** Accepted by project owner

**Story:** As a maintainer, I want startup and cleanup failures observed in the
supported DSP build so the activation contract is based on actual Unity
behavior.

**Delivers:** A disposable fixture comparing direct private Unity messages with
the narrow explicit lifecycle seam permitted by FM-03.

**Acceptance:**

- The fixture records whether `AddComponent` exposes completion or failure of a
  derived private `Awake` and whether ordinary destruction exposes completion
  or failure of private `OnDestroy`.
- Both success and throwing cases run on Unity's main thread and record callback
  counts, timing, and complete diagnostics.
- The result selects direct Unity-message supervision or the explicit
  source-adapted seam, plus the shared-versus-per-plugin object arrangement.
- If the explicit seam is selected, the decision records its minimum callback
  signatures and how the real component retains coroutine access.
- The selected acknowledgement and shutdown semantics are recorded in
  `PROJECT.md` before production lifecycle implementation.

**Depends on:** Completed Milestone 1.

**Not included:** Production activation, service APIs, or a general Unity-event
interceptor.

**Requirements:** FM-03's sole blocking lifecycle experiment and R-08 through
R-10.

#### RM-14 — Selected assembly runtime loader

**Status:** Accepted by project owner

**Story:** As the host, I want only reconciled candidates loaded at runtime so
rejected and superseded code remains outside the execution context.

**Delivers:** A bounded loader that converts an accepted candidate record into
one verified runtime assembly and concrete type.

**Acceptance:**

- Only selected candidate records are accepted, and one selected assembly path
  is runtime-loaded at most once.
- Runtime assembly identity and the resolved concrete type exactly match the
  statically inspected record before activation can proceed.
- Missing dependencies, identity/type drift, and load failure produce
  candidate-scoped diagnostics without falling back to an older version.
- Fixture tests prove rejected, redundant, superseded, and ambiguous files are
  never runtime-loaded.

**Depends on:** RM-12.

**Not included:** Unity object construction, arbitrary probing, assembly unload,
or retry.

**Requirements:** The runtime boundary between R-07 and R-08, plus R-10 failure
context.

#### RM-15 — Deterministic lifecycle state record

**Status:** Accepted by project owner

**Story:** As an operator, I want each selected plugin to have one authoritative
lifecycle outcome so logs cannot confuse loading, activation, and cleanup.

**Delivers:** An internal per-identity state record and validated transition
rules shaped by RM-13.

**Acceptance:**

- The accepted transition model records its initial state and gives exact
  meanings to `Activating`, `Active`, `Failed`, `Stopping`, `Stopped`, and
  `StopFailed`.
- Invalid, duplicate, and out-of-order transitions are rejected
  deterministically without altering the prior state.
- Failure records retain identifier, version, path, type, phase, and complete
  exception context.
- `Stopped` and `StopFailed` never imply managed assembly unloading.

**Depends on:** RM-13 and RM-14.

**Not included:** Retry, enable/disable, restart, persistence, or a public
lifecycle registry.

**Requirements:** R-08 through R-10 and FM-03's necessary state scaffold.

#### RM-16 — Plugin logging contract slice

**Status:** Accepted by project owner

**Story:** As a plugin author, I want a retained plugin logger available before
startup so migrated code and helpers can emit attributable diagnostics.

**Delivers:** The smallest public logging surface backed by the accepted RM-07
and RM-08 implementation.

**Acceptance:**

- A focused decision records the exact public type and member names before
  implementation.
- The surface exposes only information, warning, and error emission and cannot
  change its stable identifier or display-name attribution.
- The handle can be retained and passed to helpers, and formatting or sink
  failure never escapes a call.
- A consumer-shaped compile fixture exercises all three severities without
  exposing listeners, sinks, filters, or source registration.

**Depends on:** RM-07 through RM-09.

**Not included:** Configuration, structured logging, Unity-log forwarding, or
plugin-selected destinations.

**Requirements:** R-11 and the plugin-facing slice of FM-04.

#### RM-17 — Plugin writable-root contract slice

**Status:** Accepted by project owner

**Story:** As a plugin author, I want one immutable writable root owned by my
stable identity so output files do not depend on BepInEx paths or the working
directory.

**Delivers:** A minimal public path surface over the accepted immutable host
environment.

**Acceptance:**

- A validated plugin identifier deterministically owns one normalized absolute
  child beneath an explicitly supplied writable parent.
- The path exists before plugin startup, remains unchanged for the process, and
  is independent of the current working directory.
- Consumer-shaped fixtures round-trip bounded UTF-8 files in separate plugin
  roots without cross-plugin path collisions.
- No game, executable, managed, dependency, cache, or arbitrary host path is
  exposed.

**Depends on:** RM-02 and RM-09.

**Not included:** Final physical installation location, quotas, cleanup,
watching, or confinement claims.

**Requirements:** R-16 and FM-07.

#### RM-18 — Persistent Unity host container

**Status:** Accepted by project owner

**Story:** As the host, I want one persistent Unity container created at the
validated handoff so plugin components survive scene changes without duplicate
roots.

**Delivers:** The RM-13-selected shared or per-plugin object arrangement,
created once on Unity's main thread.

**Acceptance:**

- Creation is admitted only after the established Unity handoff and only on
  Unity's main thread.
- The active host root is created once, marked persistent, and is not duplicated
  by repeated handoff attempts or representative scene changes.
- Object identity and destruction ownership are retained internally for later
  activation and cleanup.
- No candidate assembly is loaded and no plugin component is attached by this
  story.

**Depends on:** RM-13 and RM-15.

**Not included:** Plugin activation, scene services, prefabs, assets, or
manager-facing enable/disable controls.

**Requirements:** The persistent-container scaffold of R-08 and FM-03.

#### RM-19 — One selected plugin activation

**Status:** Accepted by project owner

**Story:** As an operator, I want one selected candidate activated once as a
real Unity component so its accepted startup behavior can run without BepInEx.

**Delivers:** The first production path from a reconciled candidate through
runtime loading, service preparation, and supervised Unity activation.

**Acceptance:**

- The host establishes `Activating`, logger attribution, and writable-root
  context before plugin startup can observe them.
- The exact inspected type is attached once as a real, initially enabled
  `MonoBehaviour` on the RM-18 container and its instance is retained by stable
  identity.
- The RM-13 acknowledgement moves the plugin to `Active`; assembly load or
  `AddComponent` return alone does not.
- Repeated activation requests cannot create a second authoritative instance.

**Depends on:** RM-14 through RM-18.

**Not included:** Configuration, multiple-plugin continuation, frame/coroutine
qualification, or shutdown.

**Requirements:** R-08, R-11, R-16, and FM-03 activation ordering.

#### RM-20 — Activation failure isolation

**Status:** Accepted by project owner

**Story:** As an operator, I want one plugin's startup failure contained so an
unrelated selected plugin can still become active and remain diagnosable.

**Delivers:** Per-plugin activation supervision over a deterministic selected
set.

**Acceptance:**

- Construction, runtime-dependency, and acknowledged-startup failures produce
  `Failed` with full plugin and phase context.
- A partial failed component and its service scope are cleaned without
  destroying another plugin's component, services, or state.
- An unrelated selected fixture still reaches `Active` after another fixture
  fails.
- Failure never activates a redundant, superseded, ambiguous, or older fallback
  candidate.

**Depends on:** RM-19.

**Not included:** Retry, recovery, dependency graphs, runtime `Update` exception
interception, or process isolation.

**Requirements:** R-10 and FM-03 failure isolation.

#### RM-21 — Exercised Unity runtime delivery

**Status:** Accepted by project owner

**Story:** As a plugin author, I want ordinary Unity frame and coroutine
behavior preserved so an active component behaves like the retained consumers.

**Delivers:** A guide-shaped lifecycle fixture running on the production
activation path.

**Acceptance:**

- `Awake` occurs once before rendered-frame `Update` on Unity's main thread.
- `StartCoroutine` returns a usable handle, `yield return null` resumes on a
  later frame, and `StopCoroutine` cancels that exact handle.
- The same component instance continues across a representative DSP scene
  transition without another `Awake` or host root.
- Ambient unclaimed Unity messages are neither blocked nor advertised as
  supervised compatibility features.

**Depends on:** RM-19 and RM-20.

**Not included:** Custom schedulers, `FixedUpdate`/`LateUpdate` guarantees,
global input capture, or arbitrary Unity-event interception.

**Requirements:** The runtime-delivery portion of R-09 and FM-03.

#### RM-22 — Observable orderly shutdown

**Status:** Accepted by project owner

**Story:** As an operator, I want one supported orderly stop path so plugin
cleanup finishes before its services and the current-run log are disposed.

**Delivers:** Supervised component destruction and terminal lifecycle outcomes
using the RM-13-selected shutdown seam.

**Acceptance:**

- An active plugin moves through `Stopping` and receives its supported cleanup
  callback exactly once on Unity's main thread.
- Logger, writable-root context, runtime dependencies, and the component remain
  usable through cleanup return.
- Successful cleanup records `Stopped`; an attributable cleanup exception
  records `StopFailed` without preventing unrelated cleanup.
- The current-run log flushes and closes only after all attempted plugin
  cleanup, and neither terminal state claims assembly unload or crash cleanup.

**Depends on:** RM-20 and RM-21.

**Not included:** Forced-process cleanup guarantees, hot unload, restart, or
background shutdown orchestration.

**Requirements:** The shutdown portion of R-09 and R-10, plus R-12 disposal
ordering.

#### RM-23 — Harmony availability through plugin lifecycle

**Status:** Accepted by project owner

**Story:** As a Mirror maintainer, I want the pinned manager-owned Harmony stack
usable through activation and cleanup so plugins do not provision private
copies.

**Delivers:** A plugin-shaped fixture that consumes the exact supported Harmony
surface through the production lifecycle.

**Acceptance:**

- The fixture compiles against the manager-owned `0Harmony` reference while its
  output contains no Harmony, MonoMod, or Cecil copy.
- Activation resolves the exact pinned runtime closure only from the manager
  dependency directory and applies an attributable postfix to a fixture target.
- Orderly cleanup removes only the fixture's patches while the dependency stack
  remains available.
- A Harmony-specific resolution or patch failure reaches `Failed` or
  `StopFailed` as appropriate without preventing a non-Harmony fixture from
  completing its lifecycle.

**Depends on:** RM-04, RM-19, RM-20, and RM-22.

**Not included:** Real Mirror migration, manager-owned patch targets, other
Harmony versions, shims, or a public MonoMod/Cecil contract.

**Requirements:** R-17, R-18, and FM-08/09.

### Milestone 2 exit — completed

The installed exit run consumed the Milestone 1 candidate cases within one
12-entry plan, runtime-loaded only the six selected fixtures, and activated five
independent real Unity components with logger and writable-root services ready
before startup. It demonstrated the exercised frame, coroutine, scene, and
persistence behavior; isolated one activation failure; applied and removed one
manager-provisioned Harmony patch; recorded four `Stopped` and one `StopFailed`
orderly outcomes; and closed the current-run log after those outcomes. The
[installed evidence record](../MILESTONE-2-EXIT-EVIDENCE.md) retains the exact
results, artifact identities, and restoration boundary.

This exit did not claim configuration, configurable shortcuts, a migrated real
consumer, dependency-graph planning, or a publishable installation package.
