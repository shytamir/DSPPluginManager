# DSP Plugin Manager Roadmap

## Purpose

This roadmap is the approved bounded implementation plan for Milestone 3.
Current product decisions and status remain authoritative in
[`PROJECT.md`](PROJECT.md), and completed story history remains in the
[roadmap archive](archive/ROADMAP-MILESTONE-1.md).

The roadmap implements only the remaining per-plugin configuration and
configurable-shortcut host features, then produces the contract comparison and
instructions needed by downstream migrators. It does not migrate either
consumer or establish the final publishable package.

## Current status

- **Roadmap:** Active
- **Active milestone:** Milestone 3 — Configuration and shortcut migration
  readiness
- **Active story:** RM-24 — Public configuration and shortcut contract slice
- **Active story status:** Pending implementation
- **In progress:** None

## Working rules

- Implement one accepted story at a time in the listed order.
- A story may use accepted outcomes from an earlier story but must not require
  parallel changes in either consumer repository.
- Each implementation story ends with focused deterministic checks.
- Public surface and persisted behavior are changed only by the story that owns
  that contract.
- Only the project owner may accept a story or declare it completed.
- Real consumer edits, final installation packaging, and publication remain
  outside this roadmap.

## Existing foundation

Milestones 1 and 2 delivered the deterministic pre-activation host, supervised
Unity lifecycle, plugin logger and writable root, and the pinned manager-owned
Harmony closure. The public plugin contract currently has no configuration or
shortcut surface. Required plugin startup work uses explicit `Activate()` after
host services are prepared; ambient private Unity `Awake` is not a supervised
startup boundary.

The remaining host requirements are R-13 through R-15 from the private feature
map. FM-05 requires safe per-plugin persistence, including Guide's late-bound
save-specific keys. FM-06 requires the exact non-consuming keyboard-combination
behavior used by both consumers. Mirror is the selected first migration
consumer, but both mapped consumer patterns constrain this milestone.

## Milestone 3: Configuration and shortcut migration readiness

### RM-24 — Public configuration and shortcut contract slice

**Status:** Pending implementation

**Story:** As a plugin implementer, I want the complete bounded configuration
and shortcut surface fixed before storage work begins so migration code has one
stable compile target.

**Delivers:** Exact public types and members for the plugin-owned configuration
handle, typed entries, explicit save, and shortcut value.

**Acceptance:**

- The exact assembly, namespace, type, member, mutability, and failure contracts
  are recorded in `PROJECT.md` before implementation.
- `PluginBehaviour` exposes one host-prepared configuration handle; plugin code
  can bind a section/key/default/description, read and change a typed value, and
  request an explicit save.
- The public handle has three closed `Bind` overloads returning non-constructible
  typed entries for Boolean, string, and the project-owned shortcut scalar; no
  unrestricted generic bind or converter surface makes other types accidental
  contracts.
- Mirror-shaped fixed bindings and Guide-shaped fixed, late-bound string, and
  explicit-save calls compile using only the proposed surface.
- Shortcut construction, unset state, equality, display, and `IsDown()` are
  represented without adding input registration, events, or rebinding APIs.
- Contract misuse fails synchronously, while the public delegation seams permit
  later storage failures to be diagnosed without escaping or discarding usable
  in-memory values.

**Depends on:** Accepted RM-09, RM-16, RM-17, and RM-19 contracts.

**Not included:** File parsing, persistence, Unity input calls, or consumer
migration.

**Requirements:** The public source-migration boundary of R-13 through R-15.

### RM-25 — Per-plugin configuration ownership and file policy

**Status:** Pending

**Story:** As an operator, I want every plugin configuration file owned by its
validated identity so plugins cannot collide or select arbitrary host paths.

**Delivers:** One immutable per-plugin configuration scope using a separate,
manager-owned `<canonical-identifier>.cfg` file.

**Acceptance:**

- One canonical lowercase plugin identifier owns the normalized
  `<canonical-identifier>.cfg` file beneath the project-owned, configured host
  configuration directory; traversal and cross-plugin claims are rejected.
- Scope creation is independent of the process working directory, creates or
  validates the configured parent, and returns one immutable final path for
  later lifecycle integration.
- The manager does not discover, import, move, or write an existing BepInEx
  `.cfg` file. Tests prove the manager scope is separate and cannot mutate a
  supplied BepInEx fixture; required value carry-forward is deferred to the
  RM-33 migration instructions.
- A missing or empty file is a valid empty scope. Unreadable files,
  directory-at-file collisions, and access denial produce deterministic scoped
  outcomes without overwriting the affected source.
- One plugin's file failure does not change another plugin's scope or lifecycle.

**Depends on:** RM-02, RM-17, and RM-24.

**Not included:** Section parsing, typed binding, writes, live reload, or final
manager installation layout.

**Requirements:** R-13 ownership and FM-05's selected separation policy.

### RM-26 — Sectioned parsing and value-preserving late-bound entries

**Status:** Pending

**Story:** As a Guide migrator, I want valid settings preserved before their
save-specific keys are known so early fixed bindings cannot erase inactive-save
selections.

**Delivers:** A bounded human-editable section/key document model that retains
every valid unclaimed definition and serialized value.

**Acceptance:**

- Definition identity is the ordinal case-sensitive `(section, key)` pair, with
  validation preventing whitespace, line-break, header, or assignment
  injection.
- Blank lines and comments are inert. Valid definitions and serialized values
  are parsed without executing plugin code and remain available for a later
  bind; original whitespace, comment placement, and byte layout are not
  retained contracts.
- Malformed lines are isolated and diagnosed with line context; repeated
  definitions are diagnosed and deterministically retain the last textual
  value.
- Fixed Mirror/Guide entries can be observed without claiming unrelated current
  or legacy `Phase Selection` keys.
- Repeated load and in-memory document construction produce deterministic
  definition state and diagnostics.

**Depends on:** RM-25.

**Not included:** Type conversion, entry mutation, saving, full TOML, or public
dictionary access.

**Requirements:** R-13 late binding and FM-05 parser safety.

### RM-27 — Keyboard shortcut scalar and persisted grammar

**Status:** Pending

**Story:** As a plugin implementer, I want one deterministic shortcut value so
configuration, display, equality, and later polling all share the same meaning.

**Delivers:** The immutable keyboard-only scalar and its bounded parser and
canonical serializer.

**Acceptance:**

- A value contains one main Unity `KeyCode`, a defensively copied normalized
  held-key set, or an explicit unset state that can never trigger.
- `KeyCode.None` with no held keys is the unset value; a null held-key array,
  held `None`, an unset main key with held keys, and mouse, joystick, or other
  unsupported key families are rejected as contract or parse errors rather
  than normalized into another meaning.
- Duplicate held keys and the main key are removed, remaining keys have one
  deterministic order, and value equality supports configuration change
  detection.
- An empty scalar parses and serializes as unset. Configured text accepts one or
  more case-sensitive Unity `KeyCode` names separated by `+` with optional
  surrounding ASCII spaces; the first name is the main key. Canonical persisted
  and configured display text uses ` + `, and unset display text is `Not set`.
- F8, F9, and multi-key combinations round-trip; malformed or unsupported
  keyboard names return a conversion failure rather than silently becoming
  unset.
- Comma, semicolon, and pipe separators are rejected. Their deliberate drift
  from BepInEx shortcut text is recorded for RM-33 migration documentation.

**Depends on:** RM-24 and RM-25.

**Not included:** Unity polling, mouse/gamepad guarantees, capture UI, live
rebinding, or configuration file writes.

**Requirements:** The representation and persistence portion of R-15 and the
FM-05/FM-06 boundary.

### RM-28 — Typed binding, defaults, and in-memory mutation

**Status:** Pending

**Story:** As a plugin implementer, I want stored values converted into stable
typed entries so malformed settings remain local and usable defaults survive.

**Delivers:** Binding and value semantics for exactly Boolean, string, and the
RM-27 shortcut scalar.

**Acceptance:**

- Binding claims a matching unbound value or retains the supplied default. The
  first successful bind establishes the definition's type, default, and
  description; rebinding the same definition and type returns that authoritative
  entry without changing those attributes.
- A conflicting type for an existing definition is rejected with plugin,
  section, key, and type context without changing the prior entry.
- Boolean parsing is case-insensitive with canonical lowercase output; strings
  round-trip Unicode, control characters, `;`, and `=`; shortcut failures use
  the RM-27 scoped conversion path.
- Empty strings are valid; null string defaults or assigned values are rejected
  as the RM-24 synchronous contract error rather than serialized ambiguously.
- A malformed stored value warns with expected type and reason, retains the
  supplied default, and does not affect another entry or plugin.
- Changed values update the authoritative in-memory entry; assigning an equal
  value is a no-op suitable for later autosave decisions.

**Depends on:** RM-26 and RM-27.

**Not included:** Disk writes, arbitrary converters, acceptable-value ranges,
events, or enumeration APIs.

**Requirements:** R-14 and the typed binding portion of R-13.

### RM-29 — Deterministic autosave and atomic replacement

**Status:** Pending

**Story:** As an operator, I want configuration changes persisted without
destroying the last usable file so a failed save cannot silently erase settings.

**Delivers:** Serialized whole-file persistence for new bindings, changed
values, and explicit save.

**Acceptance:**

- A new bind and a changed value request safe autosave; explicit save writes the
  complete current snapshot even after an autosave.
- Bound and still-unbound definitions are emitted in deterministic ordinal
  section/key order as UTF-8 with inert descriptions and canonical scalar text.
- A complete temporary file is written, durably flushed, and closed in the same
  directory before `File.Replace` updates an existing file or `File.Move`
  publishes a first file; no truncate-in-place or non-atomic fallback is used.
- Simulated temporary-write, flush, replace, move, and final-path failures leave
  an existing file unchanged, clean up only the owned temporary file where
  possible, keep in-memory values usable, and distinguish requested from
  persisted state in diagnostics.
- Operations are serialized per plugin; no background worker, debounce,
  cross-process coordination, or multi-file transaction is introduced.
- A scope whose existing file could not be read remains write-blocked for that
  process: autosave and explicit `Save()` diagnose the condition without
  touching the final file. Operator recovery requires correcting the condition
  and reopening the scope rather than risking an in-process overwrite.
- Repeated save and reload retains Guide's unbound current and legacy keys until
  the matching late bind claims them.

**Depends on:** RM-25, RM-26, and RM-28.

**Not included:** Live reload, file watching, history, stale-key cleanup, or
configuration UI.

**Requirements:** R-13 persistence and failure safety.

### RM-30 — Exact non-consuming shortcut polling

**Status:** Pending

**Story:** As a plugin implementer, I want configured shortcuts evaluated with
the established exact-combination rule so F8/F9 behavior does not drift during
migration.

**Delivers:** `IsDown()` over one narrow internal Unity-input adapter.

**Acceptance:**

- Polling is supported from Unity's main-thread `Update` and checks the main-key
  down edge before any held-key scan.
- On that edge every configured held key must be down and every additional held
  non-mouse keyboard key rejects the combination; mouse-button state does not.
- Unset shortcuts never query or trigger, and malformed stored values have
  already retained their configured defaults through RM-28.
- Queries are non-consuming, allowing two plugins with the same shortcut to
  observe the same frame independently.
- The Unity host installs one internal polling bridge on its admitted main
  thread. A configured shortcut rejects polling before bridge installation or
  from another thread; an unset shortcut returns false without querying it.
- `DSPPluginManager.Contracts.dll` remains free of an
  `UnityEngine.InputLegacyModule` reference; the late-loaded Unity host owns the
  legacy `Input` adapter.
- Deterministic adapter tests cover no edge, missing keys, exact matches, extra
  keyboard keys, mouse coexistence, two observers, and absence of a broad scan
  on ordinary frames.

**Depends on:** RM-27 and the accepted RM-21 Unity runtime behavior.

**Not included:** DSP `VFInput` registration, input-context suppression, event
buffering, continuous/key-up queries, conflict management, or device expansion.

**Requirements:** The polling portion of R-15 and FM-06's selected default.

### RM-31 — Configuration service lifecycle integration

**Status:** Pending

**Story:** As a plugin implementer, I want my configuration handle ready for
supported startup and retained through cleanup so lifecycle behavior is
deterministic and isolated.

**Delivers:** One host-owned configuration service passed through the existing
selected-candidate activation and orderly-stop path.

**Acceptance:**

- The host creates and loads one configuration scope after selection and before
  component construction, then initializes the component's public handle
  exactly once before `Activate()`.
- Logger, writable root, configuration, lifecycle state, and runtime dependency
  context are all usable during `Activate()` and remain usable through
  `Deactivate()` return.
- The contract explicitly does not promise configuration during ambient private
  `Awake`; required BepInEx `Awake` setup must move to `Activate()` during source
  migration.
- Read, parse, conversion, or save diagnostics retain plugin identity. An
  operational file/format failure retains the documented safe configuration
  state; a scope-construction or contract failure can fail that plugin's service
  preparation, but neither outcome prevents an unrelated selected plugin from
  activating or cleaning up.
- Configuration is neither cleared nor disposed before `Deactivate()` returns.
  Later release of supervisor references does not delete the file or claim
  assembly unload; use of a retained handle after `Deactivate()` returns is
  outside the supported lifecycle contract.

**Depends on:** RM-24 through RM-30 and accepted RM-19 through RM-22 lifecycle
behavior.

**Not included:** Runtime enable/disable, retry, global host configuration, or
consumer source changes.

**Requirements:** R-13 through R-15 lifecycle integration and R-10 isolation.

### RM-32 — Both-consumer configuration pattern qualification

**Status:** Pending

**Story:** As a migration maintainer, I want both mapped configuration patterns
exercised before real repository changes so host omissions are found upstream.

**Delivers:** Deterministic Mirror-shaped and Guide-shaped fixtures using only
the completed manager contract.

**Acceptance:**

- The Mirror-shaped fixture binds its three fixed Boolean/shortcut settings,
  observes default and stored values, and displays/polls the configured shortcut.
- The Guide-shaped fixture binds its fixed settings, preserves unbound current
  and legacy save keys through early autosaves, claims them late, changes the
  active value, and performs explicit save.
- Dispose-and-reopen fixtures prove Boolean, string, unset/F8/F9, and a
  multi-key shortcut round-trip without cross-plugin file or entry collisions;
  installed process restart remains RM-34's responsibility.
- Malformed values and one plugin's read/write failure retain defaults and
  diagnostics while the other fixture remains usable.
- The Mirror-shaped build uses only the documented Unity, manager-contract, and
  manager-owned `0Harmony` references; the Guide-shaped build uses only Unity
  and the manager contract. Neither output contains a `BepInEx` reference or a
  private Harmony/MonoMod/Cecil copy.

**Depends on:** RM-31.

**Not included:** Changes to Mirror or Guide repositories, installed DSP input
automation, or publication packaging.

**Requirements:** Complete deterministic coverage of R-13 through R-15 for both
mapped consumer shapes.

### RM-33 — Migration drift specification and implementer instructions

**Status:** Pending

**Story:** As a Mirror or Guide implementer, I want one authoritative migration
document so source, configuration, build, package, and user-documentation changes
can be made without rediscovering host behavior.

**Delivers:** A reproducible compile-reference kit plus a reviewed migration
contract comparison and step-by-step, target-specific implementation
instructions derived from the completed host.

**Acceptance:**

- The document maps existing BepInEx metadata, base class, `Awake`/`OnDestroy`,
  logger, configuration, shortcut, writable-root, coroutine, and Harmony wiring
  to the exact manager contract or an explicit retained plugin responsibility.
- It records every known behavioral drift: explicit lifecycle callbacks,
  service availability boundary, file location/extension and existing `.cfg`
  treatment, case-sensitive definitions, late-entry retention, autosave and
  failure behavior, shortcut grammar/exactness, and input-context differences.
- It identifies required source and documentation string changes, including
  BepInEx log/config/root references and consumer-owned diagnostic locations.
- It gives Mirror-first and Guide-following checklists for project references,
  CI acquisition, tests, manifest dependency, package path, artifact validation,
  and removal of `BepInEx.dll`; unresolved final packaging choices are clearly
  separated from source work that can begin.
- The repository build emits a validated migration-reference kit containing
  only the built `DSPPluginManager.Contracts.dll`, the approved plugin-facing
  `0Harmony.dll`, required third-party notices, integrity metadata, and the
  migration instructions. The guide gives one reproducible local/CI acquisition
  procedure pinned to a manager revision; it does not present the kit as an
  installable runtime package.
- Instructions are complete for source, build, deterministic-test, and user
  documentation migration. Manifest dependency, final plugin payload path, and
  publication steps that require the later installation contract are named as
  explicit blocked substitutions rather than guessed or omitted.
- All contract names and code fragments are validated against the built public
  assembly and the RM-32 fixtures; the instructions do not claim that either
  consumer has already migrated.

**Depends on:** RM-24 through RM-32 and accepted Milestones 1 and 2.

**Not included:** Editing downstream repositories, final Thunderstore
publication, compatibility shims, or preserving BepInEx assembly identity.

**Requirements:** FM-10 migration preparation plus documented FM-05/FM-06 drift.

### RM-34 — Installed migration-readiness qualification

**Status:** Pending; detailed evidence matrix to be designed before
implementation

**Story:** As the project owner, I want one reversible installed DSP
qualification proving the completed host contract is ready for downstream
implementers to begin migration.

**Delivers:** The Milestone 3 installed exit harness and retained evidence
record using the RM-32 consumer-shaped fixtures and RM-33 instructions.

**Acceptance boundary:**

- The qualification uses the built bootstrap bundle and no BepInEx-provided
  lifecycle or service assembly.
- It exercises both consumer-shaped configuration patterns, persisted shortcut
  values, supported Unity polling across two DSP launches, independent failure,
  orderly cleanup, and the previously accepted Harmony availability.
- Evidence identifies exact builds and files, relates every observation to the
  migration instructions, and records exact restoration of the installed game
  environment.
- The detailed run sequence, input stimuli, restart boundary, observable counts,
  and retained evidence fields will be frozen in the separate exit-test design
  review before this story can be implemented.

**Depends on:** RM-32 and RM-33.

**Not included:** A migrated real consumer, final installation package,
publication, or claims beyond the accepted two consumer patterns.

**Requirements:** Installed closure of R-13 through R-15 and the milestone's
ready-for-migration objective.

## Milestone 3 exit — planned

Milestone 3 will be complete when RM-24 through RM-34 are accepted and the
approved installed qualification demonstrates that the manager supplies every
host-side interface and behavior required to begin Mirror migration without
BepInEx. Both consumer-shaped patterns must remain covered so Guide migration
does not uncover a known host omission later.

“Ready for migration” will mean that the public contract is complete for the
mapped consumers, configuration and shortcut drift is decided and documented,
the implementer instructions are validated against built fixtures, and the
installed host passes the approved readiness test. It will not mean that Mirror
or Guide has been migrated, that final Thunderstore installation/publication is
settled, or that the product release steering gate has passed.

The exact installed exit procedure and evidence matrix are deliberately left
for the next design review after this story decomposition is approved.
