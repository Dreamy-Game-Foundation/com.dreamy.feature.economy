using System;
using System.IO;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;
using NUnit.Framework;

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
