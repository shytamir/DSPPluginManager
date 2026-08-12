# Milestone 2 Installed Exit Evidence

## Result

Milestone 2 passed its installed DSP exit check on 2026-08-12. The check used
the working tree based on source revision
`da8b0e4db7bcdf2ce4077dc36661a4f369c7b652`, release label
`0.1.24.da8b0e4`, and the integrated runner
[`Invoke-Milestone2InstalledCheck.ps1`](../scripts/Invoke-Milestone2InstalledCheck.ps1).

The run temporarily installed the bootstrap bundle into the configured DSP
installation, launched DSP through Steam, waited for the fixture-requested
orderly exit, retained the evidence under the ignored local artifact directory,
and restored the pre-run installation state.

## Observed acceptance outcomes

- The installed discovery report exactly matched the generated 12-entry plan:
  six selected, one redundant, one superseded, two ambiguous, and two rejected.
- Discovery reported zero runtime-loaded candidates. The selected Milestone 1
  fixture's execution sentinel appeared only after activation, and the log
  contained exactly six selected activation outcomes.
- Five independent plugin components reached `Active`; the intentional Harmony
  activation failure reached `Failed` without stopping the other candidates.
- The runtime-delivery fixture recorded `Awake` before rendered `Update`, a
  later-frame coroutine resume, exact coroutine cancellation, a completed scene
  round trip, and stable component and host-root identities.
- Logger and writable-root services remained usable through cleanup. Four
  active plugins reached `Stopped`; the intentional cleanup failure reached
  `StopFailed`; later cleanup continued.
- The manager-provisioned Harmony closure produced patched result `112`, removed
  only the fixture owner while preserving the control owner at result `102`,
  and returned the target to unpatched result `2`.
- All observed callbacks used Unity thread `1`. The current-run log retained
  the attributable failure details, recorded all orderly outcomes, and was
  exclusively reopenable after the final close message.

## Tested artifact identities

| Artifact | SHA-256 |
| --- | --- |
| `DSPPluginManager.dll` | `A16C0C294F442AB92E7219782EC8D2CAEBC64F4057F34B00CD24180234684587` |
| `DSPPluginManager.Contracts.dll` | `3E563EB7EF903B1F0AA9878F6366E8B733141D20400C9F13E2E3D62BA12E6AA4` |
| `DSPPluginManager.UnityHost.dll` | `5FD53C7B419A04E08ADD35DA7A9013DC14FBF4509C267036DE47718444EC61E7` |
| UnityDoorstop `winhttp.dll` | `CF9DD372CA0DDBE01153502C49F8F756197BB260001792FE766F6C0242DC7FC0` |

The retained local evidence hashes were:

| Evidence | SHA-256 |
| --- | --- |
| `EXPECTED-PLAN.txt` | `BC90A077032F3B5C2817116C39437EC7B6FE1A82246DFA550CDFA57F8AEE0409` |
| `DSPPluginManager.log` | `45AD553AEB2AA5B1C56A47D35755E282ADACDE20B41BEF84C9B9853A82988BEA` |
| `RM21-RUNTIME-EVIDENCE.log` | `C12D6844507E015D4EA21DF975BF79E7774A17595E7DDBBA557FE8ED97BB7007` |
| `RM22-FAILURE-EVIDENCE.log` | `CA96FAD6376F3EBDDF3EE19F45A2143A032A7CF5C242D11430A9167C27ABA646` |
| `RM22-SUCCESS-EVIDENCE.log` | `5890FF1FE2904C73B76E0D73AD02684C34E1C4A5300BE1FA6FEEE2DD764DDB0C` |
| `RM23-HARMONY-EVIDENCE.log` | `6415859B1813A8D554F58D7360306D8DBF1C02FAF124DDDB692B7F598B2DD4EC` |

## Restoration result

The runner removed the temporary manager directory, restored the original
Doorstop configuration byte-for-byte, retained the identical pre-existing
proxy, stopped DSP, and verified that the game executable, `Assembly-CSharp`,
and `UnityEngine.CoreModule` hashes were unchanged.

## Boundary

This evidence closes only the Milestone 2 exit defined by the roadmap. It does
not claim configuration, configurable shortcuts, a migrated real consumer,
dependency-graph planning, or a publishable installation package.
