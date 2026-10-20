# Reward Presentation

Working candidate: Economy 0.7.0. This is presentation over a feature-owned claim source; the panel never grants directly or owns player saves.

## Choose a flow

| Flow | Call | Grant timing |
| --- | --- | --- |
| Confirm before receiving | `presentation.OpenAsync(RewardPresentationRequest.Confirm(reference), source, token)` | Only after the player presses Claim. |
| Inline claim, no popup | `RewardClaimFlow.ClaimAsync(reference, source, uiCancellationToken: token)` | This call is the explicit claim intent. |
| Inline claim, optional received notice | `RewardClaimFlow.ClaimAsync(reference, source, presentation, token)` | Final feature commit precedes the notice. |
| Display an already committed claim | `presentation.OpenAsync(RewardPresentationRequest.Notice(completed), uiCancellationToken: token)` | No grant/query/claim dependency in the notice path. |

Example host choice (inside an async UniTask method):

```csharp
if (confirmBeforeClaim)
    await presentation.OpenAsync(RewardPresentationRequest.Confirm(reference), source, token);
else
    await RewardClaimFlow.ClaimAsync(reference, source,
        showReceivedPanel ? presentation : null, token);
```

Do not configure manual claiming without a panel or another reachable claim button. Turning off presentation is not permission to consume or discard an entitlement. A feature's existing Claim button is already an intent; normally use direct claim plus optional notice rather than a second confirmation.

## Composition

1. Restore the existing profile's Datasave, wallet/inventory destinations and feature services. One writer per owner/save key.
2. Inject an `IRewardClaimSource` for the exact feature/claim. The feature must retain original eligibility, reward payload, delivery journal and final claimed state.
3. Inject an `IRewardPresentationHost` that opens/closes one leased view. Import the Reward Presentation sample for a ready uGUI `RewardPanel` and `RewardPanelPresentation` scene-panel host.
4. Create one `RewardPresentationQueue(profileScope, host, capacity)` at the root. Dispose on root/profile teardown; do not create a wallet or queue for every popup opening.
5. Root decides when it may display a modal. Hook queue `Diagnostic` to logging. Optional `NewlyClaimed` feedback occurs only for a newly completed Claim execution, never Notice/reopen.

Assembly references: source/presenter callers use `Dreamy.Economy.UI.Integration`, `Dreamy.Economy.Runtime` and `UniTask`. Concrete sample callers also reference `Dreamy.Economy.RewardPresentation.Sample` and UI assemblies they use directly. Economy.Runtime remains engine-independent and does not reference this presentation layer or feature packages.

## Source contract

`QueryAsync(reference, token)` is read-only and queries the original stable profile/source/claim ID. A source may not interpret it as “whatever reward is current now”.

`ClaimAsync(command)` revalidates original reference and expected fingerprint at the same domain boundary that checks eligibility. It returns `Claimed` only after final feature commit, not after Begin/Resolve or the first destination delivery. A synchronous Dreamy service returns an already completed UniTask on the owner thread; do not Task.Run wallet mutations.

Status semantics: Available may execute; Pending/Busy retry only when CanRetry; AlreadyClaimed is a final observation without a new effect; Blocked/Expired/Unavailable cannot execute; RequiresReload stops mutation until source and affected owners reconcile durable state. Use original immutable bundles; display rows are not authority.

A `RewardClaimSnapshot` is a trusted adapter observation, not a cryptographic receipt. Do not create completed snapshots from an arbitrary preview bundle to simulate a successful backend purchase.

## Result and cancellation

`RewardPresentationResult.Outcome` describes UI. `Observation` describes the claim last observed. Dismissed/Cancelled/PresentationUnavailable never proves no grant occurred. After dispatch, UI cancellation detaches the view while the source continues/reconciles; result can be Unknown. Query the original reference when returning.

Closing before Claim preserves the feature's entitlement until its own expiry. Closing after commit or skipping a notice never rolls back rewards. Source tasks remain observed after detach. Each root and UI operation runs on the owner thread; asynchronous adapters must resume there before calling synchronous Dreamy owners.

Queue is bounded in-memory FIFO, with one modal. Same presentation key/fingerprint returns AlreadyQueued; changed payload returns PayloadMismatch; capacity returns QueueFull. It is not persisted and does not replace delivery receipts or claim history. A game requiring unseen notices across restarts needs a feature-owned notice outbox committed with final claimed state.

## Visuals and extensions

The sample uses ResourceDisplayCatalog/ResourceAmountFormatter, a cached UIPanel with SafeArea, and reusable reward rows. Reskin/move via Unity with .meta files, or replace IRewardPresentationView/IRewardPresentationHost. Custom resources can supply their own visual adapter; eligibility, inventory metadata, SDK and random/chest rules remain in game services.

Bundle amounts represent entitlement, not a universal actual balance delta. Unique items may already be owned; do not promise +1 new quantity unless the source exposes that meaning. Do not infer final claim completion from BalanceChanged.

## Limits

No global reward inbox, cloud merge, server authority, IAP fulfillment engine, random resolver or multi-owner atomic transaction is added. Progression auto-delivery and Shop purchase fulfillment are unchanged. Their completed results may be displayed as Notice. Battle Pass integration awaits that package's implementation; use its stable slot/occurrence claim source when available.
