# One save for wallet and inventory

Economy 0.5.0 extends DatasaveResourceWallet to store all resource balances and delivery receipts
in the same EconomySaveData v4 document. It remains IResourceWallet, IResourceBalanceSource and
IResourceTransactionWallet. No dependency on game classes, fixed reward enum or game inventory types.

```csharp
var wallet = new DatasaveResourceWallet(datasave, saveKey: "player-economy",
    rewardDefinitions: new[]
    {
        new ResourceRewardDefinition("card", new ResourceId("card.knight")),
        new ResourceRewardDefinition("chest", new ResourceId("chest.silver")),
        new ResourceRewardDefinition("cosmetic", new ResourceId("skin.hero.red"), unique: true)
    });
var gateway = new RewardDeliveryGateway(new[]
{
    wallet.CreateRewardDestination("currency"),
    wallet.CreateRewardDestination("card"),
    wallet.CreateRewardDestination("chest"),
    wallet.CreateRewardDestination("cosmetic")
});
long owned = wallet.GetBalance(new ResourceId("card.knight"));
```

Create ONE wallet instance per profile/save key at composition root. Inject that same instance
into Shop, Daily, Missions, Progression and game inventory consumers. CreateRewardDestination
makes lightweight routing adapters; adapters share owner state, mutation guard and save document.
Creating another wallet per kind using the same key is unsupported and can lose updates.
Persisted target IDs must be globally unique ResourceIds (dot-separated), even across kinds.
Preserve kind/target definitions and unique/stackable semantics across releases, or migrate explicitly.

Currency works as before; Coin and Gem are separate target IDs of the `currency` kind.
Non-currency destinations accept only registered targets. Stackable balances use checked long
arithmetic; unique balances become 1 on grant, even if already owned. Every valid entitlement
still persists its own receipt, so duplicate cosmetic delivery becomes a recorded no-op.
Unique does not implement game-specific duplicate conversion, card metadata or chest opening.
A balance is quantity/ownership; arbitrary instance metadata needs a game-owned extension/owner.

Balances for all kinds are in EconomySaveData.Balances; strict delivery fingerprints are in
DeliveryReceipts; spend/exchange receipts remain TransactionReceipts. All are saved together.
Version 1–3 balances, legacy transaction IDs and receipts remain intact on load/save as v4;
InventoryImports defaults to an empty list. Do not downgrade a v4 save to older clients.

Validate does not write. Each delivered entry commits quantity/ownership plus receipt in a
single detached candidate. Different kind grants and Shop currency spend cannot overwrite other
balances or migration markers. Listener exceptions do not invalidate committed mutations.
If a save throws, the owner blocks further mutations across ALL adapters: the write may have
committed. Reload durable state through wallet.Reload() before retrying. Existing subscriptions
and adapters remain attached to the same owner. Host persistence must load durable state, not
return a stale cached object. Recovery should reload this owner and feature journals coherently.

## Import old split inventory saves

The host knows the legacy schema. Stop old writers first and read a frozen copy of each source.
Call `ImportInventory(migrationId, amounts, originalDeliveryReceipts)` before reward recovery.

- migrationId must identify one source snapshot/policy permanently, e.g. `inventory-v1/cards`.
- amounts are positive ResourceAmounts for registered targets. Zero-count sources pass no amount.
- preserve original transaction IDs and fingerprints exactly; do not rebuild receipts from a changed config.
- destination amounts, imported receipts and migration marker are committed in one document.
- same migrationId/snapshot returns AlreadyDelivered after restart; changed snapshot returns PayloadMismatch.
- different migration sources with overlapping receipts are rejected to avoid adding the same ownership twice.
- unknown target, duplicate source entries, invalid receipts or overflow fail without mutation.
- write failure requires Reload then retry the SAME snapshot/ID; after-write failures cannot double-add counts.

This is a trusted migration boundary, not a reward grant endpoint. Import only legacy sources
not already represented in the shared save. A missing/truncated legacy receipt does not prove an
entry was never delivered; the host must audit/reconcile that ambiguity before importing/retrying.
Retain old files for rollback/diagnostics; do not continue writing them after cutover. Markers and
strict receipts must not be pruned while old snapshots or pending journals can be replayed.

Progression XP/level and feature journals remain in feature saves. One resource document does
not make an entire multi-entry bundle atomic: the coordinator still commits entries separately.
Backend authority, cloud merge and restoring different files from different generations need
explicit game policies. Custom IRewardDestinations remain supported where necessary.
