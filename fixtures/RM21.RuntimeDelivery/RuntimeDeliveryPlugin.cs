using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DSPPluginManager.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DSPPluginManager.RM21RuntimeDelivery
{
    [Plugin(
        "fixture.rm21.runtime-delivery",
        "RM-21 Runtime Delivery",
        "1.0.0"
    )]
    public sealed class RuntimeDeliveryPlugin : PluginBehaviour
    {
        private int sequence;
        private int awakeCount;
        private int updateCount;
        private int awakeSequence;
        private int firstUpdateSequence;
        private int awakeThread;
        private int activateThread;
        private int firstUpdateThread;
        private int resumeThread;
        private int sceneThread;
        private int awakeFrame;
        private int firstUpdateFrame;
        private int resumeStartFrame;
        private int resumeFrame;
        private int awakeInstanceId;
        private int updateInstanceId;
        private int rootBeforeId;
        private int rootDuringId;
        private int rootAfterId;
        private bool resumeHandleUsable;
        private bool cancelHandleUsable;
        private bool handlesDistinct;
        private bool cancelledStarted;
        private bool cancelledResumed;
        private bool resumedAfterNull;
        private bool probeSceneActivated;
        private bool originalSceneRestored;
        private bool sceneTransitionComplete;
        private bool evidenceWritten;

        private void Awake()
        {
            awakeCount++;
            awakeSequence = NextSequence();
            awakeThread = Thread.CurrentThread.ManagedThreadId;
            awakeFrame = Time.frameCount;
            awakeInstanceId = GetInstanceID();
            rootBeforeId = RequiredRoot().GetInstanceID();
        }

        public override void Activate()
        {
            activateThread = Thread.CurrentThread.ManagedThreadId;
            Coroutine cancelled = StartCoroutine(CancelledRoutine());
            cancelHandleUsable = cancelled != null;
            Coroutine resumed = StartCoroutine(ResumeAndTransitionRoutine());
            resumeHandleUsable = resumed != null;
            handlesDistinct = cancelled != resumed;
            StopCoroutine(cancelled);
        }

        public override void Deactivate()
        {
        }

        private void Update()
        {
            updateCount++;
            if (updateCount == 1)
            {
                firstUpdateSequence = NextSequence();
                firstUpdateThread = Thread.CurrentThread.ManagedThreadId;
                firstUpdateFrame = Time.frameCount;
                updateInstanceId = GetInstanceID();
            }

            if (!evidenceWritten && updateCount >= 4 &&
                sceneTransitionComplete)
            {
                WriteEvidence();
            }
        }

        private IEnumerator CancelledRoutine()
        {
            cancelledStarted = true;
            yield return null;
            cancelledResumed = true;
        }

        private IEnumerator ResumeAndTransitionRoutine()
        {
            resumeStartFrame = Time.frameCount;
            yield return null;
            resumeFrame = Time.frameCount;
            resumeThread = Thread.CurrentThread.ManagedThreadId;
            resumedAfterNull = resumeFrame > resumeStartFrame;

            Scene original = SceneManager.GetActiveScene();
            Scene probe = SceneManager.CreateScene(
                "DSPPluginManager.RM21.RuntimeProbe"
            );
            sceneThread = Thread.CurrentThread.ManagedThreadId;
            probeSceneActivated = SceneManager.SetActiveScene(probe);
            rootDuringId = RequiredRoot().GetInstanceID();
            originalSceneRestored = original.IsValid() &&
                SceneManager.SetActiveScene(original);
            rootAfterId = RequiredRoot().GetInstanceID();
            SceneManager.UnloadSceneAsync(probe);
            sceneTransitionComplete = probeSceneActivated &&
                originalSceneRestored;
        }

        private void WriteEvidence()
        {
            evidenceWritten = true;
            List<string> lines = new List<string>
            {
                "event=rm21-runtime-delivery",
                "awakeCount=" + awakeCount,
                "updateCount=" + updateCount,
                "awakeSequence=" + awakeSequence,
                "firstUpdateSequence=" + firstUpdateSequence,
                "awakeThread=" + awakeThread,
                "activateThread=" + activateThread,
                "firstUpdateThread=" + firstUpdateThread,
                "resumeThread=" + resumeThread,
                "sceneThread=" + sceneThread,
                "awakeFrame=" + awakeFrame,
                "firstUpdateFrame=" + firstUpdateFrame,
                "resumeStartFrame=" + resumeStartFrame,
                "resumeFrame=" + resumeFrame,
                "awakeInstanceId=" + awakeInstanceId,
                "updateInstanceId=" + updateInstanceId,
                "rootBeforeId=" + rootBeforeId,
                "rootDuringId=" + rootDuringId,
                "rootAfterId=" + rootAfterId,
                "resumeHandleUsable=" + resumeHandleUsable,
                "cancelHandleUsable=" + cancelHandleUsable,
                "handlesDistinct=" + handlesDistinct,
                "cancelledStarted=" + cancelledStarted,
                "cancelledResumed=" + cancelledResumed,
                "resumedAfterNull=" + resumedAfterNull,
                "probeSceneActivated=" + probeSceneActivated,
                "originalSceneRestored=" + originalSceneRestored,
                "sceneTransitionComplete=" + sceneTransitionComplete,
                "event=probe-complete"
            };
            string path = Path.Combine(
                WritableRoot,
                "RM21-RUNTIME-EVIDENCE.log"
            );
            File.WriteAllLines(path, lines.ToArray());
            Logger.Information(
                "RM-21 ordinary Unity runtime delivery evidence completed."
            );
        }

        private int NextSequence()
        {
            sequence++;
            return sequence;
        }

        private static GameObject RequiredRoot()
        {
            GameObject root = GameObject.Find("DSPPluginManager");
            if (root == null)
            {
                throw new InvalidOperationException(
                    "The persistent manager root was not found."
                );
            }
            return root;
        }
    }
}
