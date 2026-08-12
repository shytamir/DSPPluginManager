# DSP Plugin Manager Roadmap

## Purpose

This roadmap is the ordered implementation plan and story history for
Milestone 2. Current product decisions and status remain authoritative in
[`PROJECT.md`](PROJECT.md). The completed first roadmap is retained in the
[documentation archive](archive/ROADMAP-MILESTONE-1.md).

Milestone 2 is deliberately bounded around supervised Unity activation. It
starts from the accepted deterministic candidate plan and stops before
configuration, configurable shortcuts, real-consumer migration, final package
layout, or publication.

## Current status

- **Roadmap:** Active
- **Active milestone:** Milestone 2 — Supervised Unity activation
- **Active story:** RM-16 — Plugin logging contract slice
- **Active story status:** Acceptance conditions met; awaiting project-owner
  acceptance
- **In progress:** None

## Working rules

- Implement one story at a time in the listed order.
- A story may use completed outcomes from earlier stories, but must not require
  parallel implementation in another repository.
- Each story ends with its focused automated checks and any stated DSP check.
- A story does not expand the public contract or compatibility claim beyond its
  explicit acceptance criteria.
- Only the project owner may accept a story or declare it completed.
- Completed stories stay in this document as concise history.

## Existing foundation

Milestone 1 delivered the compiled `net472` host, immutable environment paths,
independent bootstrap diagnostics, reserved dependency resolution, the validated
UnityDoorstop handoff, source-scoped current-run logging, the minimal discovery
contract, and deterministic static candidate reconciliation. Its installed exit
proved that DSP reached the host once without BepInEx providing the lifecycle
and produced the same seven-entry candidate plan as the offline tests without
runtime-loading or executing candidate code.

Those accepted outcomes and the pinned manager-owned HarmonyX/MonoMod/Mono.Cecil
stack are inputs to the stories below. They are not evidence that a plugin can
yet be runtime-loaded, activated, supervised, or stopped.

## Milestone 2: Supervised Unity activation

Milestone 2 picks up at the accepted Milestone 1 reconciliation plan. It ends
when selected fixture plugins can be loaded and supervised as real persistent
Unity components, receive the first two required plugin services before
startup, continue through the exercised Unity runtime behavior, and follow one
observable orderly stop path. It also exercises the pinned manager-owned
Harmony stack through that lifecycle.

Configuration, configurable shortcuts, real-consumer migration, final package
layout, and publication remain outside this milestone. The stories are ordered
so each implementation depends only on accepted earlier outcomes.

### RM-13 — Unity lifecycle observability decision probe

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

### RM-14 — Selected assembly runtime loader

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

### RM-15 — Deterministic lifecycle state record

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

### RM-16 — Plugin logging contract slice

**Status:** Acceptance conditions met; awaiting project-owner acceptance

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

### RM-17 — Plugin writable-root contract slice

**Status:** Pending

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

### RM-18 — Persistent Unity host container

**Status:** Pending

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

### RM-19 — One selected plugin activation

**Status:** Pending

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

### RM-20 — Activation failure isolation

**Status:** Pending

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

### RM-21 — Exercised Unity runtime delivery

**Status:** Pending

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

### RM-22 — Observable orderly shutdown

**Status:** Pending

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

### RM-23 — Harmony availability through plugin lifecycle

**Status:** Pending

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

## Milestone 2 exit

Milestone 2 is complete when an installed DSP run consumes the Milestone 1
candidate plan, runtime-loads only selected fixture assemblies, activates
independent real Unity components with logger and writable-root services ready
before startup, demonstrates the exercised frame/coroutine/persistence behavior,
isolates one activation failure, applies and removes one manager-provisioned
Harmony patch, and records observable orderly stop outcomes before the
current-run log closes.

This exit does not claim configuration, configurable shortcuts, a migrated real
consumer, dependency-graph planning, or a publishable installation package.
