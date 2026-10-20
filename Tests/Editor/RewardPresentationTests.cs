using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.Rewards;
using Dreamy.Economy.UI;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Dreamy.Economy.Tests
{
    public sealed class RewardPresentationTests
    {
        private static readonly RewardClaimReference Reference = new("profile", "level", "run-1");
        private static readonly ResolvedRewardBundle Bundle = new(1, "test", new[] { new ResolvedRewardEntry("coin", "currency", "currency.coin", 100) });
        private static RewardClaimSnapshot Snapshot(RewardClaimStatus status = RewardClaimStatus.Available, bool retry = false) =>
            new(Reference, Bundle.Fingerprint, Bundle, status, canRetry: retry);
        private sealed class View : IRewardPresentationView
        {
            public event Action ClaimRequested;
            public event Action CloseRequested;
            public event Action Detached;
            public RewardPresentationState State;
            public int Renders;
            public void Render(RewardPresentationState state) { State = state; Renders++; }
            public void Claim() => ClaimRequested?.Invoke();
            public void Close() => CloseRequested?.Invoke();
            public void Destroy() => Detached?.Invoke();
        }
        private sealed class Source : IRewardClaimSource
        {
            public RewardClaimSnapshot Current = Snapshot();
            public int Calls, Queries;
            public UniTaskCompletionSource<RewardClaimSnapshot> Pending;
            public bool Throw;
            public UniTask<RewardClaimSnapshot> QueryAsync(RewardClaimReference reference, CancellationToken token)
            { token.ThrowIfCancellationRequested(); Queries++; return UniTask.FromResult(Current); }
            public UniTask<RewardClaimSnapshot> ClaimAsync(RewardClaimCommand command)
            {
                Calls++;
                if (Throw) throw new InvalidOperationException("unknown write outcome");
                if (command.ExpectedFingerprint != Current.Fingerprint) throw new InvalidOperationException("mismatch");
                if (Pending != null) return Pending.Task;
                return UniTask.FromResult(Current = Snapshot(RewardClaimStatus.Claimed));
            }
        }
        private sealed class Host : IRewardPresentationHost
        {
            public View View = new();
            public int Opens, Closes;
            public UniTask<IRewardPresentationView> OpenAsync(RewardPresentationRequest request, CancellationToken token)
            { Opens++; return UniTask.FromResult<IRewardPresentationView>(View); }
            public UniTask CloseAsync(IRewardPresentationView view) { Closes++; return UniTask.CompletedTask; }
        }

        [Test] public void ReferenceUsesCanonicalEncoding() => Assert.AreNotEqual(
            new RewardClaimReference("a:b", "c", "d").Key, new RewardClaimReference("a", "b:c", "d").Key);
        [Test] public void InvalidReferenceRejected() => Assert.Throws<ArgumentException>(() => new RewardClaimReference("", "s", "c"));
        [Test] public void NoticeRequiresFinalCommit() => Assert.Throws<ArgumentException>(() => RewardPresentationRequest.Notice(Snapshot(RewardClaimStatus.Pending)));
        [Test] public void AvailableRequiresPayload() => Assert.Throws<ArgumentException>(() => new RewardClaimSnapshot(Reference,"f",null,RewardClaimStatus.Available));
        [Test] public void BlockedCannotBecomeRetryable() => Assert.IsFalse(Snapshot(RewardClaimStatus.Blocked, true).CanRetry);

        [UnityTest] public IEnumerator PreviewDoesNotClaim() => UniTask.ToCoroutine(async () =>
        {
            var source = new Source(); var view = new View();
            using var session = new RewardPresentationSession(RewardPresentationRequest.Confirm(Reference), source, view);
            var task = session.RunAsync();
            Assert.IsTrue(view.State.CanClaim); Assert.AreEqual(0, source.Calls);
            view.Close(); var result = await task;
            Assert.AreEqual(RewardClaimObservation.NotDispatched, result.Observation);
        });
        [UnityTest] public IEnumerator ClaimAndReopenCannotDispatchAgain() => UniTask.ToCoroutine(async () =>
        {
            var source = new Source(); var view = new View();
            using(var session = new RewardPresentationSession(RewardPresentationRequest.Confirm(Reference), source, view))
            {
                var task = session.RunAsync(); view.Claim(); view.Claim();
                Assert.AreEqual(1,source.Calls); Assert.IsTrue(view.State.Snapshot.IsCompleted);
                view.Close(); Assert.AreEqual(RewardClaimObservation.Completed,(await task).Observation);
            }
            using(var session = new RewardPresentationSession(RewardPresentationRequest.Confirm(Reference),source,view))
            { var task=session.RunAsync(); view.Claim(); view.Close(); await task; Assert.AreEqual(1,source.Calls); }
        });
        [UnityTest] public IEnumerator NoticeNeverQueriesOrClaims() => UniTask.ToCoroutine(async () =>
        {
            var source=new Source(); var view=new View();
            using var session=new RewardPresentationSession(RewardPresentationRequest.Notice(Snapshot(RewardClaimStatus.Claimed)),source,view);
            var task=session.RunAsync(); view.Claim(); view.Close(); await task;
            Assert.AreEqual(0,source.Calls); Assert.AreEqual(0,source.Queries);
        });
        [UnityTest] public IEnumerator DetachedExecutionIsObservedWithoutRenderingNewView() => UniTask.ToCoroutine(async () =>
        {
            var source=new Source { Pending=new UniTaskCompletionSource<RewardClaimSnapshot>() }; var view=new View();
            using var session=new RewardPresentationSession(RewardPresentationRequest.Confirm(Reference),source,view);
            var task=session.RunAsync(); view.Claim(); view.Claim(); view.Destroy(); var result=await task;
            Assert.AreEqual(RewardPresentationOutcome.DetachedWhileExecuting,result.Outcome);
            Assert.AreEqual(RewardClaimObservation.Unknown,result.Observation); Assert.AreEqual(1,source.Calls);
            int renders=view.Renders; source.Pending.TrySetResult(Snapshot(RewardClaimStatus.Claimed));
            await UniTask.Yield(); Assert.AreEqual(renders,view.Renders);
        });
        [UnityTest] public IEnumerator CancelBeforeDispatchDoesNotClaim() => UniTask.ToCoroutine(async () =>
        {
            var source=new Source(); var view=new View(); using var cancel=new CancellationTokenSource();
            using var session=new RewardPresentationSession(RewardPresentationRequest.Confirm(Reference),source,view);
            var task=session.RunAsync(cancel.Token); cancel.Cancel(); await task; view.Claim(); Assert.AreEqual(0,source.Calls);
        });
        [UnityTest] public IEnumerator ExpectedSnapshotMismatchDisablesClaim() => UniTask.ToCoroutine(async () =>
        {
            var source=new Source(); var view=new View();
            using var session=new RewardPresentationSession(RewardPresentationRequest.Confirm(Reference,expectedFingerprint:"wrong"),source,view);
            var task=session.RunAsync(); Assert.AreEqual("PayloadMismatch",view.State.Error);
            view.Claim(); view.Close(); await task; Assert.AreEqual(0,source.Calls);
        });
        [UnityTest] public IEnumerator ExceptionAfterDispatchIsPendingNotFailedGrant() => UniTask.ToCoroutine(async () =>
        {
            var source=new Source{Throw=true}; var view=new View();
            using var session=new RewardPresentationSession(RewardPresentationRequest.Confirm(Reference),source,view);
            var task=session.RunAsync(); view.Claim(); Assert.AreEqual(RewardClaimStatus.Pending,view.State.Snapshot.Status);
            view.Close(); Assert.AreEqual(RewardClaimObservation.Pending,(await task).Observation);
        });
        [UnityTest] public IEnumerator ListenerFailureDoesNotInvalidateCompletion() => UniTask.ToCoroutine(async () =>
        {
            var source=new Source(); var view=new View();
            using var session=new RewardPresentationSession(RewardPresentationRequest.Confirm(Reference),source,view);
            session.NewlyClaimed += _ => throw new InvalidOperationException("feedback");
            var task=session.RunAsync(); view.Claim(); view.Close(); Assert.AreEqual(RewardClaimObservation.Completed,(await task).Observation);
        });
        [UnityTest] public IEnumerator QueueRejectsDuplicatesMismatchAndOverflow() => UniTask.ToCoroutine(async () =>
        {
            var host=new Host(); var source=new Source(); using var queue=new RewardPresentationQueue("profile",host,1);
            var request=RewardPresentationRequest.Confirm(Reference); var first=queue.OpenAsync(request,source);
            Assert.AreEqual(RewardPresentationOutcome.AlreadyQueued,(await queue.OpenAsync(request,source)).Outcome);
            Assert.AreEqual(RewardPresentationOutcome.PayloadMismatch,(await queue.OpenAsync(RewardPresentationRequest.Confirm(Reference,"different"),source)).Outcome);
            Assert.AreEqual(RewardPresentationOutcome.QueueFull,(await queue.OpenAsync(RewardPresentationRequest.Confirm(new RewardClaimReference("profile","level","run-2")),source)).Outcome);
            host.View.Close(); await first; Assert.AreEqual(1,host.Closes);
        });
        [Test] public void QueueRejectsWrongProfile()
        {
            using var queue=new RewardPresentationQueue("other",new Host());
            Assert.Throws<ArgumentException>(() => queue.OpenAsync(RewardPresentationRequest.Confirm(Reference),new Source()));
        }
        [UnityTest] public IEnumerator HiddenInlineClaimNeedsNoView() => UniTask.ToCoroutine(async () =>
        { var source=new Source(); var result=await RewardClaimFlow.ClaimAsync(Reference,source); Assert.IsTrue(result.IsCompleted); Assert.AreEqual(1,source.Calls); });
        [UnityTest] public IEnumerator DisposeQueueDetachesWithoutCancellingSource() => UniTask.ToCoroutine(async () =>
        {
            var host=new Host(); var source=new Source{Pending=new UniTaskCompletionSource<RewardClaimSnapshot>()};
            var queue=new RewardPresentationQueue("profile",host);
            var task=queue.OpenAsync(RewardPresentationRequest.Confirm(Reference),source); host.View.Claim(); queue.Dispose();
            Assert.AreEqual(RewardClaimObservation.Unknown,(await task).Observation);
            Assert.IsTrue(source.Pending.TrySetResult(Snapshot(RewardClaimStatus.Claimed)));
        });
    }
}
