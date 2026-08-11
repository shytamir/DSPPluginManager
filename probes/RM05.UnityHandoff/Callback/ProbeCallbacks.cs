using System;
using System.Threading;
using DSPPluginManager.RM05Probe;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DSPPluginManager.RM05Callback
{
    public static class ProbeCallbacks
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeAttributeHandoff()
        {
            if (string.Equals(
                    ProbeRecorder.Mode,
                    "runtime-attribute",
                    StringComparison.Ordinal
                ))
            {
                Activate("runtime-attribute");
            }
        }

        public static void CecilHandoff()
        {
            if (string.Equals(
                    ProbeRecorder.Mode,
                    "cecil",
                    StringComparison.Ordinal
                ))
            {
                Activate("cecil");
            }
        }

        private static void Activate(string candidate)
        {
            SynchronizationContext synchronization =
                SynchronizationContext.Current;
            int callbackCount = ProbeRecorder.RecordCallback(
                candidate,
                "unityVersion=" + Application.unityVersion,
                "isPlaying=" + Application.isPlaying,
                "realtimeSinceStartup=" + Time.realtimeSinceStartup,
                "scene=" + SceneManager.GetActiveScene().name,
                "synchronizationContext=" +
                (synchronization == null
                    ? "<null>"
                    : synchronization.GetType().FullName)
            );
            if (callbackCount != 1)
            {
                ProbeRecorder.Record(
                    "callback-count-violation",
                    "count=" + callbackCount
                );
                return;
            }

            GameObject observerObject = new GameObject(
                "DSPPluginManager_RM05_Probe"
            );
            UnityEngine.Object.DontDestroyOnLoad(observerObject);
            ProbeObserver observer = observerObject.AddComponent<ProbeObserver>();
            observer.Candidate = candidate;
        }
    }

    public sealed class ProbeObserver : MonoBehaviour
    {
        private int updateCount;

        public string Candidate { get; set; }

        private void Awake()
        {
            ProbeRecorder.Record(
                "observer-awake",
                "thread=" + Thread.CurrentThread.ManagedThreadId,
                "sameAsPreloadThread=" +
                (Thread.CurrentThread.ManagedThreadId ==
                    ProbeRecorder.PreloadThreadId)
            );
        }

        private void Update()
        {
            updateCount++;
            if (updateCount == 1 || updateCount == 120)
            {
                ProbeRecorder.Record(
                    updateCount == 1 ? "first-update" : "steady-update",
                    "candidate=" + Candidate,
                    "frame=" + Time.frameCount,
                    "scene=" + SceneManager.GetActiveScene().name,
                    "thread=" + Thread.CurrentThread.ManagedThreadId,
                    "sameAsPreloadThread=" +
                    (Thread.CurrentThread.ManagedThreadId ==
                        ProbeRecorder.PreloadThreadId)
                );
            }
        }
    }
}
