using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    public static class RewardClaimFlow
    {
        // Calling this is the claim intent (e.g. an inline Claim button). A notice is optional.
        // Before dispatch token may cancel; after dispatch only presentation uses the UI token.
        public static async UniTask<RewardClaimSnapshot> ClaimAsync(RewardClaimReference reference, IRewardClaimSource source,
            IRewardPresentation optionalNotice = null, CancellationToken uiCancellationToken = default, Action<Exception> presentationFailed = null)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (source == null) throw new ArgumentNullException(nameof(source));
            var current = await source.QueryAsync(reference, uiCancellationToken);
            uiCancellationToken.ThrowIfCancellationRequested();
            if (current == null || !current.Reference.Equals(reference)) throw new InvalidOperationException("Source returned another claim.");
            string expectedFingerprint = current.Fingerprint;
            if (!current.IsCompleted && (current.Status == RewardClaimStatus.Available || current.CanRetry))
                current = await source.ClaimAsync(new RewardClaimCommand(reference, current.Fingerprint));
            if (current == null || !current.Reference.Equals(reference) || current.Fingerprint != expectedFingerprint) throw new InvalidOperationException("Source returned another claim or payload.");
            if (current.IsCompleted && optionalNotice != null)
            {
                // Presentation failure must never turn a committed claim into a failed transaction.
                try { await optionalNotice.OpenAsync(RewardPresentationRequest.Notice(current), null, uiCancellationToken); }
                catch (Exception exception) { try { presentationFailed?.Invoke(exception); } catch (Exception) { } }
            }
            return current;
        }
    }
}
