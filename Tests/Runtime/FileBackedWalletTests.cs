using System;
using System.IO;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Dreamy.Economy.Tests
{
    public sealed class FileBackedWalletTests
    {
        private string directory;

        [SetUp]
        public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "DreamyEconomy-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private DatasaveService Service(ISaveCodec codec = null) => new(new DatasaveOptions
        {
            DirectoryName = directory, CreateFileOnFirstLoad = false, Codec = codec ?? new PlainTextSaveCodec()
        });

        [Test]
        public void V3Envelope_UpgradesToV4_AndInterleavedInventoryWritesPreserveCurrencyAndReceipts()
        {
            var service = Service();
            var seed = new DatasaveResourceWallet(service);
            seed.Deliver(RewardContractTests.Request("before-upgrade"));
            string path = Path.Combine(directory, "economy.json");
            var envelope = JObject.Parse(File.ReadAllText(path));
            envelope["DataVersion"] = 3;
            var payload = JObject.Parse((string)envelope["Payload"]);
            payload.Remove("InventoryImports");
            envelope["Payload"] = payload.ToString(Newtonsoft.Json.Formatting.None);
            File.WriteAllText(path, envelope.ToString());
            var wallet = new DatasaveResourceWallet(Service(), rewardDefinitions: new[]
            { new ResourceRewardDefinition("card", new ResourceId("card.knight")) });
            var card = new RewardDeliveryRequest("new-card", "request", "bundle",
                new ResolvedRewardEntry("card", "card", "card.knight", 2));
            Assert.That(wallet.CreateRewardDestination("card").Deliver(card).IsSuccess, Is.True);
            Assert.That(wallet.Deliver(RewardContractTests.Request("before-upgrade")).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(10));
            Assert.That(wallet.GetBalance(new ResourceId("card.knight")), Is.EqualTo(2));
            Assert.That((int)JObject.Parse(File.ReadAllText(path))["DataVersion"], Is.EqualTo(4));
            Assert.That(Service().Load<EconomySaveData>("economy").DeliveryReceipts.Count, Is.EqualTo(2));
        }

        [Test]
        public void RealDataSave_CodecFailureDoesNotPoisonSaveAllCache_AndReceiptSurvivesRecreation()
        {
            var codec = new FailingCodec();
            var service = Service(codec);
            var wallet = new DatasaveResourceWallet(service);
            RewardDeliveryRequest request = RewardContractTests.Request("file-backed");
            codec.Fail = true;
            Assert.That(wallet.Deliver(request).Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.Zero);
            codec.Fail = false;
            service.SaveAll();
            wallet = new DatasaveResourceWallet(Service());
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.Zero);
            Assert.That(wallet.Deliver(request).Status, Is.EqualTo(RewardDeliveryStatus.Delivered));
            wallet = new DatasaveResourceWallet(Service());
            Assert.That(wallet.Deliver(request).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(10));
        }

        [Test]
        public void RealDataSave_TempWriteFailureLeavesCommittedOwnerAndCacheUntouched()
        {
            var service = Service();
            var wallet = new DatasaveResourceWallet(service, initialBalances: new[]
            {
                new ResourceAmount(new ResourceId("currency.coin"), 5)
            });
            string tempPath = Path.Combine(directory, "economy.json.tmp");
            Directory.CreateDirectory(tempPath);
            RewardDeliveryRequest request = RewardContractTests.Request("file-failure");
            Assert.That(wallet.Deliver(request).Reason, Is.EqualTo(RewardDeliveryReason.PersistenceFailed));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(5));
            Directory.Delete(tempPath);
            service.SaveAll();
            wallet = new DatasaveResourceWallet(Service());
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(5));
            Assert.That(wallet.Deliver(request).IsSuccess, Is.True);
            wallet = new DatasaveResourceWallet(Service());
            Assert.That(wallet.Deliver(request).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(15));
        }

        [Test]
        public void RealDataSave_MissingMainRestoresWholeOwnerSnapshotFromBackup()
        {
            var service = Service();
            var wallet = new DatasaveResourceWallet(service);
            RewardDeliveryRequest request = RewardContractTests.Request("retained");
            wallet.Deliver(request);
            // Save the same committed generation to create an equivalent backup.
            service.SaveAll();
            File.Delete(Path.Combine(directory, "economy.json"));
            wallet = new DatasaveResourceWallet(Service());
            Assert.That(wallet.Deliver(request).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.EqualTo(10));
        }

        [Test]
        public void TypedSpend_FileWriteFailureReconcilesAndReplaySurvivesRestart()
        {
            var coin = new ResourceId("currency.coin");
            var wallet = new DatasaveResourceWallet(Service(), initialBalances: new[] { new ResourceAmount(coin, 10) });
            var request = new ResourceSpendRequest("spend-file", new[] { new ResourceAmount(coin, 5) });
            string temporary = Path.Combine(directory, "economy.json.tmp");
            Directory.CreateDirectory(temporary);
            Assert.That(wallet.Spend(request).Status, Is.EqualTo(ResourceTransactionStatus.PersistenceFailed));
            Assert.That(wallet.GetBalance(coin), Is.EqualTo(10));
            Directory.Delete(temporary);
            Assert.That(wallet.Spend(request).Status, Is.EqualTo(ResourceTransactionStatus.Committed));
            wallet = new DatasaveResourceWallet(Service());
            Assert.That(wallet.Spend(request).Status, Is.EqualTo(ResourceTransactionStatus.AlreadyCommitted));
            Assert.That(wallet.GetBalance(coin), Is.EqualTo(5));
        }

        [Test]
        public void Version2Envelope_MigratesWithoutLosingBalancesOrOldReceiptNamespaces()
        {
            var coin = new ResourceId("currency.coin");
            var wallet = new DatasaveResourceWallet(Service(), initialBalances: new[] { new ResourceAmount(coin, 10) });
            wallet.TryGrant(new ResourceGrantRequest("legacy-v2", new ResourceAmount(coin, 1)));
            wallet.Deliver(RewardContractTests.Request("strict-v2", 10));
            string path = Path.Combine(directory, "economy.json");
            var envelope = JObject.Parse(File.ReadAllText(path));
            var payload = JObject.Parse((string)envelope["Payload"]);
            payload.Remove("TransactionReceipts");
            envelope["Payload"] = payload.ToString();
            envelope["DataVersion"] = 2;
            File.WriteAllText(path, envelope.ToString());
            wallet = new DatasaveResourceWallet(Service());
            Assert.That(wallet.GetBalance(coin), Is.EqualTo(21));
            Assert.That(wallet.Spend(new ResourceSpendRequest("legacy-v2", new[] { new ResourceAmount(coin, 1) })).Status,
                Is.EqualTo(ResourceTransactionStatus.LegacyReceiptUnverifiable));
            Assert.That(wallet.Deliver(RewardContractTests.Request("strict-v2", 10)).Status, Is.EqualTo(RewardDeliveryStatus.AlreadyDelivered));
            Assert.That(wallet.Spend(new ResourceSpendRequest("schema3", new[] { new ResourceAmount(coin, 1) })).IsNewCommit, Is.True);
            Assert.That((int)JObject.Parse(File.ReadAllText(path))["DataVersion"], Is.EqualTo(4));
        }

        private sealed class FailingCodec : ISaveCodec
        {
            public bool Fail;
            public string Encode(string text)
            {
                if (Fail) throw new IOException("Injected codec failure");
                return text;
            }
            public string Decode(string text) => text;
        }
    }
}
