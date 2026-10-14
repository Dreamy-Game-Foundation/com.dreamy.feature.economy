# Reward delivery ownership and recovery

Status: accepted implementation decision, 2026-10-14.

## Ownership

Economy owns the reusable reward entry, bundle, identity, delivery result,
embedded journal schema and coordinator. These belong to the existing
engine-independent `Dreamy.Economy.Runtime` assembly, not its UI assembly.
Daily and Missions already reference this assembly. Economy does not reference
Feature, Daily, Missions, Shop or game code.

Feature v0.1.0 references DataConfig through asmdef GUID
`e34fd9426baf41dab67b6e4d8a9b4282`. Its manifest must declare the corresponding
`com.dreamy.dataconfig` dependency. This metadata repair does not make Feature
the owner of reward delivery.

The feature owns eligibility, its pending journal and the final claim state.
Its journal is embedded in the same save document as its claimed transition.
The currency or item owner owns the effect and its durable receipt. Game
currency remains in ResourceSave; game cards remain in UnitProgressSave.
There is no additional inventory, economy or independent journal save in the
game. Composition roots inject these owners and any resolver.

## Identities and payloads

An entry has stable entry, kind and target IDs and a positive long amount.
A bundle has a schema version and ordered, uniquely identified entries.
Resolved entries contain concrete targets, never arbitrary objects, game enums
or unresolved random selections.

Request identity includes the profile/save scope and the feature's stable
schedule or catalog identity and claim occurrence. Use length-prefixed
encoding, never delimiter concatenation. Derive delivery identity from the
request and stable entry ID, not its list position. Store a versioned canonical
payload fingerprint; reject reuse of an identity with a different payload.
Preserve recognized legacy transaction identities when adapting old features.

Validate the complete bundle and all owner routes before granting an entry.
A mystery-card resolver must persist its concrete selected IDs before delivery.
An external resolver must receive a stable request ID and support recovery of
that request; unavailable authority is an error, not permission to roll locally.

## State transitions

1. Persist a detached eligible intent in the feature save.
2. Resolve and persist a fixed concrete bundle and its intended final transition.
3. Deliver each entry using a stable sub-transaction identity.
4. Persist entry acknowledgements in a detached feature-save candidate.
5. Atomically persist claimed state and the completed journal in that document.
6. Publish committed state and report success only after persistence succeeds.

Each destination persists its effect and receipt, including payload identity,
in the same destination snapshot. A failed save must not alter cached data,
runtime dictionaries, transaction history or notifications. Currency exchange
keeps its cost and all rewards within one owner snapshot.

On restart, recover pending work before new eligibility or period/config reset.
Retry an identical entry after an acknowledgement failure; the destination's
receipt returns already-delivered without repeating its effect. Finalize a
fully delivered claim without granting again. Preserve the original claim
date, cycle, window, revision and final transition across midnight or updates.

Serialize calls per feature and save owner on the Unity main thread. Reentrant
calls return Busy. This is not a multithreaded contract.

## Results and compatibility

Delivery distinguishes Delivered, AlreadyDelivered, Pending and Failed, with
an explicit reason and retryability. Unknown targets, invalid amounts,
overflow and payload mismatch are deterministic errors. Unknown remote
outcomes remain pending; never create a fresh request to hide them.

Existing wallet methods, constructors and currency configuration remain
available. A bool-returning legacy wallet does not by itself prove durable
idempotency. Strict delivery requires a destination with persisted receipts.
Legacy ID-only receipts cannot prove payload equality; preserve them and fail
ambiguous strict collisions rather than granting again.

## Retention and limits

Strict receipts are retained for the lifetime of their profile/save namespace.
Do not evict them using the legacy 4096-entry history limit. Compaction requires
an explicit replay-rejection protocol, with durable tombstones or watermarks.
Progress-event deduplication has a separate, documented retention contract.

This protocol provides recoverable, idempotent delivery only when every
destination persists effects and receipts together and retains those records.
It does not make a multi-owner bundle atomic. It does not certify every remote
backend, prevent save tampering, recover legacy receipts already evicted, or
reconcile independent save corruption/rollback. Backup recovery must restore a
consistent save generation or require reconciliation.

New tests must recreate every persistence owner after injected failures, not
just the feature model. Cover intent, resolution, owner commit, acknowledgement,
finalization, reentrancy, payload mismatch, overflow and more than 4096 strict
deliveries. Keep existing currency-only tests and callers working.
