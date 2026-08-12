# Milestone 3 Installed Exit Evidence

## Result

RM-34 passed its installed DSP acceptance gate on 2026-08-12. The two-launch
qualification used product artifacts built from source revision
`fa1fe4d84a1c9179ab9d5e112e20ea85a32af7c4`, release label
`0.1.1.fa1fe4d`, and the foreground-hardened runner at revision
`78b7ef26ad692e8e5093a358c4d18eb78883dbec`.

The run followed the frozen
[RM-34 exit-test design](RM34-EXIT-TEST-DESIGN.md), temporarily installed the
bootstrap bundle in DSP, exercised both RM-32 consumer-shaped plugins across
two process launches, and restored the pre-run installation exactly. Retained
local evidence is under the ignored directory
`artifacts/milestone3-installed-check/20260812T140005590Z`.

## Observed acceptance outcomes

- Both Mirror and Guide activated, polled their shortcut, and cleaned up once
  on Unity thread 1 in each launch. The intentional third plugin failed
  independently once per launch.
- Mirror loaded stored `Enabled = true`, defaulted `Verbose` to false on the
  first launch, changed it to true, and reopened it as true. Its
  `F9 + LeftShift` shortcut persisted and fired once per launch, while
  `LeftShift + A + F9` was rejected.
- Guide loaded stored `Show Panel = false`, began with an unset shortcut,
  retained and late-bound both phase selections, changed Current to
  `next phase`, changed the shortcut to F8, explicitly saved, and reopened all
  values on the second launch.
- The plugins used separate configuration files and writable roots without
  cross-plugin collision. Logger, configuration, writable root, component,
  contract, and Unity access remained available through cleanup.
- Mirror's manager-provisioned Harmony patch produced result 12 during
  activation and cleanup restored the target result to 2 on both launches.
- Neither launch observed a loaded BepInEx assembly. The installed manager log
  recorded both activations, the isolated failure, both cleanups, and orderly
  close per launch.

These observations qualify the lifecycle/service substitutions, configuration
and shortcut drift, build references, Harmony ownership, and BepInEx-removal
boundary documented in [the migration instructions](MIGRATION.md). They do not
claim that either real consumer has migrated.

## Tested artifact identities

| Artifact | SHA-256 |
| --- | --- |
| `DSPPluginManager.dll` | `9E135FABC4983B600BCA16DF493DB887A770D6D1821143048AAB8693543A10B6` |
| `DSPPluginManager.Contracts.dll` | `7312D8B2D55B86EB1CE07A0AD9B7E5A31130183FE8832B250AB3F483261AFC0D` |
| `DSPPluginManager.UnityHost.dll` | `AFAA733408A84AD9303C08C3194CA689D4C473F9A616D857131B268B0AFC0147` |
| Mirror qualification fixture | `7C2D2E3EFDC6C9F01FB2A44814A077B086DD0DE5B71AFF44C1ED8DB124B9EC58` |
| Guide qualification fixture | `C84908A013174B28AE67C43E6E472A461AFD7D6D76F6BE674AD4B2D442595587` |
| UnityDoorstop `winhttp.dll` | `CF9DD372CA0DDBE01153502C49F8F756197BB260001792FE766F6C0242DC7FC0` |

The retained result summary has SHA-256
`A359DE859221E995C5463733F0655A5D9B3887BE64D5DBD7A6EBA8F975178DE4`.
The per-launch consumer evidence hashes are:

| Evidence | SHA-256 |
| --- | --- |
| Run 1 Mirror | `2026CB580920DA1CEBC05DFDC775A3B480F3C204D59C1FEFB1D4309A7331F7E5` |
| Run 1 Guide | `0BD6811C132181D1C37338EA5A959CFCA9072B135B96E2A7AA4B5E1873518F9A` |
| Run 2 Mirror | `DF1907D67B1CD8EFB526A4892BA7A697E5A741A44112FEEA452F75296FFB87E8` |
| Run 2 Guide | `9947A092A137AACFAC3E3FD2E9338C572BC4F4AD36344C4E4ACE75F265F48305` |

## Restoration result

The runner stopped both launched DSP processes, removed the temporary manager
installation, restored the original Doorstop configuration byte-for-byte,
retained the identical pre-existing proxy, and verified that `DSPGAME.exe`,
`Assembly-CSharp.dll`, and `UnityEngine.CoreModule.dll` were unchanged.

## Boundary

RM-34 has met its acceptance gate and awaits project-owner acceptance. This
evidence establishes host-side readiness to begin consumer migration; it does
not establish a migrated consumer, final installation layout, publishable
package, or completion of the product release steering gate.
