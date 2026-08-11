# Milestone 1 Exit Evidence

## Outcome

Milestone 1, Deterministic pre-activation host, completed its installed exit
check on 2026-08-11 UTC. RM-01 through RM-12 were accepted by the project
owner.

The check exercised the working tree based on revision `02a2201` with local
validation version `0.1.1.0`. The tested `DSPPluginManager.dll` SHA-256 was
`b78c80e7eb640cd9531dc1bc88bb0c3c305fe313b267e9901d7dd871b6dd4782`.

## Installed DSP result

The reversible check temporarily replaced the existing Doorstop configuration,
targeted `DSPPluginManager\DSPPluginManager.dll`, launched DSP through Steam,
and observed a responsive game window.

| Gate | Result |
| --- | --- |
| Managed host entry | Exactly one |
| Unity main-thread handoff | Exactly one, on thread 1 with `UnityEngine.UnitySynchronizationContext` |
| Current-run log | Opened and read while DSP was running |
| Enumerated fixture candidates | Seven |
| Runtime-loaded candidate assemblies | Zero |
| Candidate execution sentinel | Not triggered |
| Offline and installed plans | Exact ordered match |

The deterministic plan contained:

- two ambiguous entries for one conflicting identity and version;
- one selected highest-version candidate;
- one byte-identical redundant placement;
- one superseded lower version;
- one invalid-metadata rejection; and
- one non-managed-file rejection.

The installed log used plugin-root-relative paths and stable state and
diagnostic codes. It excluded timestamps, machine-specific absolute paths, and
free-form diagnostic details from the compared plan payload.

## Installation recovery

The installed check reused the already present byte-identical UnityDoorstop
proxy with SHA-256
`cf9dd372ca0ddbe01153502c49f8f756197bb260001792fe766f6c0242dc7fc0`.
It did not overwrite or remove that proxy.

After the run:

- the temporary `DSPPluginManager` installation was removed;
- the pre-existing BepInEx Doorstop configuration was restored exactly;
- `DSPGAME.exe`, `Assembly-CSharp.dll`, and
  `UnityEngine.CoreModule.dll` retained their pre-run hashes; and
- the validated DSP process was stopped.

## Scope of the result

This evidence establishes the Milestone 1 pre-activation boundary only. It
does not claim plugin activation, plugin dependency planning, lifecycle
services, consumer migration, or a publishable package.

## Reproduction

Build and validate the bootstrap bundle, stop DSP, and run:

```powershell
.\scripts\Invoke-Milestone1InstalledCheck.ps1 `
  -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program'
```

The script generates its fixture assemblies, expected plan, current-run log,
checkpoint, and result report beneath
`artifacts\milestone1-installed-check`. Generated binaries and local evidence
remain ignored build artifacts.
