using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    // One session owns subscriptions and UI observation; the source owns durable operations.
    public sealed class RewardPresentationSession : IDisposable
    {
        private readonly RewardPresentationRequest request;
        private readonly IRewardClaimSource source;
        private readonly IRewardPresentationView view;
        private readonly UniTaskCompletionSource<RewardPresentationResult> completion = new();
        private RewardClaimSnapshot snapshot;
        private bool bound, ended, executing, dispatched;
        private CancellationToken uiToken;
        private string error;
        public event Action<Exception> Diagnostic;
        public event Action<RewardClaimSnapshot> NewlyClaimed;

        public RewardPresentationSession(RewardPresentationRequest request, IRewardClaimSource source, IRewardPresentationView view)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            if (request.Mode == RewardPresentationMode.ConfirmClaim && source == null) throw new ArgumentNullException(nameof(source));
            this.source = source;
        }

        public async UniTask<RewardPresentationResult> RunAsync(CancellationToken uiCancellationToken = default)
        {
            if (bound || ended) throw new InvalidOperationException("A presentation session is single-use.");
            uiToken = uiCancellationToken;
            bound = true;
            view.ClaimRequested += Claim;
            view.CloseRequested += Close;
            view.Detached += Detach;
            try
            {
                if (uiToken.IsCancellationRequested) Cancel();
                else
                {
                    using (uiToken.Register(Cancel))
                    {
                        if (request.Mode == RewardPresentationMode.GrantedNotice)
                        { snapshot = request.Granted; Render(); }
                        else Load().Forget(Report);
                        return await completion.Task;
                    }
                }
                return await completion.Task;
            }
            finally { Dispose(); }
        }

        private async UniTask Load()
        {
            Render(loading: true);
            try
            {
                var value = await source.QueryAsync(request.Reference, uiToken);
                if (ended) return;
                if (!Matches(value) || (request.ExpectedFingerprint != null && value.Fingerprint != request.ExpectedFingerprint))
                { Render(error: "PayloadMismatch"); return; }
                snapshot = value; Render();
            }
            catch (OperationCanceledException) when (uiToken.IsCancellationRequested) { Cancel(); }
            catch (Exception exception) { Report(exception); Render(error: "QueryFailed"); }
        }

        private bool Matches(RewardClaimSnapshot value) => value != null && value.Reference.Equals(request.Reference);
        private void Claim()
        {
            if (ended || executing || error != null || snapshot == null || !new RewardPresentationState(request, snapshot).CanClaim) return;
            Execute().Forget(Report);
        }
        private async UniTask Execute()
        {
            executing = true;
            var expected = snapshot;
            Render();
            // Rendering/close listeners may cancel before dispatch.
            if (ended) { executing = false; return; }
            dispatched = true;
            try
            {
                var value = await source.ClaimAsync(new RewardClaimCommand(request.Reference, expected.Fingerprint));
                if (ended) return; // completion is still observed, but no stale view may render it.
                if (!Matches(value) || value.Fingerprint != expected.Fingerprint)
                { snapshot = null; Render(error: "PayloadMismatch"); return; }
                snapshot = value;
                executing = false;
                if (value.Status == RewardClaimStatus.Claimed)
                {
                    var listeners = NewlyClaimed;
                    if (listeners != null) foreach (Action<RewardClaimSnapshot> listener in listeners.GetInvocationList())
                        try { listener(value); } catch (Exception exception) { Report(exception); }
                }
            }
            catch (Exception exception)
            {
                Report(exception);
                // Unknown outcome: never enable a fresh claim from the old Available snapshot.
                snapshot = new RewardClaimSnapshot(request.Reference, expected.Fingerprint, expected.Bundle,
                    RewardClaimStatus.Pending, "UnknownOutcome", canRetry: true);
            }
            finally { executing = false; Render(); }
        }
        private void Render(bool loading = false, string error = null)
        {
            if (ended) return;
            if (error != null) this.error = error;
            try { view.Render(new RewardPresentationState(request, snapshot, loading, executing, this.error)); }
            catch (Exception exception) { Report(exception); Finish(RewardPresentationOutcome.PresentationUnavailable); }
        }
        private void Report(Exception exception) { try { Diagnostic?.Invoke(exception); } catch (Exception) { } }
        private void Close() => Finish(snapshot != null && snapshot.IsCompleted ? RewardPresentationOutcome.ClosedAfterCompletion : RewardPresentationOutcome.Dismissed);
        private void Detach() => Finish(executing && dispatched ? RewardPresentationOutcome.DetachedWhileExecuting : RewardPresentationOutcome.Dismissed);
        private void Cancel() => Finish(executing && dispatched ? RewardPresentationOutcome.DetachedWhileExecuting : RewardPresentationOutcome.Cancelled);
        private void Finish(RewardPresentationOutcome outcome)
        {
            if (ended) return;
            ended = true;
            if (executing && dispatched) outcome = RewardPresentationOutcome.DetachedWhileExecuting;
            var observed = executing && dispatched ? RewardClaimObservation.Unknown : snapshot?.IsCompleted == true ? RewardClaimObservation.Completed :
                snapshot?.Status == RewardClaimStatus.RequiresReload ? RewardClaimObservation.RequiresReload :
                snapshot?.Status == RewardClaimStatus.Pending ? RewardClaimObservation.Pending : dispatched ? RewardClaimObservation.Unknown : RewardClaimObservation.NotDispatched;
            completion.TrySetResult(new RewardPresentationResult(outcome, observed, snapshot));
        }
        public void Dispose()
        {
            if (!ended) Detach();
            if (!bound) return;
            bound = false;
            view.ClaimRequested -= Claim; view.CloseRequested -= Close; view.Detached -= Detach;
        }
    }
}
