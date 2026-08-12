# AGENTS.md

This file defines working and safety rules for coding agents in this
repository. Product requirements, architecture, compatibility scope, and open
decisions belong in [`docs/PROJECT.md`](docs/PROJECT.md) and other project
documentation, not here.

## 1. Authority and scope

Follow, in order:

1. the current user request;
2. this file's working and safety rules;
3. the directly relevant project documentation;
4. existing implementation and test conventions;
5. general engineering judgment.

Complete the requested task with the smallest coherent change that satisfies
its acceptance criteria. Inspect and modify only named files and files directly
required by the change. Do not fix unrelated defects, reorganize nearby code,
upgrade unrelated dependencies, or add speculative abstractions.

Treat requests to inspect, review, explain, or plan as non-mutating unless the
user explicitly requests changes. Commit only when requested. Push only when
explicitly requested.

## 2. Before editing

1. Run `git status --short` and preserve existing user changes.
2. Read the directly relevant documentation, implementation, and tests.
3. Identify the smallest viable change and the narrowest useful validation.
4. Resolve minor ambiguity from repository evidence. Ask only when a missing
   decision would materially change the contract, migration path, or safety of
   the result.

Do not present proposed, planned, implemented, or validated behavior as another
state. Update the authoritative project documentation when a requested change
alters its contract or user-visible status.

## 3. Editing discipline

- Preserve unrelated tracked and untracked work.
- Follow existing formatting and test conventions.
- Avoid repository-wide formatting or cleanup unless requested.
- Prefer focused, independently reviewable changes.
- Do not add generated output, temporary diagnostics, logs, local paths,
  credentials, or editor noise.
- Do not commit `bin/`, `obj/`, DLLs, PDBs, copied third-party assemblies, or
  other build artifacts unless the task explicitly establishes a reviewed need.

Reference repositories and installed third-party files are read-only unless a
task explicitly targets them. Do not copy or redistribute third-party code or
binaries without first identifying provenance, license terms, required notices,
and the permitted modification/distribution boundary.

## 4. Validation

Run the narrowest relevant check first, then broaden only when justified:

1. focused tests or document validation;
2. the affected build;
3. directly related integration or fixture checks;
4. broader repository checks;
5. an external or in-application checkpoint when the changed behavior requires
   one.

Fix failures caused by the change and rerun the failed check. Report unavailable
tools, dependencies, or external state as skipped or blocked; do not call them
passed. Do not claim that compilation proves runtime behavior.

## 5. Git safety

Do not overwrite, revert, stage, or commit unexplained user changes. Before a
commit:

1. inspect `git status --short`;
2. inspect the final diff and staged diff;
3. confirm only intended files are staged;
4. run the relevant validation;
5. check for secrets, local paths, binaries, logs, and generated output.

Do not amend, rebase, reset, clean, stash, force-push, or rewrite history unless
explicitly instructed. Use non-interactive Git operations and avoid global Git
configuration changes.

An explicit request to push `main` authorizes pushing the requested commits to
this repository's existing configured `origin/main`. Resolve and verify the
current branch and `origin` URL internally before pushing; do not require the
user to restate a literal remote URL or repository path. Include the resolved
destination in any tool approval rationale when needed. This authorization does
not extend to another remote, another branch, a changed or missing `origin`, a
force-push, or any history rewrite.

If Git reports dubious ownership, scope the exception to the command:

```powershell
$repo = (Resolve-Path '.').Path.Replace('\', '/')
git -c "safe.directory=$repo" status --short
```

If Git cannot create `.git/index.lock` under the sandbox identity, rerun only
the required Git operation under the authenticated desktop context. Do not
change repository permissions.

## 6. Definition of done

A task is complete when:

- the requested result exists;
- the change stays within scope;
- relevant checks pass or skips are reported accurately;
- documentation matches the result;
- the final diff contains only intentional changes;
- requested Git operations have succeeded.

Once these conditions are met, stop.

## 7. Final report

Report:

- **Completed:** concise result;
- **Changed:** files and significant behavior or contract changes;
- **Validation:** checks actually run and their results;
- **Git:** branch, commit hash or not committed, and push result;
- **Residual issues:** only known limitations or relevant follow-up, or state
  that none are known within the requested scope.
