using System;
using System.IO;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;
using NUnit.Framework;

namespace Dreamy.Economy.Tests
{
    public sealed class ResourceTransactionTests
    {
        private static readonly ResourceId Coin = new("currency.coin");
        private static readonly ResourceId Gem = new("currency.gem");
        private static ResourceAmount C(long value) => new(Coin, value);
        private static ResourceAmount G(long value) => new(Gem, value);

        [Test]
        public void MultiCost_DeficitsComeFromOneSnapshot_AndDoNotMutate()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(7) });
            int changes = 0;
            wallet.BalanceChanged += _ => changes++;
            var result = wallet.Spend(new ResourceSpendRequest("spend", new[] { C(4), G(2), C(6) }));
            Assert.That(result.Status, Is.EqualTo(ResourceTransactionStatus.InsufficientResources));
            Assert.That(result.Deficits.Count, Is.EqualTo(2));
            Assert.That(result.Deficits[0].Required, Is.EqualTo(10));
            Assert.That(result.Deficits[0].Available, Is.EqualTo(7));
            Assert.That(result.Deficits[0].Missing, Is.EqualTo(3));
            Assert.That(result.Deficits[1].Available, Is.Zero);
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(7));
            Assert.That(store.Load<EconomySaveData>("economy").TransactionReceipts, Is.Empty);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void CanonicalReceipt_ReplaysAfterRestartBeforeBalanceCheck_AndRejectsChangedPayload()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(10), G(2) });
            Assert.That(wallet.Spend(new ResourceSpendRequest("id", new[] { C(4), G(2), C(6) }, "upgrade")).IsNewCommit, Is.True);
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk));
            Assert.That(wallet.Spend(new ResourceSpendRequest("id", new[] { G(2), C(10) }, "upgrade")).Status,
                Is.EqualTo(ResourceTransactionStatus.AlreadyCommitted));
            Assert.That(wallet.Spend(new ResourceSpendRequest("id", new[] { C(10), G(2) }, "other")).Status,
                Is.EqualTo(ResourceTransactionStatus.PayloadMismatch));
            Assert.That(wallet.GetBalance(Coin), Is.Zero);
            Assert.That(wallet.Exchange(new ResourceTransactionRequest("id", new[] { C(10) }, new[] { G(1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.PayloadMismatch));
        }

        [Test]
        public void InvalidAndOverflowRequests_DoNotDebitOrCreateReceipts()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(long.MaxValue), G(5) });
            Assert.That(wallet.Spend(default).Status, Is.EqualTo(ResourceTransactionStatus.InvalidRequest));
            Assert.That(wallet.Spend(new ResourceSpendRequest("bad", new ResourceAmount[] { default })).Status,
                Is.EqualTo(ResourceTransactionStatus.InvalidRequest));
            Assert.That(wallet.Spend(new ResourceSpendRequest("overflow", new[] { C(long.MaxValue), C(1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.Overflow));
            Assert.That(wallet.Exchange(new ResourceTransactionRequest("empty", new[] { G(1) }, Array.Empty<ResourceAmount>())).Status,
                Is.EqualTo(ResourceTransactionStatus.InvalidRequest));
            Assert.That(wallet.Exchange(new ResourceTransactionRequest("overflow-reward", new[] { G(1) }, new[] { C(1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.Overflow));
            Assert.That(wallet.GetBalance(Gem), Is.EqualTo(5));
            Assert.That(store.Load<EconomySaveData>("economy").TransactionReceipts, Is.Empty);
        }

        [Test]
        public void Exchange_SameResourceDebitsBeforeReward_AndNotifiesOnceAfterSave()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(long.MaxValue) });
            int events = 0;
            wallet.BalanceChanged += change =>
            {
                events++;
                Assert.That(store.Load<EconomySaveData>("economy").TransactionReceipts.Count, Is.EqualTo(1));
            };
            var result = wallet.Exchange(new ResourceTransactionRequest("same", new[] { C(10) }, new[] { C(2), C(3) }));
            Assert.That(result.IsNewCommit, Is.True);
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(long.MaxValue - 5));
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void SaveFailure_ReconcilesOldState_AndRetryCommitsOnlyOnce()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(10) });
            var request = new ResourceSpendRequest("failure", new[] { C(5) });
            int events = 0;
            wallet.BalanceChanged += _ => events++;
            store.BeforeSave = RewardTestStore.Fail;
            Assert.That(wallet.Spend(request).Status, Is.EqualTo(ResourceTransactionStatus.PersistenceFailed));
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(10));
            Assert.That(events, Is.Zero);
            store.BeforeSave = null;
            store.SaveAll();
            Assert.That(wallet.Spend(request).IsNewCommit, Is.True);
            Assert.That(wallet.Spend(request).Status, Is.EqualTo(ResourceTransactionStatus.AlreadyCommitted));
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void ExceptionAfterWrite_ReconcilesCommit_AndSubscriberExceptionsDoNotInvalidateIt()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(10) });
            int events = 0, failures = 0;
            wallet.BalanceChanged += _ => throw new Exception("listener");
            wallet.BalanceChanged += _ => events++;
            wallet.NotificationFailed += _ => failures++;
            store.AfterSave = _ => throw new IOException("after durable write");
            var request = new ResourceSpendRequest("saved", new[] { C(10) });
            Assert.That(wallet.Spend(request).Status, Is.EqualTo(ResourceTransactionStatus.Committed));
            Assert.That(wallet.GetBalance(Coin), Is.Zero);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(wallet.Spend(request).Status, Is.EqualTo(ResourceTransactionStatus.AlreadyCommitted));
        }

        [Test]
        public void Version2Fixture_MigratesTombstonesAndStrictReceipts_AndKeepsLegacyEvictionBehavior()
        {
            var store = new RewardTestStore();
            // Raw old-format payload deliberately has no TransactionReceipts field.
            store.Disk["economy"] = "{\"Balances\":[{\"ResourceId\":\"currency.coin\",\"Amount\":10}],\"TransactionIds\":[\"old\"],\"DeliveryReceipts\":[],\"LegacyDeliveryTombstones\":null}";
            var wallet = new DatasaveResourceWallet(store, transactionHistoryLimit: 1);
            wallet.TryGrant(new ResourceGrantRequest("new", C(1)));
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk), transactionHistoryLimit: 1);
            Assert.That(wallet.Spend(new ResourceSpendRequest("old", new[] { C(1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.LegacyReceiptUnverifiable));
            Assert.That(wallet.TryGrant(new ResourceGrantRequest("old", C(1))), Is.True);
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(12));
            Assert.That(wallet.Deliver(RewardContractTests.Request("strict")).IsSuccess, Is.True);
            Assert.That(wallet.Spend(new ResourceSpendRequest("strict", new[] { C(1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.PayloadMismatch));
            Assert.That(wallet.Spend(new ResourceSpendRequest("new-api", new[] { C(1) })).IsSuccess, Is.True);
            Assert.That(wallet.Deliver(RewardContractTests.Request("new-api")).Reason,
                Is.EqualTo(RewardDeliveryReason.PayloadMismatch));
            Assert.That(wallet.TryGrant(new ResourceGrantRequest("new-api", C(99))), Is.True);
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(21));
        }

        [Test]
        public void ReentryAndCorruptLedger_FailClosed()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(10) });
            var request = new ResourceSpendRequest("id", new[] { C(1) });
            store.BeforeSave = _ => Assert.That(wallet.Spend(request).Status, Is.EqualTo(ResourceTransactionStatus.Busy));
            Assert.That(wallet.Spend(request).IsSuccess, Is.True);
            store.BeforeSave = null;
            var data = store.Load<EconomySaveData>("economy");
            data.TransactionReceipts.Add(data.TransactionReceipts[0]);
            store.Save(data, "economy");
            Assert.Throws<InvalidOperationException>(() => new DatasaveResourceWallet(new RewardTestStore(store.Disk)));
        }

        private sealed class UncertainStore : IDatasaveService
        {
            public readonly RewardTestStore Inner = new();
            public bool FailLoad;
            public T Load<T>(string key = null) where T : SaveData, new() =>
                FailLoad ? throw new IOException("Cannot reconcile") : Inner.Load<T>(key);
            public void Save<T>(T value, string key = null) where T : SaveData => Inner.Save(value, key);
            public bool Exists(string key) => Inner.Exists(key);
            public void SaveAll() => Inner.SaveAll();
            public void Delete(string key) => Inner.Delete(key);
            public void DeleteAll() => Inner.DeleteAll();
        }

        [Test]
        public void UnreconcilableSave_QuarantinesEveryMutationUntilOwnerIsRecreated()
        {
            var store = new UncertainStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(10) });
            store.Inner.BeforeSave = RewardTestStore.Fail;
            store.FailLoad = true;
            Assert.That(wallet.Spend(new ResourceSpendRequest("uncertain", new[] { C(1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.PersistenceUncertain));
            store.Inner.BeforeSave = null;
            store.FailLoad = false;
            Assert.That(wallet.Spend(new ResourceSpendRequest("new", new[] { C(1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.PersistenceUncertain));
            Assert.That(wallet.TryGrant(new ResourceGrantRequest("grant", C(1))), Is.False);
            Assert.That(wallet.TryExchange(new ResourceExchangeRequest("exchange", C(1), new[] { G(1) })), Is.False);
            Assert.That(wallet.Deliver(RewardContractTests.Request("strict")).IsSuccess, Is.False);
            Assert.That(new DatasaveResourceWallet(store).GetBalance(Coin), Is.EqualTo(10));
        }

        [Test]
        public void StrictTransactionReceipts_AreRetainedBeyondLegacyHistoryLimit()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { C(3) }, transactionHistoryLimit: 1);
            Assert.That(wallet.Spend(new ResourceSpendRequest("first", new[] { C(1) })).IsNewCommit, Is.True);
            wallet.TryGrant(new ResourceGrantRequest("old1", G(1)));
            wallet.TryGrant(new ResourceGrantRequest("old2", G(1)));
            wallet.Spend(new ResourceSpendRequest("second", new[] { C(1) }));
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk), transactionHistoryLimit: 1);
            Assert.That(wallet.Spend(new ResourceSpendRequest("first", new[] { C(1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.AlreadyCommitted));
            Assert.That(store.Load<EconomySaveData>("economy").TransactionReceipts.Count, Is.EqualTo(2));
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(1));
        }

        [Test]
        public void RequestAndResult_AreDetachedFromCallerCollections()
        {
            var values = new[] { C(1) };
            var request = new ResourceSpendRequest("id", values);
            values[0] = C(99);
            Assert.That(request.Costs[0].Amount, Is.EqualTo(1));
            Assert.That(request.Costs, Is.Not.InstanceOf<ResourceAmount[]>());
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceAmount(Coin, 0));
            Assert.That(new ResourceDeficit(Coin, 1, 0).Missing, Is.EqualTo(1));
        }
    }
}
