# Reward API integration

The runtime assembly remains engine-independent and adds no dependencies.
Types below live in `Dreamy.Economy.Rewards`; the existing wallet and its save
types remain in `Dreamy.Economy`.

## Immutable payload and identity

```csharp
new ResolvedRewardEntry(string entryId, string kindId, string targetId, long amount);
new ResolvedRewardBundle(int schemaVersion, string resolverVersion,
    IReadOnlyList<ResolvedRewardEntry> entries);

RewardIdentity.Request(scope, feature, definitionId, occurrenceId, claimId);
RewardIdentity.EntryTransaction(requestId, entryId);
RewardIdentity.Fingerprint(params string[] fields);
```

Bundle schema 1 requires positive amounts, nonempty IDs and unique entry IDs.
Its constructor defensively copies the ordered entries. Its `Fingerprint`
includes the schema, resolver version and every ordered entry field. Reordering
entries changes the payload fingerprint, but does not change entry identities.
Revision belongs in the request fingerprint and feature snapshot, not in a
new request identity that could regrant the same occurrence.

Canonical encoding is `reward-v1:` followed by each field's invariant decimal
UTF-16 code-unit length, `:`, then its exact value. There is no normalization.
Fingerprints are `sha256-v1:` plus lowercase SHA-256 hex of the strict UTF-8
encoding of this canonical representation. Malformed UTF-16 is rejected.
`Request` and `EntryTransaction` add distinct domain fields. No index,
`GetHashCode`, serializer property order or delimiter concatenation is used.

The caller creates the request fingerprint from all authoritative claim inputs,
including the original revision and configured reward/resolver inputs. Economy
cannot infer those fields from the final bundle. Persist the fingerprint and
original context before dispatching a remote resolver; its retry/query must use
the same request ID. Once `Resolved`, read the journal's fixed bundle and do not
resolve again. A resolver failure leaves intent pending and grants nothing.

## Destinations and gateway

```csharp
public interface IRewardDestination
{
    string KindId { get; }
    RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests);
    RewardDeliveryResult Deliver(RewardDeliveryRequest request);
}

var gateway = new RewardDeliveryGateway(new IRewardDestination[] { wallet, hostItemOwner });
```

`Validate` is nonmutating. The gateway first checks all routes, groups the whole
bundle by destination, then validates every group before delivery. An owner
must check target existence and combined checked arithmetic across its group,
excluding identical durable receipts. The wallet accepts every syntactically
valid `ResourceId`; a host with a finite currency catalog must enforce that
catalog in its own destination. The package does not own an item inventory.

`RewardDeliveryRequest` takes `(transactionId, requestFingerprint,
resolvedFingerprint, entry)` and derives `Fingerprint` from both fingerprints
and the exact entry payload. Store that fingerprint and transaction ID with
the effect in one detached owner candidate. Persist, then commit runtime
state/cache/events; return `Delivered` only afterward. A durable receipt with
the same fingerprint returns `AlreadyDelivered`; a different fingerprint
returns nonretryable `PayloadMismatch`. A legacy receipt with no fingerprint
returns nonretryable `LegacyReceiptUnverifiable`. A known deterministic invalid
request must not be reported as a retryable persistence error.

Statuses are `Delivered`, `AlreadyDelivered`, `Pending` and `Failed`.
`Reason`, `Retryable` and `IsSuccess` are exposed. An unknown backend outcome
returns `Pending` and is recovered under the same ID, never a new one.
The gateway itself does not promise durability; each destination must fulfill
the contract. Do not wrap an arbitrary legacy bool wallet and call it strict.

## Feature-owned embedded journal

```csharp
var coordinator = new RewardDeliveryCoordinator(gateway, featureSave.PendingJournal);
RewardDeliveryResult begin = coordinator.Begin(requestId, requestFingerprint, PersistJournal);
RewardDeliveryResult resolved = coordinator.Resolve(fixedBundle, PersistJournal);
RewardDeliveryResult delivered = coordinator.Deliver(PersistJournal);
RewardDeliveryResult claimed = coordinator.Complete(PersistClaimed);
```

Check `IsSuccess` after **each** operation; do not run later operations after
failure. `Begin` is for a new intent or the identical existing request. A
different request cannot overwrite an existing journal. Create a new
coordinator only after the feature has durably retired/finalized the old claim
and retained enough claimed identity to reject replay.

Every persistence callback is `Action<RewardJournal>` and receives a detached
candidate. Clone the **whole feature save**, assign this journal, preserve its
original claim date/window/cycle/revision and intended next state, save that
candidate, and only then replace the feature's runtime state. `PersistClaimed`
must set claimed state and this `Completed` journal in that same candidate and
same save call. It is not an acknowledgement in a second save key. Do not mutate
the supplied journal or clear the pending context while delivery is recoverable.

`Journal` returns a deep copy; failed callbacks cannot mutate the coordinator's
current journal. The serialized DTO uses public fields, lists and `[Serializable]`
without a serializer dependency. Its states are `IntentPersisted`, `Resolved`,
`Delivering`, `DeliveredAwaitingClaimCommit`, `Completed`; `IsValid()` rejects
unsupported schema, corrupted fingerprints, invalid entries and inconsistent
acknowledgement states. `GetResolvedBundle()` restores the immutable snapshot.

Operation results describe that operation: `Begin`/`Resolve` success means its
snapshot persisted, not that any reward was granted. `Deliver` success means all
entries are acknowledged; **only successful `Complete` means the feature can
report claimed**. If the current journal is already awaiting final commit,
delivery returns `AlreadyDelivered` without dispatching owners.

Recovery at startup happens before new eligibility, date/window resets or
configuration replacement:

1. Restore every destination and the feature from persisted data.
2. If intent-only, recover/resolve using the original request and persist it.
3. Call `Deliver`; previously acknowledged entries are skipped. If an owner
   committed but its journal acknowledgement failed, its receipt returns
   `AlreadyDelivered` and the coordinator retries that acknowledgement.
4. Call `Complete` to atomically persist original claimed state and `Completed`.
5. Publish the committed feature state.

Use one live coordinator per feature owner and one live writer per destination.
Calls are synchronous on the Unity main thread, not thread-safe. Coordinator,
gateway and wallet callbacks retain their busy guard through persistence and
notifications. Reentry returns `Pending/Busy`; legacy wallet calls return
`false`. The feature must also guard eligibility and finalization around the
coordinator. No asynchronous dependency or resolver framework is added.

## Wallet compatibility and migration

Existing wallet constructor and methods remain unchanged. Legacy save failures
still throw; strict save failures return `Pending/PersistenceFailed`.
Grant/exchange now reject default invalid values and checked overflow without
mutating balances, consuming IDs or notifying. Exchange persists debit and all
rewards once. Repeated changed-resource notifications are coalesced.

`EconomySaveData.Version` is now 2. Existing balances and `TransactionIds` load
without rewriting them. `DeliveryReceipts` hold strict ID/fingerprint records
for the lifetime of this save namespace. `LegacyDeliveryTombstones` preserve
loaded and newly committed legacy IDs for strict collision rejection even
after their bounded legacy history expires. Legacy calls intentionally consult
only their bounded IDs plus actual strict receipts, so their old eviction
behavior remains. Migrated tombstones persist with the next successful owner
write; old IDs already evicted before migration cannot be recovered.

Do not prune strict receipts or tombstones without a replay-rejection protocol.
Independent save rollback, tampering and cross-owner atomicity remain outside
the guarantee. A backup must restore a consistent generation across owners;
the tests do not claim that arbitrary old backups prevent duplicate grants.

## Tests

`Tests/Runtime/Dreamy.Economy.Runtime.Tests.asmdef` follows the neighboring
Editor NUnit assembly convention. Fault fixtures serialize feature, wallet and
fake item state and reconstruct **every** owner/service/coordinator after each
failure boundary, including failures after writes. File-backed tests use unique
absolute temporary directories and real DataSave codecs/cache/temp/backup
behavior; they never change Unity's global persistent path or player saves.
