# Claim-before-grant recovery

The UI never owns the persistent transaction. Feature save holds earned eligibility and an embedded RewardJournal; each destination stores effects plus receipts in one candidate; feature Complete stores claimed plus Completed journal together.

1. Save earned completion/entitlement before requiring a confirmation panel.
2. Opening/querying does not grant. On Claim, persist original context/intent before delivery.
3. Resolve/freeze the payload, deliver using stable entry transaction IDs, persist acknowledgements, then final claimed transition.
4. A failure after an owner write may have applied its effect. Stop/reload/reconcile all relevant owners from durable state and retry the original claim.
5. A receipt returns AlreadyDelivered after acknowledgement loss; it must not increment balances again.
6. Pending partial delivery is not a completed bundle. Only final feature commit yields Claimed.
7. Final success followed by lost callback is recovered by querying the same claim. Notice dismissal does not grant or undo it.

The sample's Inject Final Save Failure button fails after coin/hammer delivery but before feature final commit. Close, Reload / Recover, open the reward again and Claim: balances remain unchanged while the feature finalizes. Tests reconstruct every owner against real temporary Datasave files.

UI queue dedup is transient, strict receipt retention is durable. Never prune receipts using popup history. Restore consistent save generations; independent save rollback, tampering, remote authority and universal filesystem power-loss durability are outside this module's guarantee.
