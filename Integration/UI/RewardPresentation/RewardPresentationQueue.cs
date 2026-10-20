using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    // In-memory FIFO, one root/profile. Never owns claim persistence or receipt retention.
    public sealed class RewardPresentationQueue : IRewardPresentation, IDisposable
    {
        private sealed class Work
        {
            public RewardPresentationRequest Request;
            public IRewardClaimSource Source;
            public CancellationToken Token;
            public readonly UniTaskCompletionSource<RewardPresentationResult> Completion = new();
        }
        private readonly IRewardPresentationHost host;
        private readonly Queue<Work> pending = new();
        private readonly Dictionary<string, string> keys = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource lifetime = new();
        private readonly int capacity;
        private readonly string scope;
        private bool pumping, disposed;
        public event Action<Exception> Diagnostic;
        public event Action<RewardClaimSnapshot> NewlyClaimed;
        public RewardPresentationQueue(string scope, IRewardPresentationHost host, int capacity = 16)
        {
            if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("A profile scope is required.");
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.scope = scope; this.host = host ?? throw new ArgumentNullException(nameof(host)); this.capacity = capacity;
        }
        public UniTask<RewardPresentationResult> OpenAsync(RewardPresentationRequest request, IRewardClaimSource source = null,
            CancellationToken uiCancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Reference.Scope != scope) throw new ArgumentException("Wrong profile scope.");
            if (request.Mode == RewardPresentationMode.ConfirmClaim && source == null) throw new ArgumentNullException(nameof(source));
            if (disposed || uiCancellationToken.IsCancellationRequested) return Result(RewardPresentationOutcome.CancelledBeforeOpen);
            if (keys.TryGetValue(request.Key, out var fingerprint))
                return Result(fingerprint == request.Fingerprint ? RewardPresentationOutcome.AlreadyQueued : RewardPresentationOutcome.PayloadMismatch);
            if (keys.Count >= capacity) return Result(RewardPresentationOutcome.QueueFull);
            var work = new Work { Request = request, Source = source, Token = uiCancellationToken };
            keys.Add(request.Key, request.Fingerprint); pending.Enqueue(work);
            if (!pumping) { pumping = true; Pump().Forget(Report); }
            return work.Completion.Task;
        }
        private static UniTask<RewardPresentationResult> Result(RewardPresentationOutcome outcome) => UniTask.FromResult(new RewardPresentationResult(outcome));
        private async UniTask Pump()
        {
            try
            {
                while (pending.Count > 0)
                {
                    var work = pending.Dequeue();
                    var result = new RewardPresentationResult(RewardPresentationOutcome.CancelledBeforeOpen);
                    IRewardPresentationView view = null;
                    using (var ui = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, work.Token))
                    {
                        try
                        {
                            if (!ui.IsCancellationRequested)
                            {
                                view = await host.OpenAsync(work.Request, ui.Token);
                                ui.Token.ThrowIfCancellationRequested();
                                if (view == null) result = new RewardPresentationResult(RewardPresentationOutcome.PresentationUnavailable);
                                else
                                {
                                    using (var session = new RewardPresentationSession(work.Request, work.Source, view))
                                    {
                                        session.Diagnostic += Report;
                                        session.NewlyClaimed += Claimed;
                                        result = await session.RunAsync(ui.Token);
                                    }
                                }
                            }
                        }
                        catch (OperationCanceledException) when (ui.IsCancellationRequested) { }
                        catch (Exception exception) { Report(exception); result = new RewardPresentationResult(RewardPresentationOutcome.PresentationUnavailable); }
                        finally
                        {
                            try { await host.CloseAsync(view); } catch (Exception exception) { Report(exception); }
                            keys.Remove(work.Request.Key);
                            work.Completion.TrySetResult(result);
                        }
                    }
                }
            }
            finally { pumping = false; if (disposed) lifetime.Dispose(); }
        }
        private void Claimed(RewardClaimSnapshot snapshot)
        {
            var listeners = NewlyClaimed;
            if (listeners != null) foreach (Action<RewardClaimSnapshot> listener in listeners.GetInvocationList())
                try { listener(snapshot); } catch (Exception exception) { Report(exception); }
        }
        private void Report(Exception exception) { try { Diagnostic?.Invoke(exception); } catch (Exception) { } }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            if (!pumping) lifetime.Dispose();
        }
    }
}
