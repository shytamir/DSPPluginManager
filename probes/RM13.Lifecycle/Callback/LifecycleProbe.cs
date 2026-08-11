using System;
using System.Threading;
using DSPPluginManager.RM13Probe;
using UnityEngine;

namespace DSPPluginManager.RM13Callback
{
    public static class LifecycleProbe
    {
        public static void Handoff()
        {
            SynchronizationContext synchronization =
                SynchronizationContext.Current;
            int count = ProbeRecorder.RecordCallback(
                "unityVersion=" + Application.unityVersion,
                "isPlaying=" + Application.isPlaying,
                "synchronizationContext=" +
                (synchronization == null
                    ? "<null>"
                    : synchronization.GetType().FullName)
            );
            if (count != 1)
            {
                ProbeRecorder.Record(
                    "callback-count-violation",
                    "count=" + count
                );
                return;
            }

            GameObject root = new GameObject(
                "DSPPluginManager_RM13_LifecycleProbe"
            );
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.AddComponent<ProbeSupervisor>();
        }
    }

    public sealed class ProbeSupervisor : MonoBehaviour
    {
        private GameObject directStopSuccessObject;
        private GameObject directStopFailureObject;
        private ExplicitLifecycleFixture explicitSuccess;
        private ExplicitLifecycleFixture explicitStopFailure;
        private int updateCount;
        private bool shutdownStarted;
        private bool completed;

        private void Awake()
        {
            Application.logMessageReceived += RecordUnityLog;
            ProbeRecorder.Record("supervisor-awake");
        }

        private void Start()
        {
            ProbeRecorder.Record("supervisor-start");
            RunDirectStartupCases();
            RunExplicitStartupCases();
            CreateShutdownCases();
        }

        private void Update()
        {
            updateCount++;
            if (!shutdownStarted && updateCount >= 2)
            {
                shutdownStarted = true;
                RunShutdownCases();
                return;
            }
            if (!completed && shutdownStarted && updateCount >= 5)
            {
                completed = true;
                RecordSummary();
                ProbeRecorder.Record("probe-complete");
            }
        }

        private static void RunDirectStartupCases()
        {
            GameObject success = NewChild("DirectAwakeSuccess");
            try
            {
                DirectAwakeSuccess component =
                    success.AddComponent<DirectAwakeSuccess>();
                ProbeRecorder.Record(
                    "direct-awake-success-add-return",
                    "componentNull=" + (component == null)
                );
            }
            catch (Exception exception)
            {
                ProbeRecorder.Record(
                    "direct-awake-success-add-catch",
                    exception.ToString()
                );
            }

            GameObject failure = NewChild("DirectAwakeFailure");
            try
            {
                DirectAwakeFailure component =
                    failure.AddComponent<DirectAwakeFailure>();
                ProbeRecorder.Record(
                    "direct-awake-failure-add-return",
                    "componentNull=" + (component == null)
                );
            }
            catch (Exception exception)
            {
                ProbeRecorder.Record(
                    "direct-awake-failure-add-catch",
                    exception.ToString()
                );
            }
        }

        private static void RunExplicitStartupCases()
        {
            ExplicitLifecycleFixture success = CreateExplicit(
                "ExplicitStartSuccess",
                ExplicitLifecycleMode.Success
            );
            InvokeExplicitStart(success, "explicit-start-success");

            ExplicitLifecycleFixture failure = CreateExplicit(
                "ExplicitStartFailure",
                ExplicitLifecycleMode.StartFailure
            );
            InvokeExplicitStart(failure, "explicit-start-failure");
        }

        private void CreateShutdownCases()
        {
            directStopSuccessObject = NewChild("DirectStopSuccess");
            directStopSuccessObject.AddComponent<DirectStopSuccess>();
            directStopFailureObject = NewChild("DirectStopFailure");
            directStopFailureObject.AddComponent<DirectStopFailure>();

            explicitSuccess = CreateExplicit(
                "ExplicitStopSuccess",
                ExplicitLifecycleMode.Success
            );
            InvokeExplicitStart(explicitSuccess, "explicit-stop-success-start");
            explicitStopFailure = CreateExplicit(
                "ExplicitStopFailure",
                ExplicitLifecycleMode.StopFailure
            );
            InvokeExplicitStart(
                explicitStopFailure,
                "explicit-stop-failure-start"
            );
        }

        private void RunShutdownCases()
        {
            InvokeExplicitStop(explicitSuccess, "explicit-stop-success");
            InvokeExplicitStop(explicitStopFailure, "explicit-stop-failure");

            ProbeRecorder.Record("direct-destroy-request-start");
            UnityEngine.Object.Destroy(directStopSuccessObject);
            UnityEngine.Object.Destroy(directStopFailureObject);
            ProbeRecorder.Record("direct-destroy-request-return");
        }

        private static void InvokeExplicitStart(
            ExplicitLifecycleFixture fixture,
            string caseName
        )
        {
            try
            {
                fixture.ActivateForHost();
                ProbeRecorder.Record(caseName + "-return");
            }
            catch (Exception exception)
            {
                ProbeRecorder.Record(
                    caseName + "-catch",
                    exception.ToString()
                );
            }
        }

        private static void InvokeExplicitStop(
            ExplicitLifecycleFixture fixture,
            string caseName
        )
        {
            try
            {
                fixture.DeactivateForHost();
                ProbeRecorder.Record(caseName + "-return");
            }
            catch (Exception exception)
            {
                ProbeRecorder.Record(
                    caseName + "-catch",
                    exception.ToString()
                );
            }
        }

        private static ExplicitLifecycleFixture CreateExplicit(
            string name,
            ExplicitLifecycleMode mode
        )
        {
            ExplicitLifecycleFixture fixture =
                NewChild(name).AddComponent<ExplicitLifecycleFixture>();
            fixture.Mode = mode;
            return fixture;
        }

        private static GameObject NewChild(string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(
                GameObject.Find("DSPPluginManager_RM13_LifecycleProbe")
                    .transform,
                false
            );
            return child;
        }

        private static void RecordUnityLog(
            string condition,
            string stackTrace,
            LogType type
        )
        {
            if (type != LogType.Exception && type != LogType.Error)
            {
                return;
            }
            if (condition == null || !condition.Contains("RM-13"))
            {
                return;
            }
            ProbeRecorder.Record(
                "unity-log-exception",
                "type=" + type,
                "condition=" + condition,
                "stackTrace=" + stackTrace
            );
        }

        private static void RecordSummary()
        {
            ProbeRecorder.Record(
                "lifecycle-summary",
                "directAwakeSuccessEnter=" +
                    LifecycleCounters.DirectAwakeSuccessEnter,
                "directAwakeSuccessComplete=" +
                    LifecycleCounters.DirectAwakeSuccessComplete,
                "directAwakeFailureEnter=" +
                    LifecycleCounters.DirectAwakeFailureEnter,
                "directAwakeFailureComplete=" +
                    LifecycleCounters.DirectAwakeFailureComplete,
                "directDestroySuccessEnter=" +
                    LifecycleCounters.DirectDestroySuccessEnter,
                "directDestroySuccessComplete=" +
                    LifecycleCounters.DirectDestroySuccessComplete,
                "directDestroyFailureEnter=" +
                    LifecycleCounters.DirectDestroyFailureEnter,
                "directDestroyFailureComplete=" +
                    LifecycleCounters.DirectDestroyFailureComplete,
                "explicitStartSuccess=" +
                    LifecycleCounters.ExplicitStartSuccess,
                "explicitStartFailure=" +
                    LifecycleCounters.ExplicitStartFailure,
                "explicitStopSuccess=" +
                    LifecycleCounters.ExplicitStopSuccess,
                "explicitStopFailure=" +
                    LifecycleCounters.ExplicitStopFailure
            );
        }
    }

    public sealed class DirectAwakeSuccess : MonoBehaviour
    {
        private void Awake()
        {
            LifecycleCounters.DirectAwakeSuccessEnter++;
            ProbeRecorder.Record("direct-awake-success-enter");
            LifecycleCounters.DirectAwakeSuccessComplete++;
            ProbeRecorder.Record("direct-awake-success-complete");
        }
    }

    public sealed class DirectAwakeFailure : MonoBehaviour
    {
        private void Awake()
        {
            LifecycleCounters.DirectAwakeFailureEnter++;
            ProbeRecorder.Record("direct-awake-failure-enter");
            throw new InvalidOperationException(
                "RM-13 direct Awake failure.\r\n" +
                "The second line proves complete diagnostics."
            );
        }
    }

    public sealed class DirectStopSuccess : MonoBehaviour
    {
        private void Awake()
        {
            ProbeRecorder.Record("direct-stop-success-awake");
        }

        private void OnDestroy()
        {
            LifecycleCounters.DirectDestroySuccessEnter++;
            ProbeRecorder.Record("direct-destroy-success-enter");
            LifecycleCounters.DirectDestroySuccessComplete++;
            ProbeRecorder.Record("direct-destroy-success-complete");
        }
    }

    public sealed class DirectStopFailure : MonoBehaviour
    {
        private void Awake()
        {
            ProbeRecorder.Record("direct-stop-failure-awake");
        }

        private void OnDestroy()
        {
            LifecycleCounters.DirectDestroyFailureEnter++;
            ProbeRecorder.Record("direct-destroy-failure-enter");
            throw new InvalidOperationException(
                "RM-13 direct OnDestroy failure.\r\n" +
                "The second line proves complete diagnostics."
            );
        }
    }

    public enum ExplicitLifecycleMode
    {
        Success,
        StartFailure,
        StopFailure
    }

    public sealed class ExplicitLifecycleFixture : MonoBehaviour
    {
        public ExplicitLifecycleMode Mode { get; set; }

        public void ActivateForHost()
        {
            ProbeRecorder.Record("explicit-start-enter", "mode=" + Mode);
            if (Mode == ExplicitLifecycleMode.StartFailure)
            {
                LifecycleCounters.ExplicitStartFailure++;
                throw new InvalidOperationException(
                    "RM-13 explicit activation failure.\r\n" +
                    "The second line proves complete diagnostics."
                );
            }
            LifecycleCounters.ExplicitStartSuccess++;
            ProbeRecorder.Record("explicit-start-complete", "mode=" + Mode);
        }

        public void DeactivateForHost()
        {
            ProbeRecorder.Record("explicit-stop-enter", "mode=" + Mode);
            if (Mode == ExplicitLifecycleMode.StopFailure)
            {
                LifecycleCounters.ExplicitStopFailure++;
                throw new InvalidOperationException(
                    "RM-13 explicit cleanup failure.\r\n" +
                    "The second line proves complete diagnostics."
                );
            }
            LifecycleCounters.ExplicitStopSuccess++;
            ProbeRecorder.Record("explicit-stop-complete", "mode=" + Mode);
        }
    }

    internal static class LifecycleCounters
    {
        internal static int DirectAwakeSuccessEnter;
        internal static int DirectAwakeSuccessComplete;
        internal static int DirectAwakeFailureEnter;
        internal static int DirectAwakeFailureComplete = 0;
        internal static int DirectDestroySuccessEnter;
        internal static int DirectDestroySuccessComplete;
        internal static int DirectDestroyFailureEnter;
        internal static int DirectDestroyFailureComplete = 0;
        internal static int ExplicitStartSuccess;
        internal static int ExplicitStartFailure;
        internal static int ExplicitStopSuccess;
        internal static int ExplicitStopFailure;
    }
}
