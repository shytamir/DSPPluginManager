# RM-06 Managed Bootstrap

## Status

Implementation and acceptance evidence are complete. RM-06 remains awaiting
project-owner acceptance.

This document describes the internal bootstrap bundle exercised by RM-06. It
does not define the final Thunderstore installation layout or a public plugin
contract.

## Pinned native boundary

The build acquires only the official UnityDoorstop `v3.4.0.0` x64 release
artifact from the `NeighTools/UnityDoorstop` GitHub repository.

| Item | Locked value |
| --- | --- |
| Source commit | `81919046fa3cff331916f26bb5aec0c5d6d25adb` |
| Release archive | `Doorstop_x64_3.4.0.0.zip` |
| Archive SHA-256 | `6d391bcafee7d10e5cf26497adadcb5b214725fe8b5ba18746453922936925f5` |
| Native proxy | x64 `winhttp.dll`, PE machine `0x8664` |
| Proxy SHA-256 | `cf9dd372ca0ddbe01153502c49f8f756197bb260001792fe766f6c0242dc7fc0` |
| License | CC0 1.0 Universal |

`Restore-UnityDoorstop.ps1` validates the reviewed source coordinates, archive
length and hash, exact archive member, native file length and hash, PE
architecture, file version, and committed CC0 notice before staging anything.
The build does not accept x86, another release, or an arbitrary 3.4 binary.

## Acceptance bundle

`New-BootstrapBundle.ps1` creates this generated internal layout:

```text
bootstrap-bundle/
|-- winhttp.dll
|-- doorstop_config.ini
`-- DSPPluginManager/
    |-- DSPPluginManager.dll
    |-- DSPPluginManager.UnityHandoff.dll
    |-- dependencies/
    |-- notices/
    `-- plugins/, config/, logs/, writable/ [created on managed entry]
```

The configuration targets
`DSPPluginManager\DSPPluginManager.dll`, leaves output redirection and Mono
runtime overrides off, and leaves Doorstop's environment disable switch
effective.

The managed entrypoint:

1. admits only its first call;
2. requires the installed process to be `DSPGAME.exe` and validates Doorstop's
   executable, managed-directory, and target-assembly paths;
3. materializes the accepted host directories and installs only the reserved
   Harmony/Cecil dependency resolver;
4. loads the separate Cecil component and applies the selected in-memory call
   insertion to `UnityEngine.Application`'s static constructor; and
5. admits only the first resulting Unity handoff.

The entrypoint has no Unity assembly reference and calls no Unity API. The
Cecil component loads the modified Unity image from memory and never writes it
back to the game. The installed runner opts into the small
`bootstrap-checkpoint.txt` by creating a local
`bootstrap-checkpoint.enabled` marker. A normal bundle does not contain that
marker or write the checkpoint. This is acceptance instrumentation, not the
logging service planned by RM-07/RM-08. No plugin discovery or activation
occurs.

## Collision, disable, and removal

- Installation must stop if an existing `winhttp.dll` is not byte-identical to
  the pinned proxy. RM-06 does not choose ownership among competing native
  proxies.
- An existing `doorstop_config.ini` must be preserved before a manager-owned
  configuration replaces it. Automatic installation is intentionally absent.
- Set `enabled=false` in `doorstop_config.ini`, or set
  `DOORSTOP_DISABLE=TRUE`, to disable managed entry without removing files.
- For removal, stop DSP, remove the manager directory and its configuration,
  and remove `winhttp.dll` only when the manager installed that file. Restore
  any displaced pre-existing configuration or proxy exactly.

The installed check implements those ownership rules: the established DSP
installation already contained the exact pinned proxy, so it reused that file
without overwriting or later removing it. The pre-existing BepInEx Doorstop
configuration was restored byte-for-byte after the check.

## Installed DSP evidence

The check ran on 2026-08-11 against the same installed DSP and Unity build
recorded by RM-05. Three normal Steam launches passed:

| Gate | Result |
| --- | --- |
| Manager enabled | Responsive DSP window; one managed entry and one Unity callback |
| Callback context | Thread 1; `UnityEngine.UnitySynchronizationContext`; same thread as entry |
| Doorstop disabled | Responsive DSP window; no manager checkpoint |
| Manager removed | Responsive DSP window; no manager directory or checkpoint |
| Existing proxy | Exact pinned hash reused; not overwritten or removed |
| Existing configuration | Restored to SHA-256 `2255e7640434fdfccbfeb123a5f4fccb05032481b39c2ba822e905ccba58d20e` |

The following protected files had identical SHA-256 values before and after
all three launches:

- `DSPGAME.exe` — `a4b0ab1ec431f1b3c48334784a7a461f3b0dc58693fc5e5577345aa413416065`;
- `Assembly-CSharp.dll` — `ae0ba95f75bd879a62aa4ce253b2ab78eaa4fb3c7c595f5e1fee75ebe0e0ef85`;
- `UnityEngine.CoreModule.dll` — `e2b5ae2fd12646d03fc3d04d1a37d522572a3b97022fe1b95bbf2a2f2b04853a`.

The runner selected and stopped processes only when their executable path was
the installed DSP path. The concurrently running external mod probe was not
selected, stopped, or modified.

## Reproduction

Build and validate the generated bundle:

```text
build.cmd
```

With DSP stopped, run the installed gate explicitly:

```powershell
.\scripts\Invoke-RM06InstalledCheck.ps1 `
  -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program'
```

The installed gate is intentionally not part of hosted CI because it requires
the licensed game, Steam, a visible Unity window, and the recorded local
installation state.
