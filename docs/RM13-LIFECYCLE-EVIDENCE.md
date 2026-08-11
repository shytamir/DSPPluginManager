# RM-13 Unity Lifecycle Evidence

## Outcome

The explicit source-adapted lifecycle seam is selected. A real plugin component
remains a `UnityEngine.MonoBehaviour`, but the host acknowledges startup and
orderly cleanup only from explicit synchronous callbacks that it invokes inside
its own exception boundary.

The selected object arrangement is one persistent host root with one owned
child object per selected plugin. This retains one durable Unity container
while allowing a failed or stopped plugin component to be removed without
destroying an unrelated plugin.

The disposable probe and its Unity-log observer are evidence only. They are not
production activation code or a general Unity-event interceptor.

## Tested baseline

The probe ran through the installed Steam launch path, using the accepted
UnityDoorstop 3.4.0 native boundary and RM-05 in-memory Cecil handoff.

| Input | Recorded value |
| --- | --- |
| Unity | `2022.3.62f3c1` |
| Unity main-thread managed ID | `1` |
| `UnityEngine.CoreModule.dll` SHA-256 | `E2B5AE2FD12646D03FC3D04D1A37D522572A3B97022FE1B95BBF2A2F2B04853A` |
| Product assembly SHA-256 | `E2C7E94B69AA8CE450CF502C38DE5E0371561E5B3E3675C54CE49F1AA67363B6` |
| RM-13 entry assembly SHA-256 | `C88AD4C501163EAF68E3883F665D86939DD508A5602D14A7E04AAC11433B6659` |
| RM-13 callback assembly SHA-256 | `340492C88AD531E5BB67899AF8B970C041E511F8EA4CBFF005E0D7054F3352E9` |

The entry, Unity callback, and lifecycle cases all ran on managed thread 1. DSP
reached a responsive window and the probe completed normally.

The recorder retained UTC timestamps, elapsed milliseconds, thread IDs, event
names, and sanitized complete exception text for every case. Key elapsed times
from recorder initialization were:

| Observation | Elapsed time |
| --- | ---: |
| Unity callback | 4,062 ms |
| Probe supervisor `Start` | 18,885 ms |
| Direct `Awake` cases | 18,886–18,892 ms |
| Explicit activation cases | 18,893–18,894 ms |
| Explicit cleanup calls | 19,751–19,752 ms |
| Ordinary-destruction callbacks | 19,753 ms |
| Final validated summary | 19,783 ms |

## Compared behavior

### Direct private Unity messages

- A successful private `Awake` entered and completed once before
  `AddComponent` returned.
- A throwing private `Awake` entered once and did not complete, but Unity caught
  the exception and `AddComponent` still returned a non-null component. The
  caller therefore could not distinguish successful from failed startup.
- Successful and throwing private `OnDestroy` messages each entered once after
  ordinary destruction was requested.
- Ordinary `Destroy` returned before either `OnDestroy` callback ran. Unity
  caught the throwing callback, so the caller observed neither cleanup
  completion nor failure at the destruction boundary.
- The two complete exceptions were visible to the probe's temporary global
  Unity-log observer. Depending on such an observer would add the general
  event-interception mechanism RM-13 deliberately excludes, and would still
  not make ordinary destruction synchronous.

### Explicit host calls

- Three successful activation calls returned normally; one throwing activation
  call was caught directly by the host boundary with its full two-line
  exception and stack trace.
- One successful cleanup call returned normally; one throwing cleanup call was
  caught directly with its full two-line exception and stack trace.
- Every explicit callback and catch occurred on Unity's main thread. The host
  therefore received an unambiguous synchronous success or failure signal for
  both phases without intercepting Unity's private-message dispatcher.

The validated summary recorded these completion counts:

| Case | Entered or attempted | Completed or returned normally | Failed |
| --- | ---: | ---: | ---: |
| Direct `Awake` success | 1 | 1 | 0 |
| Direct `Awake` throw | 1 | 0 | 1 |
| Direct `OnDestroy` success | 1 | 1 | 0 |
| Direct `OnDestroy` throw | 1 | 0 | 1 |
| Explicit activation | 4 | 3 | 1 |
| Explicit cleanup | 2 | 1 | 1 |

## Selected contract

The minimum plugin callbacks are:

```csharp
public abstract void Activate();
public abstract void Deactivate();
```

They belong to `DSPPluginManager.Contracts.PluginBehaviour`. Because that type
continues to derive from `UnityEngine.MonoBehaviour`, its concrete instance can
use Unity's normal `StartCoroutine`, `StopCoroutine`, and other inherited
component facilities.

The host prepares the plugin's identity-owned services and lifecycle state,
attaches the exact selected component to its owned child object, and then calls
`Activate()` on Unity's main thread. Only normal callback return acknowledges
`Active`; an exception acknowledges failure. `AddComponent` return is not an
activation result.

For the supported orderly path, the host calls `Deactivate()` exactly once on
the main thread while the component and its services remain usable. Normal
return acknowledges successful cleanup and an exception acknowledges failed
cleanup. The host destroys the owned child afterward; private `OnDestroy` is
not an acknowledgement. This contract makes no guarantee for abrupt process
termination or crashes and does not imply managed assembly unloading.

Private `Awake`, `OnDestroy`, and other Unity messages can still occur as
ordinary engine behavior, but migrated plugins must not use them for startup or
cleanup that the host is required to supervise.

## Installation integrity and cleanup

After the validated run:

- the original `doorstop_config.ini` bytes were restored;
- `DSPGAME.exe`, `Assembly-CSharp.dll`, and
  `UnityEngine.CoreModule.dll` retained their pre-run SHA-256 values;
- the dedicated installed probe directory was removed;
- the DSP process was stopped; and
- no game or Unity assembly was copied into tracked repository content.

Production lifecycle implementation remains outside RM-13.
