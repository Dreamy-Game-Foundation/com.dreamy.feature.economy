using System;
using System.IO;
using Dreamy.Economy.Rewards;
using NUnit.Framework;

namespace Dreamy.Economy.Tests
{
    public sealed class DatasaveResourceWalletTests
    {
        private static readonly ResourceId Coin = new("currency.coin");
        private static readonly ResourceId Gem = new("currency.gem");

        [Test]
        public void LegacyGrant_WriteFailure_DoesNotMutateCacheBalanceReceiptOrEvent()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store);
            var loaded = (EconomySaveData)store.Cache["economy"];
            int events = 0;
            wallet.BalanceChanged += _ => events++;
            store.BeforeSave = RewardTestStore.Fail;
            var grant = new ResourceGrantRequest("old", new ResourceAmount(Coin, 10));
            Assert.Throws<IOException>(() => wallet.TryGrant(grant));
            Assert.That(wallet.GetBalance(Coin), Is.Zero);
            Assert.That(loaded.Balances, Is.Empty);
            Assert.That(loaded.TransactionIds, Is.Empty);
            Assert.That(events, Is.Zero);
            store.BeforeSave = null;
            store.SaveAll();
            var recreated = new DatasaveResourceWallet(new RewardTestStore(store.Disk));
            Assert.That(recreated.GetBalance(Coin), Is.Zero);
            Assert.That(wallet.TryGrant(grant), Is.True);
            recreated = new DatasaveResourceWallet(new RewardTestStore(store.Disk));
            Assert.That(recreated.TryGrant(grant), Is.True);
            Assert.That(recreated.GetBalance(Coin), Is.EqualTo(10));
        }

        [Test]
        public void StrictGrant_FailedSaveRetriesAgainstCachedState_ThenRecreates()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store);
            var request = RewardContractTests.Request("strict");
            int events = 0;
            wallet.BalanceChanged += _ => events++;
            store.BeforeSave = RewardTestStore.Fail;
            Assert.That(wallet.Deliver(request).Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            Assert.That(wallet.GetBalance(Coin), Is.Zero);
            Assert.That(((EconomySaveData)store.Cache["economy"]).DeliveryReceipts, Is.Empty);
            Assert.That(events, Is.Zero);
            store.BeforeSave = null;
            store.SaveAll();
            Assert.That(wallet.Deliver(request).Status, Is.EqualTo(RewardDeliveryStatus.Delivered));
            Assert.That(events, Is.EqualTo(1));
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk));
            Assert.That(wallet.Deliver(request).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(wallet.Deliver(RewardContractTests.Request("strict", 11)).Reason, Is.EqualTo(RewardDeliveryReason.PayloadMismatch));
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(10));
        }

        [Test]
        public void MigratedLegacyTombstone_RemainsStrictlyRejectedAfterLegacyEviction()
        {
            var store = new RewardTestStore();
            var legacy = new EconomySaveData();
            legacy.TransactionIds.Add("old");
            legacy.Balances.Add(new ResourceBalanceSaveEntry { ResourceId = Coin.Value, Amount = 10 });
            store.Save(legacy, "economy");
            var wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk), transactionHistoryLimit: 1);
            wallet.TryGrant(new ResourceGrantRequest("new", new ResourceAmount(Coin, 1)));
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk), transactionHistoryLimit: 1);
            Assert.That(wallet.Deliver(RewardContractTests.Request("old")).Reason, Is.EqualTo(RewardDeliveryReason.LegacyReceiptUnverifiable));
            // Legacy replay remains explicitly bounded even though strict collisions cannot regrant.
            Assert.That(wallet.TryGrant(new ResourceGrantRequest("old", new ResourceAmount(Coin, 1))), Is.True);
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(12));
        }

        [Test]
        public void Exchange_InvalidLaterRewardOverflowAndSaveFailure_AreAtomic()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[]
            {
                new ResourceAmount(Coin, 100), new ResourceAmount(Gem, long.MaxValue)
            });
            var cost = new ResourceAmount(Coin, 10);
            Assert.That(wallet.TryExchange(new ResourceExchangeRequest("invalid", cost,
                new[] { new ResourceAmount(Coin, 2), default })), Is.False);
            Assert.That(wallet.TryExchange(new ResourceExchangeRequest("overflow", cost,
                new[] { new ResourceAmount(Coin, 2), new ResourceAmount(Gem, 1) })), Is.False);
            var valid = new ResourceExchangeRequest("valid", cost, new[] { new ResourceAmount(Coin, 2) });
            store.BeforeSave = RewardTestStore.Fail;
            Assert.Throws<IOException>(() => wallet.TryExchange(valid));
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(100));
            store.BeforeSave = null;
            store.SaveAll();
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk));
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(100));
            Assert.That(wallet.TryExchange(valid), Is.True);
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk));
            Assert.That(wallet.TryExchange(valid), Is.True);
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(92));
            Assert.That(wallet.GetBalance(Gem), Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void StrictLedger_RetainsMoreThan4096Receipts_SeparatelyFromLegacyLimit()
        {
            var store = new RewardTestStore();
            var seed = new EconomySaveData();
            // A persisted 4096-receipt fixture avoids quadratic JSON writes while testing
            // the actual retention boundary with two new committed deliveries.
            for (int i = 0; i < 4096; i++)
            {
                RewardDeliveryRequest request = RewardContractTests.Request("strict-" + i, 1);
                seed.DeliveryReceipts.Add(new RewardReceiptSaveEntry { TransactionId = request.TransactionId, Fingerprint = request.Fingerprint });
            }
            seed.Balances.Add(new ResourceBalanceSaveEntry { ResourceId = Coin.Value, Amount = 4096 });
            store.Save(seed, "economy");
            var wallet = new DatasaveResourceWallet(store, transactionHistoryLimit: 2);
            for (int i = 4096; i < 4098; i++) Assert.That(wallet.Deliver(RewardContractTests.Request("strict-" + i, 1)).IsSuccess, Is.True);
            foreach (string id in new[] { "one", "two", "three" })
                wallet.TryGrant(new ResourceGrantRequest(id, new ResourceAmount(Coin, 1)));
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk), transactionHistoryLimit: 2);
            Assert.That(wallet.Deliver(RewardContractTests.Request("strict-0", 1)).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(store.Load<EconomySaveData>("economy").DeliveryReceipts.Count, Is.EqualTo(4098));
            Assert.That(store.Load<EconomySaveData>("economy").TransactionIds, Is.EqualTo(new[] { "two", "three" }));
            Assert.That(wallet.TryGrant(new ResourceGrantRequest("one", new ResourceAmount(Coin, 1))), Is.True);
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(4102));
        }

        [Test]
        public void LegacyIdOnlyReceipt_CannotProveStrictPayloadEquality()
        {
            var store = new RewardTestStore();
            new DatasaveResourceWallet(store).TryGrant(new ResourceGrantRequest("old", new ResourceAmount(Coin, 10)));
            var wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk));
            Assert.That(wallet.Deliver(RewardContractTests.Request("old")).Reason, Is.EqualTo(RewardDeliveryReason.LegacyReceiptUnverifiable));
            Assert.That(wallet.TryGrant(new ResourceGrantRequest("old", new ResourceAmount(Coin, 99))), Is.True);
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(10));
        }

        [Test]
        public void Owner_ReentryDuringSaveAndEvent_IsBusy()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store);
            var request = RewardContractTests.Request("first");
            store.BeforeSave = _ =>
            {
                Assert.That(wallet.Deliver(request).Reason, Is.EqualTo(RewardDeliveryReason.Busy));
                Assert.That(wallet.TryGrant(new ResourceGrantRequest("legacy", new ResourceAmount(Coin, 1))), Is.False);
            };
            wallet.BalanceChanged += _ => Assert.That(wallet.Deliver(request).Reason, Is.EqualTo(RewardDeliveryReason.Busy));
            Assert.That(wallet.Deliver(request).IsSuccess, Is.True);
            wallet = new DatasaveResourceWallet(new RewardTestStore(store.Disk));
            Assert.That(wallet.GetBalance(Coin), Is.EqualTo(10));
        }

        [Test]
        public void DefaultRequestsAndGrantOverflow_DoNotConsumeTransactions()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store, initialBalances: new[] { new ResourceAmount(Coin, long.MaxValue) });
            Assert.That(wallet.TryGrant(default), Is.False);
            Assert.That(wallet.TryExchange(default), Is.False);
            Assert.That(wallet.TryGrant(new ResourceGrantRequest("bad", default)), Is.False);
            Assert.That(wallet.TryGrant(new ResourceGrantRequest("overflow", new ResourceAmount(Coin, 1))), Is.False);
            Assert.That(wallet.Deliver(RewardContractTests.Request("strict", 1)).Reason, Is.EqualTo(RewardDeliveryReason.Overflow));
            Assert.That(store.Load<EconomySaveData>("economy").TransactionIds, Is.Empty);
            Assert.That(new DatasaveResourceWallet(new RewardTestStore(store.Disk)).GetBalance(Coin), Is.EqualTo(long.MaxValue));
        }
    }
}
