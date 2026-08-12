# RM-34 Installed Exit-Test Design

## Objective and boundary

This design freezes the Milestone 3 installed qualification before its runner
is executed. It proves that the completed host is ready for downstream source
migration by exercising the accepted Mirror- and Guide-shaped patterns in the
installed DSP Mono runtime. It does not migrate either consumer, select final
package paths, or establish publication readiness.

The qualification uses the built bootstrap bundle and the RM-32 fixture
assemblies. It must not reference or load a BepInEx lifecycle or service
assembly. The manager-owned Harmony closure remains the only Harmony runtime
closure.

## Fixed installed state

- The game root is an explicit runner argument.
- DSP must already be stopped and the temporary `DSPPluginManager` game-root
  directory must not exist; either collision aborts before mutation.
- A byte-identical installed UnityDoorstop proxy may be reused. Another proxy
  aborts the run.
- The runner backs up `doorstop_config.ini` byte-for-byte, hashes
  `DSPGAME.exe`, `Assembly-CSharp.dll`, and `UnityEngine.CoreModule.dll`, then
  temporarily installs the validated bootstrap bundle.
- The installed plugin set is exactly the RM-32 Mirror fixture, RM-32 Guide
  fixture, and the accepted isolated activation-failure fixture.
- Each launch must reach a responsive DSP window and end through the Guide
  fixture's `Application.Quit()` request after both shortcuts were observed.

## Seeded configuration

Launch 1 starts with these manager-owned files:

- Mirror: stored `Enabled = true`, missing `Verbose` (therefore default
  `false`), and `Shortcut = F9 + LeftShift`.
- Guide: stored `Show Panel = false`, unset `Toggle Shortcut`, and unbound
  `Current = current` plus `Legacy = legacy` save values.

During launch 1, Mirror changes `Verbose` to `true`. Guide's early fixed binds
must not erase either save value; it then claims both late, changes Current to
`next phase`, changes the shortcut to F8, and explicitly saves. Launch 2 uses
the files produced by launch 1 without reseeding them.

## Run sequence and input stimuli

For each of two launches, the runner:

1. writes the launch number to each fixture's writable root;
2. launches DSP through Steam and waits for a responsive window;
3. focuses that exact DSP window;
4. on launch 1 only, sends `LeftShift + A + F9` and confirms that the exact
   Mirror shortcut does not fire;
5. sends `LeftShift + F9` and waits for one Mirror polling observation;
6. sends `F8` and waits for one Guide polling observation;
7. waits for the fixture-requested orderly process exit; and
8. copies configuration, plugin evidence, current-run log, and bootstrap
   checkpoint into a launch-specific retained evidence directory.

Key presses use the Windows keyboard-input API against the responsive DSP
window. A key-down edge is held across several Unity frames, then released. No
gameplay or UI-context suitability claim is made; that policy remains consumer
owned as documented in `MIGRATION.md`.

## Required observations

| Area | Frozen observation |
| --- | --- |
| Activation | Mirror and Guide each activate once per launch; the intentional third plugin fails independently once per launch. |
| Thread | Both fixtures activate, poll, and clean up on the one recorded Unity handoff thread. |
| Mirror launch 1 | Stored Enabled true, default Verbose false, stored `F9 + LeftShift`, Harmony patched result, and no response to the extra-key chord. |
| Mirror launch 2 | Enabled true, persisted Verbose true, persisted `F9 + LeftShift`, and supported polling succeeds again. |
| Guide launch 1 | Stored Show Panel false, initially unset shortcut, retained Current/Legacy values claimed late, Current changed, shortcut changed to F8, explicit save. |
| Guide launch 2 | Show Panel false, persisted F8, persisted `next phase`, retained `legacy`, and supported polling succeeds again. |
| Files | Mirror and Guide use separate canonical configuration and writable-root files with no cross-plugin sections or evidence. |
| Harmony | Mirror uses manager-provisioned 0Harmony, its patch works during activation, and cleanup restores the local target baseline. |
| BepInEx boundary | Neither fixture references BepInEx and neither launch observes a loaded BepInEx assembly. |
| Cleanup | Both active fixtures reach one successful `Deactivate()` per launch with Logger, Config, WritableRoot, component, Unity, and contract access still usable. |
| Log | Each launch records two activation acknowledgements, one isolated activation failure, two cleanup acknowledgements, and the final current-run log close. |

## Retained evidence

The ignored timestamped result directory contains:

- the original Doorstop configuration backup when one existed;
- `RUN-1` and `RUN-2` directories holding the manager log, bootstrap
  checkpoint, both consumer evidence files, and both resulting configuration
  files;
- `RUN-RESULT.txt` with process, thread, count, persistence, isolation,
  dependency-boundary, artifact-hash, and restoration results; and
- any new bootstrap emergency record, which fails the qualification.

The committed evidence record identifies the manager revision/release label,
artifact hashes, retained evidence hashes, exact observations, and restoration
outcome.

## Restoration gate

In a `finally` path the runner stops only the DSP process launched from the
configured game executable, restores or removes `doorstop_config.ini` according
to its exact pre-run state, removes only the validated temporary manager
directory, removes only a proxy it created, and verifies all protected hashes.
The run passes only after DSP is stopped, the original Doorstop bytes are back,
the temporary manager directory is absent, and the protected files are
unchanged.
