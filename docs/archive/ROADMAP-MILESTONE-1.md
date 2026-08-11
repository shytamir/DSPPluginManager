# DSP Plugin Manager Roadmap

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
- **Next roadmap:** Not yet planned

## Working rules

- Implement one story at a time in the listed order.
- A story may use completed outcomes from earlier stories, but must not require
  parallel implementation in another repository.
- Each story ends with its focused automated checks and any stated DSP check.
- A story does not expand the public contract or compatibility claim beyond its
  explicit acceptance criteria.
- Completed stories stay in this document as concise history.

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
