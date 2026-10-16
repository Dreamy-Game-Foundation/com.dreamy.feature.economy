using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreamy.Economy.Rewards;
using NUnit.Framework;

namespace Dreamy.Economy.Tests
{
    public sealed class UnifiedEconomyTests
    {
        private static readonly ResourceRewardDefinition[] Definitions =
        {
            new("card", new ResourceId("card.knight")),
            new("chest", new ResourceId("chest.silver")),
            new("cosmetic", new ResourceId("skin.red"), unique: true)
        };
        private static DatasaveResourceWallet Wallet(RewardTestStore store) => new(store, rewardDefinitions: Definitions);
        private static RewardDeliveryRequest Request(string id, string kind, string target, long amount = 1) =>
            new(id, "request-fingerprint", "bundle-fingerprint", new ResolvedRewardEntry(id, kind, target, amount));

        [Test]
        public void EveryKind_UsesOneDocument_InterleavedWritesPreserveAllBalancesAndReceipts()
        {
            var store = new RewardTestStore(); var wallet = Wallet(store);
            Assert.That(wallet.Deliver(Request("coin", "currency", "currency.coin", 100)).IsSuccess, Is.True);
            Assert.That(wallet.CreateRewardDestination("card").Deliver(Request("card", "card", "card.knight", 2)).IsSuccess, Is.True);
            Assert.That(wallet.Deliver(Request("gem", "currency", "currency.gem", 5)).IsSuccess, Is.True);
            wallet.CreateRewardDestination("chest").Deliver(Request("chest", "chest", "chest.silver"));
            wallet.CreateRewardDestination("cosmetic").Deliver(Request("skin", "cosmetic", "skin.red"));
            Assert.That(store.Disk.Keys, Is.EquivalentTo(new[] { "economy" }));
            var saved = store.Load<EconomySaveData>("economy");
            Assert.That(saved.Balances.Count, Is.EqualTo(5));
            Assert.That(saved.DeliveryReceipts.Count, Is.EqualTo(5));
            var restart = Wallet(new RewardTestStore(store.Disk));
            Assert.That(restart.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(100));
            Assert.That(restart.GetBalance(new ResourceId("card.knight")), Is.EqualTo(2));
            Assert.That(restart.CreateRewardDestination("card").Deliver(Request("card", "card", "card.knight", 2)).Status,
                Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
        }

        [Test]
        public void UniqueResource_IsOwnedOnce_ButKeepsReceiptForEveryEntitlement()
        {
            var store = new RewardTestStore(); var wallet = Wallet(store);
            var destination = wallet.CreateRewardDestination("cosmetic");
            destination.Deliver(Request("skin1", "cosmetic", "skin.red", long.MaxValue));
            destination.Deliver(Request("skin2", "cosmetic", "skin.red"));
            Assert.That(wallet.GetBalance(new ResourceId("skin.red")), Is.EqualTo(1));
            Assert.That(store.Load<EconomySaveData>("economy").DeliveryReceipts.Count, Is.EqualTo(2));
        }

        [Test]
        public void RoutingAndFingerprintMismatch_DoNotMutate()
        {
            var store = new RewardTestStore(); var wallet = Wallet(store);
            var card = wallet.CreateRewardDestination("card");
            Assert.That(card.Deliver(Request("bad", "chest", "chest.silver")).Reason, Is.EqualTo(RewardDeliveryReason.UnknownKind));
            Assert.That(card.Deliver(Request("bad", "card", "card.missing")).Reason, Is.EqualTo(RewardDeliveryReason.UnknownTarget));
            card.Deliver(Request("ok", "card", "card.knight"));
            Assert.That(card.Deliver(Request("ok", "card", "card.knight", 2)).Reason, Is.EqualTo(RewardDeliveryReason.PayloadMismatch));
            Assert.That(wallet.GetBalance(new ResourceId("card.knight")), Is.EqualTo(1));
        }

        [Test]
        public void CombinedOverflow_IsRejectedBeforeAnyDelivery()
        {
            var wallet = Wallet(new RewardTestStore());
            var card = wallet.CreateRewardDestination("card");
            Assert.That(card.Validate(new[] { Request("1", "card", "card.knight", long.MaxValue), Request("2", "card", "card.knight") }).Reason,
                Is.EqualTo(RewardDeliveryReason.Overflow));
            Assert.That(wallet.GetBalance(new ResourceId("card.knight")), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedOwnerWrite_FreezesEveryKindUntilReload_ThenReplaysOnce(bool afterWrite)
        {
            var store = new RewardTestStore(); var wallet = Wallet(store); var card = wallet.CreateRewardDestination("card");
            Action<string> fail = _ => throw new IOException("owner write");
            if (afterWrite) store.AfterSave = fail; else store.BeforeSave = fail;
            var request = Request("card1", "card", "card.knight", 2);
            Assert.That(card.Deliver(request).Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            Assert.That(wallet.Deliver(Request("coin", "currency", "currency.coin")).Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            store.BeforeSave = null; store.AfterSave = null;
            wallet.Reload();
            Assert.That(card.Deliver(request).IsSuccess, Is.True);
            Assert.That(wallet.GetBalance(new ResourceId("card.knight")), Is.EqualTo(2));
        }

        [Test]
        public void ImportLegacyInventory_PreservesCurrency_AndOriginalReceiptBlocksReplay()
        {
            var store = new RewardTestStore(); var wallet = Wallet(store);
            wallet.TryGrant(new ResourceGrantRequest("old-coin", new ResourceAmount(new ResourceId("currency.coin"), 50)));
            var request = Request("legacy-card-entry", "card", "card.knight", 2);
            var amounts = new[] { new ResourceAmount(new ResourceId("card.knight"), 2) };
            var receipts = new[] { new RewardReceiptSaveEntry { TransactionId = request.TransactionId, Fingerprint = request.Fingerprint } };
            Assert.That(wallet.ImportInventory("legacy-card-v1", amounts, receipts).IsSuccess, Is.True);
            Assert.That(wallet.ImportInventory("legacy-card-v1", amounts, receipts).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(wallet.CreateRewardDestination("card").Deliver(request).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(wallet.GetBalance(new ResourceId("card.knight")), Is.EqualTo(2));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(50));
            var restarted = Wallet(new RewardTestStore(store.Disk));
            Assert.That(restarted.ImportInventory("legacy-card-v1", amounts, receipts).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(restarted.ImportInventory("legacy-card-v1", new[] { new ResourceAmount(new ResourceId("card.knight"), 3) }, receipts).Reason,
                Is.EqualTo(RewardDeliveryReason.PayloadMismatch));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImportFailure_ReloadAndRetry_DoesNotDoubleCount(bool afterWrite)
        {
            var store = new RewardTestStore(); var wallet = Wallet(store);
            Action<string> fail = _ => throw new IOException("import write");
            if (afterWrite) store.AfterSave = fail; else store.BeforeSave = fail;
            var amounts = new[] { new ResourceAmount(new ResourceId("chest.silver"), 3) };
            Assert.That(wallet.ImportInventory("legacy-chests", amounts, Array.Empty<RewardReceiptSaveEntry>()).Reason,
                Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            store.BeforeSave = null; store.AfterSave = null; wallet.Reload();
            Assert.That(wallet.ImportInventory("legacy-chests", amounts, Array.Empty<RewardReceiptSaveEntry>()).IsSuccess, Is.True);
            Assert.That(wallet.GetBalance(new ResourceId("chest.silver")), Is.EqualTo(3));
        }

        [Test]
        public void DifferentImportsWithOverlappingReceipts_AreBlocked()
        {
            var wallet = Wallet(new RewardTestStore());
            var amounts = new[] { new ResourceAmount(new ResourceId("card.knight"), 2) };
            var receipts = new[] { new RewardReceiptSaveEntry { TransactionId = "receipt", Fingerprint = "fingerprint" } };
            wallet.ImportInventory("source-1", amounts, receipts);
            Assert.That(wallet.ImportInventory("source-2", amounts, receipts).Reason, Is.EqualTo(RewardDeliveryReason.PayloadMismatch));
            Assert.That(wallet.GetBalance(new ResourceId("card.knight")), Is.EqualTo(2));
        }

        [Test]
        public void LegacySpendAndExchange_PreserveInventoryAndImportMarkers()
        {
            var store = new RewardTestStore(); var wallet = Wallet(store);
            wallet.ImportInventory("cards", new[] { new ResourceAmount(new ResourceId("card.knight"), 4) }, Array.Empty<RewardReceiptSaveEntry>());
            wallet.TryGrant(new ResourceGrantRequest("coin", new ResourceAmount(new ResourceId("currency.coin"), 100)));
            Assert.That(wallet.Spend(new ResourceSpendRequest("buy", new[] { new ResourceAmount(new ResourceId("currency.coin"), 20) }, "shop")).IsSuccess, Is.True);
            Assert.That(wallet.GetBalance(new ResourceId("card.knight")), Is.EqualTo(4));
            Assert.That(store.Load<EconomySaveData>("economy").InventoryImports.Count, Is.EqualTo(1));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(80));
        }

        [Test]
        public void ListenerException_CannotInvalidateDurableItemDelivery()
        {
            var wallet = Wallet(new RewardTestStore());wallet.BalanceChanged += _ => throw new Exception("UI");
            Assert.That(wallet.CreateRewardDestination("card").Deliver(Request("item", "card", "card.knight")).IsSuccess, Is.True);
            Assert.That(wallet.GetBalance(new ResourceId("card.knight")), Is.EqualTo(1));
        }
    }
}
