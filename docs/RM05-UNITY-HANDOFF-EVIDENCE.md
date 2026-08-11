# RM-05 Unity Main-Thread Handoff Evidence

## Outcome

The narrow in-memory Cecil handoff is selected for production implementation.
The `RuntimeInitializeOnLoadMethod` alternative is rejected for the recorded
DSP build because Unity did not invoke the attributed method in a
Doorstop-preloaded external assembly during a normal responsive launch.

This decision selects a mechanism only. The disposable probe is not a product
entrypoint, lifecycle host, or public plugin contract, and the production
bootstrap remains unimplemented.

## Tested baseline

The probes ran on Windows through the installed Steam launch path and the
existing UnityDoorstop 3.4.0 native boundary.

| Input | Recorded value |
| --- | --- |
| Unity engine | `2022.3.62f3c1 (1623fc0bbb97)` |
| `DSPGAME.exe` SHA-256 | `A4B0AB1EC431F1B3C48334784A7A461F3B0DC58693FC5E5577345AA413416065` |
| `Assembly-CSharp.dll` SHA-256 | `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85` |
| `UnityEngine.CoreModule.dll` SHA-256 | `E2B5AE2FD12646D03FC3D04D1A37D522572A3B97022FE1B95BBF2A2F2B04853A` |
| UnityDoorstop | `3.4.0.0` |
| `winhttp.dll` SHA-256 | `CF9DD372CA0DDBE01153502C49F8F756197BB260001792FE766F6C0242DC7FC0` |
| Original `doorstop_config.ini` SHA-256 | `2255E7640434FDFCCBFEB123A5F4FCCB05032481B39C2BA822E905CCBA58D20E` |

The probe used the repository-pinned .NET SDK, compiled for `net472` with zero
warnings, and used the RM-04 validated Mono.Cecil 0.10.4 assembly. It copied no
game or Unity assembly into the repository or probe output.

## Probe boundary

The disposable probe has three separately compiled parts:

1. a minimal Doorstop entry assembly with no Unity reference;
2. a preloaded external callback assembly containing the Unity attribute and a
   temporary frame observer;
3. a Cecil-only installer which inserts one call at the beginning of
   `UnityEngine.Application`'s static constructor, writes the changed assembly
   to memory, and loads that image without changing the installed file.

The entry assembly used the accepted RM-03 emergency writer and RM-04 reserved
dependency resolver. None of the probe assemblies enter the normal product
build, package, or public contract.

## Results

### External `RuntimeInitializeOnLoadMethod`

- Doorstop invoked the external entry assembly on managed thread 1.
- The callback assembly was loaded before Unity initialization.
- DSP opened through Steam and remained visible and responsive for the full
  90-second observation window.
- Callback count remained zero; no observer `Awake`, first-frame, or steady-frame
  record occurred.
- No emergency diagnostic was produced.

The candidate therefore fails the required callback checkpoint even though
normal game startup succeeds.

### Narrow in-memory Cecil handoff

- Doorstop invoked the entry assembly on managed thread 1.
- Mono.Cecil inserted one call into `UnityEngine.Application..cctor` and loaded
  the resulting 1,384,960-byte image from memory.
- The installed `UnityEngine.CoreModule.dll` was not written and retained its
  original SHA-256.
- The Unity callback occurred exactly once, 3,876 ms after the probe recorder
  started, on managed thread 1.
- The callback reported `UnityEngine.UnitySynchronizationContext`,
  `Application.isPlaying=True`, Unity `2022.3.62f3c1`, and scene `DSPGame`.
- The temporary observer ran on the same thread at frame 1 and frame 120. The
  frame-120 record occurred 16,617 ms after probe start while the DSP window was
  visible and responsive.

The candidate passes callback count, Unity-main-thread, timing, and normal
startup checkpoints.

### Disabled startup

- UnityDoorstop was configured with `enabled=false` while the probe target
  remained present.
- DSP opened through Steam with a visible responsive window.
- No probe evidence file, callback, or emergency diagnostic was created.

### Forced early failure

- The Doorstop entrypoint deliberately threw a two-line exception before Unity
  handoff.
- RM-03 synchronously created one game-root emergency record before the process
  could show a window.
- The record contained the bootstrap phase, target assembly, executable,
  managed directory, probe root, dependency directory, complete exception,
  both message lines, and stack trace.
- Diagnostic creation was itself recorded as successful.

## Installation integrity and cleanup

After each run, the original configuration and Unity assembly were verified.
After the probe sequence and bounded delayed-process cleanup:

- the original `doorstop_config.ini` bytes and SHA-256 were restored;
- `UnityEngine.CoreModule.dll` retained its original SHA-256;
- the dedicated probe directory was removed;
- probe-created game-root emergency files were copied into ignored test
  artifacts and removed from the installation;
- no DSP process remained running.

A preliminary direct-executable launch was excluded from acceptance evidence:
Unity initialized, but DSP correctly exited when Steam initialization failed.
All reported acceptance runs used Steam app launch instead.

## Production constraint

RM-06 must implement only the selected narrow Cecil handoff. It must validate
the recorded target assembly, type, static constructor, and callback insertion
point before loading the in-memory image, invoke the project host at most once,
leave installed game and Unity assemblies unchanged, and route any failure
through the RM-03 emergency record. It must not bring forward the probe frame
observer, attribute callback, generalized patching, or configurable target
selection.
