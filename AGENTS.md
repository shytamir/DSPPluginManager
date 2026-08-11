# AGENTS.md

This file defines how coding agents should work in this repository.

## 1. Core rule

Complete the requested task with the smallest coherent change that satisfies
its acceptance criteria.

Do not turn a bounded task into a general plugin-framework implementation or a
repository-wide review. Optimize for correctness, deterministic behavior,
maintainability, and reviewability.

## 2. Instruction order

Follow, in order:

1. The current user prompt.
2. This `AGENTS.md`.
3. [`docs/PROJECT.md`](docs/PROJECT.md) and other repository documentation.
4. Existing implementation and test conventions.
5. General engineering judgment.

A specific instruction overrides a general one.

## 3. Product contract

DSP Plugin Manager is a Dyson Sphere Program-specific plugin host and lifecycle
manager. Its purpose is to let the owner's DSP mods stop depending on BepInEx
while retaining the small, proven set of hosting services those mods actually
use.

Preserve these invariants unless a task explicitly changes one:

- scope is Dyson Sphere Program's supported Unity Mono environment, not a
  general-purpose or cross-game modding framework;
- plugin discovery, validation, dependency planning, and activation are
  deterministic and observable;
- one invalid or failed plugin does not silently corrupt the lifecycle of
  unrelated plugins;
- hard dependencies, optional dependencies, incompatibilities, and load order
  remain distinct concepts;
- plugin identity is stable and duplicate identities are rejected clearly;
- lifecycle failures are reported with enough context to diagnose them;
- the host does not modify game or save data merely by managing plugins;
- game, Unity, and third-party framework assemblies are development inputs and
  are not redistributed without an explicit, license-compatible decision;
- compatibility is claimed only after it is exercised by tests or a documented
  in-game validation;
- broad BepInEx feature parity is not a product goal.

Read `docs/PROJECT.md` before changing public contracts, lifecycle behavior,
dependency resolution, compatibility behavior, bootstrap integration,
configuration, logging, paths, or packaging.

## 4. Current project state

The repository is in bootstrap and contract-definition phase. Documentation can
describe intended behavior, but must distinguish among:

- an accepted product requirement;
- a proposed design;
- an open decision;
- implemented behavior;
- validated behavior.

Do not present a planned loader, API, build, package, or compatibility layer as
available until it exists and has the stated evidence.

## 5. Scope discipline

Inspect and modify only:

- files named in the task;
- files directly required to implement it;
- directly affected tests and validation code;
- directly affected documentation.

Do not:

- reimplement adjacent BepInEx functionality without an accepted need;
- fix unrelated defects;
- modernize or reorganize nearby code without necessity;
- upgrade unrelated dependencies;
- add speculative abstraction layers;
- broaden platform, game, loader, or package-manager support implicitly;
- copy reference implementations merely because they already solve a similar
  problem.

Mention relevant unrelated findings in the final report. Do not fix them unless
they block the requested task.

## 6. Before editing

1. Run `git status --short` and preserve existing user changes.
2. Read the directly relevant contract and implementation.
3. Identify the smallest viable change and its compatibility impact.
4. Identify the narrowest useful validation.
5. Edit once the task is sufficiently understood.

Resolve minor ambiguity from repository evidence. Ask only when a missing
decision would materially change the public contract, migration path, or safety
of the result.

## 7. Mutating versus non-mutating work

Treat requests to inspect, review, analyze, explain, or plan as non-mutating
unless they explicitly ask for changes.

Unless the prompt says `PLAN ONLY`, an explicit request to fix, implement,
author, update, or deploy is implementation work:

1. inspect;
2. implement;
3. validate;
4. repair failures caused by the change;
5. review the final diff;
6. commit only when requested;
7. push only when explicitly requested.

## 8. Architectural boundaries

Keep these responsibilities separate as the implementation takes shape:

```text
DSP process bootstrap
        |
Host startup and environment paths
        |
Plugin discovery and metadata inspection
        |
Validation and dependency planning
        |
Plugin activation and lifecycle supervision
        |
Per-plugin services and diagnostics
```

A compatibility surface may adapt established mods to these services, but it
must not become the owner of discovery or lifecycle policy.

Prefer small components with explicit inputs and deterministic outputs.
Dependency planning and metadata validation should be testable without starting
the game. Version-sensitive DSP and Unity integration should be isolated behind
narrow adapters.

## 9. Compatibility discipline

The two initial consumer patterns use BepInEx 5 concepts including plugin
metadata, `BaseUnityPlugin`, per-plugin logging, configuration entries, Unity
lifecycle methods, and Harmony. That is migration evidence, not authorization
to reproduce all BepInEx APIs.

When changing a compatibility boundary:

- name the consumer behavior that requires it;
- prefer the smallest contract needed by the retained DSP mods;
- distinguish source compatibility from binary compatibility;
- preserve deterministic error behavior for unsupported features;
- add a focused fixture or migrated consumer check when practical;
- document intentional differences rather than imitating accidental behavior.

Do not claim drop-in compatibility from matching type names or compiling a
sample. Binary identity, startup behavior, lifecycle timing, configuration,
logging, dependency semantics, and in-game behavior all require separate
evidence.

## 10. Reference and licensing discipline

The local clones at `D:\Shy\BepInEx`, `D:\Shy\dsp-beginner-guide`, and
`D:\Shy\DSPMirrorBlueprint` are read-only evidence unless a task explicitly
targets them.

Use BepInEx to understand behavior and scope, not as a source of unreviewed code
to paste into this Apache-licensed repository. Prefer independently written
implementations based on the project's accepted contract and focused behavioral
tests. If third-party code is ever proposed for reuse, stop and identify its
origin, license, required notices, and modification boundary before adding it.

Never modify or redistribute installed DSP, Unity, BepInEx, Harmony, or other
third-party binaries as part of an ordinary implementation task.

## 11. Technical baseline

Until the project contract records a verified change, assume:

- game: Dyson Sphere Program;
- runtime: the game's Unity Mono environment on Windows;
- consumer target: .NET Framework 4.7.2 (`net472`);
- consumer language compatibility: C# 7.3;
- existing mod patching: Harmony, treated as a separate dependency rather than
  functionality to reimplement automatically.

The exact process bootstrap mechanism and the exact migration surface remain
open design decisions. Do not settle either incidentally inside an unrelated
task.

## 12. Implementation discipline

Prefer:

- deterministic metadata and dependency logic;
- explicit lifecycle states and transitions;
- stable plugin identifiers;
- isolated failures with actionable diagnostics;
- testable code that does not require launching DSP where possible;
- narrow adapters for Unity and game integration;
- bounded filesystem discovery;
- documentation that matches observed behavior.

Avoid:

- executing plugin code during metadata discovery when static inspection is
  sufficient;
- nondeterministic filesystem or dependency order;
- swallowing plugin-load failures;
- broad assembly scans outside configured roots;
- hot reload or unloading promises without runtime evidence;
- implicit global mutable state;
- reflection spread across policy, services, and UI code;
- comments that merely restate code.

Treat plugins as trusted in-process code unless the product contract explicitly
introduces another security model. Do not describe the host as a sandbox.

## 13. Validation

Run the narrowest relevant check first:

1. focused unit or contract tests for changed deterministic behavior;
2. build the affected project;
3. fixture-based discovery and dependency checks;
4. broader repository validation when justified;
5. an explicit in-game checkpoint for startup or Unity lifecycle behavior.

Fix failures caused by the change and rerun the failed check. Compilation alone
does not prove plugin discovery, load order, lifecycle timing, compatibility, or
in-game safety.

If a required tool, dependency, or game state is unavailable, report the check
as skipped or blocked. Do not call it passed.

## 14. Tests and documentation

Add or update focused deterministic tests when behavior changes. Priority areas
include metadata parsing, duplicate detection, version rules, dependency graph
ordering, cycles, missing hard dependencies, optional dependencies,
incompatibilities, and failure isolation.

Update `README.md` for user-visible status, setup, or behavior. Update
`docs/PROJECT.md` for changes to purpose, scope, architecture, lifecycle,
compatibility, migration, or acceptance criteria.

Do not commit:

- `bin/` or `obj/`;
- DLLs, PDBs, copied game assemblies, or save data;
- generated plugin caches or configuration containing local paths;
- temporary diagnostics, logs, or editor noise.

## 15. Git discipline

Do not overwrite, revert, reformat, or include unexplained user changes.

Before committing:

1. inspect `git status --short`;
2. inspect the final diff;
3. confirm only intended files changed;
4. run required validation;
5. check for secrets, local paths, binaries, logs, and generated output.

Commit only when requested. Push only when explicitly requested. Do not amend,
rebase, reset, clean, stash, force-push, or rewrite history unless explicitly
instructed.

If Git reports dubious ownership, do not change global configuration. Scope the
exception to the command:

```powershell
$repo = (Resolve-Path '.').Path.Replace('\', '/')
git -c "safe.directory=$repo" status --short
```

## 16. Definition of done

A task is complete when:

- the requested behavior or artifact exists;
- the change stays within scope and preserves product invariants;
- relevant checks pass or skips are reported accurately;
- public and project documentation match the result;
- the final diff contains only intentional changes;
- no known defect introduced by the change remains;
- requested Git operations have succeeded.

Once these conditions are met, stop.

## 17. Final report

Report:

### Completed

A concise description of the result.

### Changed

- files created, modified, or removed;
- significant contract or behavior changes.

### Validation

List each check actually run and its result. Do not claim checks that were not
run.

### Git

- branch;
- commit hash, or `Not committed - explicitly not requested`;
- push result: successful, failed, or not requested.

### Residual issues

List only known limitations, blockers, or relevant follow-up deliberately left
out of scope. If none, say:

`None known within the requested scope.`
