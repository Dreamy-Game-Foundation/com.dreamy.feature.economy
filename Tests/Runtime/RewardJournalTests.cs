using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Dreamy.Economy.Tests
{
    public sealed class RewardJournalTests
    {
        private static readonly ResolvedRewardBundle Bundle = RewardContractTests.Bundle(
            RewardContractTests.Coin(), new ResolvedRewardEntry("card", "item", "unit.scout", 3));

        [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)] [TestCase(4, false)] [TestCase(5, false)]
        [TestCase(1, true)] [TestCase(2, true)] [TestCase(3, true)] [TestCase(4, true)] [TestCase(5, true)]
        public void EveryFeatureWriteBoundary_RecreatesAllOwners_AndFinishesOnce(int failAt, bool afterWrite)
        {
            var disk = new Dictionary<string, string>();
            var first = new FeatureOwner(disk);
            int writes = 0;
            Action<string> fail = key =>
            {
                if (key == "feature" && ++writes == failAt) RewardTestStore.Fail(key);
            };
            if (afterWrite) first.Store.AfterSave = fail;
            else first.Store.BeforeSave = fail;
            Assert.That(first.Resume().Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));

            // No live wallet, item owner, feature state, coordinator or store is reused.
            var resumed = new FeatureOwner(disk);
            Assert.That(resumed.Resume().IsSuccess, Is.True);
            var verified = new FeatureOwner(disk);
            Assert.That(verified.State.Claimed, Is.True);
            Assert.That(verified.State.OriginalWindow, Is.EqualTo("2026-10-14"));
            Assert.That(verified.State.Journal.State, Is.EqualTo(RewardJournalState.Completed));
            Assert.That(verified.Wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(10));
            Assert.That(verified.Item.Balance, Is.EqualTo(3));
            Assert.That(verified.Store.Load<EconomySaveData>("economy").DeliveryReceipts.Count, Is.EqualTo(1));
            Assert.That(verified.Store.Load<ItemSave>("items").Receipts.Count, Is.EqualTo(1));
            Assert.That(verified.Resume().Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
        }

        [TestCase("economy", false)] [TestCase("items", false)]
        [TestCase("economy", true)] [TestCase("items", true)]
        public void EveryDestinationWriteBoundary_ReplaysDurableReceipt(string failedOwner, bool afterWrite)
        {
            var disk = new Dictionary<string, string>();
            var first = new FeatureOwner(disk);
            Action<string> fail = key => { if (key == failedOwner) RewardTestStore.Fail(key); };
            if (afterWrite) first.Store.AfterSave = fail;
            else first.Store.BeforeSave = fail;
            Assert.That(first.Resume().Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            var resumed = new FeatureOwner(disk);
            Assert.That(resumed.Resume().IsSuccess, Is.True);
            var verified = new FeatureOwner(disk);
            Assert.That(verified.Wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(10));
            Assert.That(verified.Item.Balance, Is.EqualTo(3));
            Assert.That(verified.State.Claimed, Is.True);
        }

        [Test]
        public void FailedAcknowledgement_RetryUsesAlreadyDelivered_NotAnotherOwnerMutation()
        {
            var disk = new Dictionary<string, string>();
            var first = new FeatureOwner(disk);
            int featureWrites = 0;
            first.Store.BeforeSave = key =>
            {
                if (key == "feature" && ++featureWrites == 3) RewardTestStore.Fail(key);
            };
            Assert.That(first.Resume().Status, Is.EqualTo(RewardDeliveryStatus.Pending));
            Assert.That(first.Coordinator.Journal.State, Is.EqualTo(RewardJournalState.Resolved));
            Assert.That(first.State.Journal.Entries[0].Acknowledged, Is.False);
            var resumed = new FeatureOwner(disk);
            int currencyWrites = 0;
            resumed.Store.BeforeSave = key => { if (key == "economy") currencyWrites++; };
            Assert.That(resumed.Resume().IsSuccess, Is.True);
            Assert.That(currencyWrites, Is.Zero);
            Assert.That(new FeatureOwner(disk).Item.Balance, Is.EqualTo(3));
        }

        [Test]
        public void FailedFinalCommit_RetainsAwaitingJournal_AndDoesNotDispatchOnRetry()
        {
            var disk = new Dictionary<string, string>();
            var first = new FeatureOwner(disk);
            int writes = 0;
            first.Store.BeforeSave = key =>
            {
                if (key == "feature" && ++writes == 5) RewardTestStore.Fail(key);
            };
            Assert.That(first.Resume().Status, Is.EqualTo(RewardDeliveryStatus.Pending));
            Assert.That(first.State.Claimed, Is.False);
            Assert.That(first.Coordinator.Journal.State, Is.EqualTo(RewardJournalState.DeliveredAwaitingClaimCommit));
            var resumed = new FeatureOwner(disk);
            resumed.Store.BeforeSave = key =>
            {
                if (key != "feature") Assert.Fail("Finalization must not write a destination.");
            };
            Assert.That(resumed.Resume().IsSuccess, Is.True);
            Assert.That(new FeatureOwner(disk).State.Claimed, Is.True);
        }

        [Test]
        public void IntentWithoutResolverResponse_GrantsNothing_AndResolutionIsFrozenOnceSaved()
        {
            var disk = new Dictionary<string, string>();
            var first = new FeatureOwner(disk);
            Assert.That(first.Begin().IsSuccess, Is.True);
            Assert.That(first.Coordinator.Deliver(first.Persist).Reason, Is.EqualTo(RewardDeliveryReason.ResolutionRequired));
            Assert.That(first.Coordinator.Complete(first.PersistClaimed).Reason, Is.EqualTo(RewardDeliveryReason.InvalidTransition));
            var resumed = new FeatureOwner(disk);
            Assert.That(resumed.Coordinator.Resolve(Bundle, resumed.Persist).IsSuccess, Is.True);
            resumed = new FeatureOwner(disk);
            Assert.That(resumed.Coordinator.Resolve(RewardContractTests.Bundle(RewardContractTests.Coin(99)), resumed.Persist).Reason,
                Is.EqualTo(RewardDeliveryReason.PayloadMismatch));
            Assert.That(resumed.Coordinator.Begin("another-request", "fp", resumed.Persist).Reason, Is.EqualTo(RewardDeliveryReason.PayloadMismatch));
            Assert.That(resumed.Coordinator.Begin(FeatureOwner.RequestId, "different-payload", resumed.Persist).Reason,
                Is.EqualTo(RewardDeliveryReason.PayloadMismatch));
            Assert.That(resumed.Resume().IsSuccess, Is.True);
            Assert.That(new FeatureOwner(disk).Wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(10));
        }

        [Test]
        public void InvalidLaterTarget_PreventsEarlierCurrencyDelivery()
        {
            var feature = new FeatureOwner(new Dictionary<string, string>());
            feature.Begin();
            var invalid = RewardContractTests.Bundle(RewardContractTests.Coin(), new ResolvedRewardEntry("item", "item", "unknown", 1));
            Assert.That(feature.Coordinator.Resolve(invalid, feature.Persist).Reason, Is.EqualTo(RewardDeliveryReason.UnknownTarget));
            Assert.That(feature.Wallet.GetBalance(new ResourceId("currency.coin")), Is.Zero);
            Assert.That(feature.Item.Balance, Is.Zero);
            Assert.That(feature.Coordinator.Journal.State, Is.EqualTo(RewardJournalState.IntentPersisted));
        }

        [Test]
        public void Coordinator_CallbackReentry_IsBusy_AndPublishedSnapshotsAreDetached()
        {
            var feature = new FeatureOwner(new Dictionary<string, string>());
            feature.Store.BeforeSave = _ =>
            {
                Assert.That(feature.Coordinator.Deliver(feature.Persist).Reason, Is.EqualTo(RewardDeliveryReason.Busy));
                Assert.That(feature.Coordinator.Begin("x", "y", feature.Persist).Reason, Is.EqualTo(RewardDeliveryReason.Busy));
            };
            Assert.That(feature.Resume().IsSuccess, Is.True);
            RewardJournal detached = feature.Coordinator.Journal;
            detached.Entries[0].Amount = 999;
            Assert.That(feature.Coordinator.Journal.Entries[0].Amount, Is.EqualTo(10));
            var reconstructed = JsonConvert.DeserializeObject<FeatureSave>(JsonConvert.SerializeObject(feature.State));
            Assert.That(reconstructed.Journal.IsValid(), Is.True);
            Assert.That(reconstructed.Journal.GetResolvedBundle().Fingerprint, Is.EqualTo(Bundle.Fingerprint));
        }

        [Test]
        public void CorruptJournal_IsRejectedWithoutDispatch()
        {
            var feature = new FeatureOwner(new Dictionary<string, string>());
            feature.Begin();
            feature.Coordinator.Resolve(Bundle, feature.Persist);
            RewardJournal corrupted = feature.Coordinator.Journal;
            corrupted.Entries[0].Amount++;
            var coordinator = new RewardDeliveryCoordinator(feature.Gateway, corrupted);
            Assert.That(coordinator.Deliver(feature.Persist).Reason, Is.EqualTo(RewardDeliveryReason.InvalidJournal));
            Assert.That(feature.Wallet.GetBalance(new ResourceId("currency.coin")), Is.Zero);
        }

        [Test]
        public void RemotePending_DoesNotAcknowledgeOrReplaceRequestIdentity()
        {
            var gateway = new PendingGateway();
            RewardJournal saved = null;
            var coordinator = new RewardDeliveryCoordinator(gateway);
            Action<RewardJournal> persist = value => saved = JsonConvert.DeserializeObject<RewardJournal>(JsonConvert.SerializeObject(value));
            coordinator.Begin("stable", "fp", persist);
            coordinator.Resolve(RewardContractTests.Bundle(RewardContractTests.Coin()), persist);
            Assert.That(coordinator.Deliver(persist).Status, Is.EqualTo(RewardDeliveryStatus.Pending));
            Assert.That(saved.Entries[0].Acknowledged, Is.False);
            string identity = gateway.LastId;
            coordinator = new RewardDeliveryCoordinator(gateway, saved);
            coordinator.Deliver(persist);
            Assert.That(gateway.LastId, Is.EqualTo(identity));
        }

        [Serializable]
        public sealed class FeatureSave : SaveData
        {
            public string OriginalWindow = "2026-10-14";
            public bool Claimed;
            public RewardJournal Journal;
        }

        [Serializable]
        public sealed class ItemSave : SaveData
        {
            public long Balance;
            public List<RewardReceiptSaveEntry> Receipts = new();
        }

        private sealed class FeatureOwner
        {
            public static readonly string RequestId = RewardIdentity.Request("profile", "daily", "schedule", "cycle-1", "day-1");
            private static readonly string RequestFingerprint = RewardIdentity.Fingerprint("configured", "revision-1", "day-1");
            public readonly RewardTestStore Store;
            public readonly DatasaveResourceWallet Wallet;
            public readonly ItemOwner Item;
            public readonly RewardDeliveryGateway Gateway;
            public readonly RewardDeliveryCoordinator Coordinator;
            public FeatureSave State;

            public FeatureOwner(Dictionary<string, string> disk)
            {
                Store = new RewardTestStore(disk);
                Wallet = new DatasaveResourceWallet(Store);
                Item = new ItemOwner(Store);
                State = Store.Load<FeatureSave>("feature");
                Gateway = new RewardDeliveryGateway(new IRewardDestination[] { Wallet, Item });
                Coordinator = new RewardDeliveryCoordinator(Gateway, State.Journal);
            }

            public RewardDeliveryResult Begin() => Coordinator.Begin(RequestId, RequestFingerprint, Persist);
            public void Persist(RewardJournal journal) => Save(journal, false);
            public void PersistClaimed(RewardJournal journal) => Save(journal, true);

            private void Save(RewardJournal journal, bool claimed)
            {
                var candidate = new FeatureSave
                {
                    OriginalWindow = State.OriginalWindow,
                    Claimed = claimed || State.Claimed,
                    Journal = journal.Clone()
                };
                Store.Save(candidate, "feature");
                State = candidate;
            }

            public RewardDeliveryResult Resume()
            {
                RewardDeliveryResult result = Begin();
                if (!result.IsSuccess) return result;
                if (Coordinator.Journal.State == RewardJournalState.IntentPersisted)
                {
                    result = Coordinator.Resolve(Bundle, Persist);
                    if (!result.IsSuccess) return result;
                }
                result = Coordinator.Deliver(Persist);
                return result.IsSuccess ? Coordinator.Complete(PersistClaimed) : result;
            }
        }

        private sealed class ItemOwner : IRewardDestination
        {
            private readonly RewardTestStore store;
            private ItemSave state;
            public ItemOwner(RewardTestStore store) { this.store = store; state = store.Load<ItemSave>("items"); }
            public string KindId => "item";
            public long Balance => state.Balance;

            public RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests)
            {
                long next = state.Balance;
                foreach (RewardDeliveryRequest request in requests)
                {
                    if (!request.IsValid) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
                    if (request.Entry.TargetId != "unit.scout") return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownTarget);
                    RewardReceiptSaveEntry receipt = state.Receipts.Find(r => r.TransactionId == request.TransactionId);
                    if (receipt != null)
                    {
                        if (receipt.Fingerprint != request.Fingerprint) return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                        continue;
                    }
                    try { next = checked(next + request.Entry.Amount); }
                    catch (OverflowException) { return RewardDeliveryResult.Failed(RewardDeliveryReason.Overflow); }
                }
                return RewardDeliveryResult.Delivered();
            }

            public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
            {
                RewardDeliveryResult validation = Validate(new[] { request });
                if (!validation.IsSuccess) return validation;
                if (state.Receipts.Exists(r => r.TransactionId == request.TransactionId)) return RewardDeliveryResult.AlreadyDelivered();
                var candidate = JsonConvert.DeserializeObject<ItemSave>(JsonConvert.SerializeObject(state));
                candidate.Balance = checked(candidate.Balance + request.Entry.Amount);
                candidate.Receipts.Add(new RewardReceiptSaveEntry { TransactionId = request.TransactionId, Fingerprint = request.Fingerprint });
                try { store.Save(candidate, "items"); }
                catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed); }
                state = candidate;
                return RewardDeliveryResult.Delivered();
            }
        }

        private sealed class PendingGateway : IRewardDeliveryGateway
        {
            public string LastId;
            public RewardDeliveryResult Validate(string requestId, string fingerprint, ResolvedRewardBundle bundle) => RewardDeliveryResult.Delivered();
            public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
            {
                LastId = request.TransactionId;
                return RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable);
            }
        }
    }
}
