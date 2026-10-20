# Feature adapters

Economy does not reference feature packages. Adapters live in the feature's Integration/RewardPresentation assembly; importable primary UI sample compiles independently of Daily/Missions.

## Daily Reward 0.3.0 candidate

`DailyRewardModel` optionally implements `IDailyRewardClaimOffers`. `GetClaimOffer(token)` and `ClaimOffer(token, expectedFingerprint)` bind a concrete reward to schedule/save scope/date/cycle/entry/revision. Validation occurs inside the model; midnight cannot redirect an old preview to the next day. Existing `IDailyRewardService.Claim()` remains unchanged.

```csharp
var source = new Dreamy.DailyReward.Presentation.DailyRewardClaimSource(
    profileScope, (Dreamy.DailyReward.IDailyRewardClaimOffers)dailyService);
var reference = source.CurrentReference();
if (reference != null)
    await presentation.OpenAsync(RewardPresentationRequest.Confirm(reference), source, token);
```

References: `Dreamy.DailyReward.RewardPresentation`, `Dreamy.DailyReward.Runtime`, Economy.UI.Integration, Economy.Runtime, UniTask. Strict gateway construction is required; legacy bool-wallet and unresolved mystery previews are unavailable. Current pending and last completed claim can be queried from existing persisted journal/context; this is not a full historic notice archive. Once a new claim replaces that context, an old unrecorded reference is unavailable, never reassigned to a new day.

The adapter blocks on ambiguous persistence failures. Stop old consumers, reload affected destinations and reconstruct the Daily service/adapter from durable state before accepting new mutation. UI cancellation does not do this reconstruction.

## Missions 0.3.0 candidate

`MissionModel` optionally implements `IMissionClaimOffers`. Offers bind catalog/save scope/window/mission/revision/reroll snapshot and bundle. Query does not refresh/advance a period. Execution can resume an original persisted pending claim across reset; stale unstarted offers do not claim a new mission with the same ID.

```csharp
var source = new Dreamy.Missions.Presentation.MissionRewardClaimSource(
    profileScope, missionId, (Dreamy.Missions.IMissionClaimOffers)missionService);
var reference = source.CurrentReference();
if (reference != null)
    await presentation.OpenAsync(RewardPresentationRequest.Confirm(reference), source, token);
```

References: `Dreamy.Missions.RewardPresentation`, `Dreamy.Missions.Runtime`, Economy.UI.Integration, Economy.Runtime, UniTask. A single adapter is bound to a mission. Original pending/completed records are read through the domain API, not private-save reflection. On a non-success result without a reliable persistence discriminator, this conservative adapter requires host reconstruction (Busy may retry); it does not automatically regrant under a fresh token.

## Other features

- Existing Daily/Mission presenter buttons can retain direct Claim. To show a notice, capture an authoritative completed snapshot through the scoped adapter. Do not attach two claim handlers.
- Progression currently delivers during advancement. Use its final milestone state for Notice or implement a dedicated host source for retry. “Wait for Claim before giving milestone rewards” requires a separate opt-in domain change; a UI adapter cannot undo already delivered rewards.
- Shop fulfills virtual exchanges before Notice. IAP production receipt/restore/recovery remains Shop/host responsibility; do not wait for Continue to fulfill a paid purchase.
- Battle Pass is still a plan. Its future adapter belongs in its sample/host and uses profile/season occurrence/tier/track identity; Claim All aggregate notices do not merge delivery identities.
- Achievements/mailbox/chest/gameplay supply IRewardClaimSource with their own durable eligibility/expiry/recovery. The demo owner is reference sample code, not a global production grant endpoint.

## Migration

These additions do not change Economy save v4, Daily save v2 or Missions save schema. Existing callers and serialized references remain intact. New integration assemblies require Economy 0.7.0 candidate and UniTask declared directly; update the feature and Economy together, then resolve lock. No release tags were published by this implementation step.
