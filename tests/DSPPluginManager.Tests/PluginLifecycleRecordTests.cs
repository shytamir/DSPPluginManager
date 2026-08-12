using System;
using System.IO;
using DSPPluginManager.Discovery;
using DSPPluginManager.Lifecycle;

namespace DSPPluginManager.Tests
{
    internal static class PluginLifecycleRecordTests
    {
        internal static void Run()
        {
            VerifySelectedEntryRequirement();
            VerifySuccessfulLifecycle();
            VerifyRuntimeAndActivationFailures();
            VerifyStopFailure();
            VerifyInvalidAndDuplicateTransitions();
        }

        private static void VerifySelectedEntryRequirement()
        {
            RecognizedPluginCandidate candidate = Candidate("rm15.redundant");
            CandidateReconciliationDiagnostic diagnostic =
                new CandidateReconciliationDiagnostic(
                    CandidateReconciliationDiagnosticCode.RedundantCopy,
                    candidate.AssemblyPath,
                    "Duplicate placement."
                );
            CandidateReconciliationEntry redundant =
                new CandidateReconciliationEntry(
                    CandidateReconciliationState.Redundant,
                    candidate,
                    null,
                    diagnostic
                );
            TestAssert.Throws<ArgumentException>(
                () => new PluginLifecycleRecord(redundant),
                "selected candidate"
            );
        }

        private static void VerifySuccessfulLifecycle()
        {
            PluginLifecycleRecord record = Record("rm15.success");
            TestAssert.Equal(
                PluginLifecycleState.Selected,
                record.State,
                "initial lifecycle state"
            );
            TestAssert.Equal(null, record.Failure, "initial lifecycle failure");

            AssertAccepted(
                record.TransitionTo(PluginLifecycleState.Activating),
                PluginLifecycleState.Selected,
                PluginLifecycleState.Activating
            );
            AssertAccepted(
                record.TransitionTo(PluginLifecycleState.Active),
                PluginLifecycleState.Activating,
                PluginLifecycleState.Active
            );
            AssertAccepted(
                record.TransitionTo(PluginLifecycleState.Stopping),
                PluginLifecycleState.Active,
                PluginLifecycleState.Stopping
            );
            AssertAccepted(
                record.TransitionTo(PluginLifecycleState.Stopped),
                PluginLifecycleState.Stopping,
                PluginLifecycleState.Stopped
            );
            TestAssert.Equal(
                "rm15.success",
                record.Candidate.Identifier,
                "stopped candidate retention"
            );
            TestAssert.Equal(null, record.Failure, "successful stop failure");
        }

        private static void VerifyRuntimeAndActivationFailures()
        {
            PluginLifecycleRecord runtime = Record("rm15.runtime-failure");
            AssertRejectedWithoutMutation(
                runtime,
                () => runtime.TransitionTo(PluginLifecycleState.Failed),
                PluginLifecycleTransitionDiagnosticCode.FailureContextRequired,
                PluginLifecycleState.Failed
            );
            Exception runtimeException = CompleteException("runtime load");
            AssertAccepted(
                runtime.TransitionToFailure(
                    PluginLifecycleState.Failed,
                    "runtime-load",
                    runtimeException
                ),
                PluginLifecycleState.Selected,
                PluginLifecycleState.Failed
            );
            AssertFailure(
                runtime,
                "rm15.runtime-failure",
                "runtime-load",
                runtimeException
            );
            PluginLifecycleFailure retained = runtime.Failure;
            AssertRejectedWithoutMutation(
                runtime,
                () => runtime.TransitionToFailure(
                    PluginLifecycleState.Failed,
                    "runtime-load",
                    CompleteException("replacement")
                ),
                PluginLifecycleTransitionDiagnosticCode.DuplicateTransition,
                PluginLifecycleState.Failed
            );
            TestAssert.True(
                object.ReferenceEquals(retained, runtime.Failure),
                "A duplicate failure replaced the authoritative failure."
            );

            PluginLifecycleRecord activation = Record(
                "rm15.activation-failure"
            );
            AssertAccepted(
                activation.TransitionTo(PluginLifecycleState.Activating),
                PluginLifecycleState.Selected,
                PluginLifecycleState.Activating
            );
            Exception activationException = CompleteException("activation");
            AssertAccepted(
                activation.TransitionToFailure(
                    PluginLifecycleState.Failed,
                    "activation",
                    activationException
                ),
                PluginLifecycleState.Activating,
                PluginLifecycleState.Failed
            );
            AssertFailure(
                activation,
                "rm15.activation-failure",
                "activation",
                activationException
            );
        }

        private static void VerifyStopFailure()
        {
            PluginLifecycleRecord record = ActiveRecord("rm15.stop-failure");
            AssertAccepted(
                record.TransitionTo(PluginLifecycleState.Stopping),
                PluginLifecycleState.Active,
                PluginLifecycleState.Stopping
            );
            AssertRejectedWithoutMutation(
                record,
                () => record.TransitionTo(PluginLifecycleState.StopFailed),
                PluginLifecycleTransitionDiagnosticCode.FailureContextRequired,
                PluginLifecycleState.StopFailed
            );
            Exception exception = CompleteException("orderly cleanup");
            AssertAccepted(
                record.TransitionToFailure(
                    PluginLifecycleState.StopFailed,
                    "orderly-cleanup",
                    exception
                ),
                PluginLifecycleState.Stopping,
                PluginLifecycleState.StopFailed
            );
            AssertFailure(
                record,
                "rm15.stop-failure",
                "orderly-cleanup",
                exception
            );
            TestAssert.Equal(
                "rm15.stop-failure",
                record.Candidate.Identifier,
                "stop-failed candidate retention"
            );
        }

        private static void VerifyInvalidAndDuplicateTransitions()
        {
            PluginLifecycleRecord selected = Record("rm15.invalid-selected");
            AssertRejectedWithoutMutation(
                selected,
                () => selected.TransitionTo(PluginLifecycleState.Active),
                PluginLifecycleTransitionDiagnosticCode.InvalidTransition,
                PluginLifecycleState.Active
            );
            AssertRejectedWithoutMutation(
                selected,
                () => selected.TransitionToFailure(
                    PluginLifecycleState.StopFailed,
                    "orderly-cleanup",
                    CompleteException("out of order")
                ),
                PluginLifecycleTransitionDiagnosticCode.InvalidTransition,
                PluginLifecycleState.StopFailed
            );
            AssertRejectedWithoutMutation(
                selected,
                () => selected.TransitionToFailure(
                    PluginLifecycleState.Active,
                    "activation",
                    CompleteException("wrong target")
                ),
                PluginLifecycleTransitionDiagnosticCode.FailureContextNotAllowed,
                PluginLifecycleState.Active
            );
            AssertRejectedWithoutMutation(
                selected,
                () => selected.TransitionToFailure(
                    PluginLifecycleState.Failed,
                    " ",
                    CompleteException("missing phase")
                ),
                PluginLifecycleTransitionDiagnosticCode.FailureContextRequired,
                PluginLifecycleState.Failed
            );

            AssertAccepted(
                selected.TransitionTo(PluginLifecycleState.Activating),
                PluginLifecycleState.Selected,
                PluginLifecycleState.Activating
            );
            AssertRejectedWithoutMutation(
                selected,
                () => selected.TransitionTo(PluginLifecycleState.Activating),
                PluginLifecycleTransitionDiagnosticCode.DuplicateTransition,
                PluginLifecycleState.Activating
            );

            PluginLifecycleRecord active = ActiveRecord("rm15.invalid-active");
            AssertRejectedWithoutMutation(
                active,
                () => active.TransitionToFailure(
                    PluginLifecycleState.Failed,
                    "runtime",
                    CompleteException("late failure")
                ),
                PluginLifecycleTransitionDiagnosticCode.InvalidTransition,
                PluginLifecycleState.Failed
            );

            PluginLifecycleRecord stopped = ActiveRecord("rm15.terminal");
            AssertAccepted(
                stopped.TransitionTo(PluginLifecycleState.Stopping),
                PluginLifecycleState.Active,
                PluginLifecycleState.Stopping
            );
            AssertAccepted(
                stopped.TransitionTo(PluginLifecycleState.Stopped),
                PluginLifecycleState.Stopping,
                PluginLifecycleState.Stopped
            );
            AssertRejectedWithoutMutation(
                stopped,
                () => stopped.TransitionTo(PluginLifecycleState.Activating),
                PluginLifecycleTransitionDiagnosticCode.InvalidTransition,
                PluginLifecycleState.Activating
            );
        }

        private static PluginLifecycleRecord ActiveRecord(string identifier)
        {
            PluginLifecycleRecord record = Record(identifier);
            AssertAccepted(
                record.TransitionTo(PluginLifecycleState.Activating),
                PluginLifecycleState.Selected,
                PluginLifecycleState.Activating
            );
            AssertAccepted(
                record.TransitionTo(PluginLifecycleState.Active),
                PluginLifecycleState.Activating,
                PluginLifecycleState.Active
            );
            return record;
        }

        private static PluginLifecycleRecord Record(string identifier)
        {
            return new PluginLifecycleRecord(new CandidateReconciliationEntry(
                CandidateReconciliationState.Selected,
                Candidate(identifier),
                null,
                null
            ));
        }

        private static RecognizedPluginCandidate Candidate(string identifier)
        {
            return new RecognizedPluginCandidate(
                identifier,
                PluginContractRules.GetIdentifierComparisonKey(identifier),
                "RM-15 Fixture",
                new Version(1, 2, 3),
                "RM15.Fixture, Version=1.0.0.0, Culture=neutral, " +
                    "PublicKeyToken=null",
                Path.GetFullPath(Path.Combine(
                    Path.GetTempPath(),
                    identifier + ".dll"
                )),
                "RM15.Fixture.Plugin",
                new string('A', 64)
            );
        }

        private static void AssertAccepted(
            PluginLifecycleTransitionResult result,
            PluginLifecycleState expectedPrevious,
            PluginLifecycleState expectedCurrent
        )
        {
            TestAssert.True(result.Accepted, "Lifecycle transition was rejected.");
            TestAssert.Equal(expectedPrevious, result.PreviousState,
                "accepted previous state");
            TestAssert.Equal(expectedCurrent, result.RequestedState,
                "accepted requested state");
            TestAssert.Equal(expectedCurrent, result.CurrentState,
                "accepted current state");
            TestAssert.Equal(null, result.Diagnostic,
                "accepted transition diagnostic");
        }

        private static void AssertRejectedWithoutMutation(
            PluginLifecycleRecord record,
            Func<PluginLifecycleTransitionResult> transition,
            PluginLifecycleTransitionDiagnosticCode expectedCode,
            PluginLifecycleState requestedState
        )
        {
            PluginLifecycleState priorState = record.State;
            PluginLifecycleFailure priorFailure = record.Failure;
            PluginLifecycleTransitionResult result = transition();
            TestAssert.True(!result.Accepted, "Invalid transition was accepted.");
            TestAssert.Equal(priorState, result.PreviousState,
                "rejected previous state");
            TestAssert.Equal(requestedState, result.RequestedState,
                "rejected requested state");
            TestAssert.Equal(priorState, result.CurrentState,
                "rejected current state");
            TestAssert.Equal(expectedCode, result.Diagnostic.Code,
                "transition diagnostic code");
            TestAssert.True(
                !string.IsNullOrWhiteSpace(result.Diagnostic.Detail),
                "Transition diagnostic detail is empty."
            );
            TestAssert.Equal(priorState, record.State,
                "state after rejected transition");
            TestAssert.True(
                object.ReferenceEquals(priorFailure, record.Failure),
                "Rejected transition altered the failure record."
            );
        }

        private static void AssertFailure(
            PluginLifecycleRecord record,
            string identifier,
            string phase,
            Exception exception
        )
        {
            PluginLifecycleFailure failure = record.Failure;
            TestAssert.True(failure != null, "Lifecycle failure is missing.");
            TestAssert.Equal(identifier, failure.Identifier,
                "failure identifier");
            TestAssert.Equal("1.2.3", failure.Version.ToString(3),
                "failure version");
            TestAssert.Equal(record.Candidate.AssemblyPath, failure.AssemblyPath,
                "failure path");
            TestAssert.Equal(record.Candidate.TypeName, failure.TypeName,
                "failure type");
            TestAssert.Equal(phase, failure.Phase, "failure phase");
            TestAssert.True(
                object.ReferenceEquals(exception, failure.Exception),
                "Failure did not retain the original exception."
            );
            TestAssert.Equal(exception.ToString(), failure.ExceptionText,
                "complete failure exception");
            TestAssert.True(
                failure.ExceptionText.Contains("first line") &&
                failure.ExceptionText.Contains("second line") &&
                failure.ExceptionText.Contains("inner failure"),
                "Multiline or inner exception context was lost."
            );
        }

        private static Exception CompleteException(string phase)
        {
            return new ApplicationException(
                phase + " first line\r\nsecond line",
                new InvalidOperationException("inner failure")
            );
        }
    }
}
