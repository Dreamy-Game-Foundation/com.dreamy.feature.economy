using System;
using System.Collections.Generic;
using System.Globalization;
using Dreamy.Economy.Rewards;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Dreamy.Economy.Tests
{
    public sealed class RewardContractTests
    {
        internal static ResolvedRewardEntry Coin(long amount = 10, string id = "coins") =>
            new(id, "currency", "currency.coin", amount);
        internal static ResolvedRewardBundle Bundle(params ResolvedRewardEntry[] entries) => new(1, "fixed-v1", entries);
        internal static RewardDeliveryRequest Request(string id, long amount = 10)
        {
            ResolvedRewardBundle bundle = Bundle(Coin(amount));
            return new RewardDeliveryRequest(id, RewardIdentity.Fingerprint("configured", "v1"), bundle.Fingerprint, bundle.Entries[0]);
        }

        [Test]
        public void Bundle_IsImmutable_AndRoundTripsCurrencyAndConcreteItem()
        {
            var entries = new[] { Coin(), new ResolvedRewardEntry("card", "item", "unit.scout", 3) };
            ResolvedRewardBundle bundle = Bundle(entries);
            entries[0] = Coin(99);
            Assert.That(bundle.Entries[0].Amount, Is.EqualTo(10));
            Assert.Throws<NotSupportedException>(() => ((IList<ResolvedRewardEntry>)bundle.Entries)[0] = Coin(2));
            var restored = JsonConvert.DeserializeObject<ResolvedRewardBundle>(JsonConvert.SerializeObject(bundle));
            Assert.That(restored.Fingerprint, Is.EqualTo(bundle.Fingerprint));
            Assert.That(restored.Entries[1].TargetId, Is.EqualTo("unit.scout"));
        }

        [TestCase("", "currency", "currency.coin", 1)]
        [TestCase("entry", "", "currency.coin", 1)]
        [TestCase("entry", "currency", "", 1)]
        [TestCase("entry", "currency", "currency.coin", 0)]
        [TestCase("entry", "currency", "currency.coin", -1)]
        public void InvalidEntry_IsRejected(string entry, string kind, string target, long amount) =>
            Assert.Catch<ArgumentException>(() => new ResolvedRewardEntry(entry, kind, target, amount));

        [Test]
        public void Bundle_RejectsDefaultDuplicateEmptyAndUnknownSchema()
        {
            Assert.Throws<ArgumentException>(() => Bundle(default));
            Assert.Throws<ArgumentException>(() => Bundle(Coin(), Coin(2)));
            Assert.Throws<ArgumentException>(() => Bundle());
            Assert.Throws<ArgumentException>(() => new ResolvedRewardBundle(1, "", new[] { Coin() }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResolvedRewardBundle(2, "v1", new[] { Coin() }));
        }

        [Test]
        public void Identity_IsLengthPrefixed_AndFingerprintIsInvariantAndOrderSensitive()
        {
            Assert.That(RewardIdentity.Request("a:b", "c", "d", "e", "f"),
                Is.Not.EqualTo(RewardIdentity.Request("a", "b:c", "d", "e", "f")));
            Assert.That(RewardIdentity.EntryTransaction("a:b", "c"), Is.Not.EqualTo(RewardIdentity.EntryTransaction("a", "b:c")));
            string fingerprint = Bundle(Coin(), new ResolvedRewardEntry("item", "item", "unit.scout", 1)).Fingerprint;
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                Assert.That(Bundle(Coin(), new ResolvedRewardEntry("item", "item", "unit.scout", 1)).Fingerprint, Is.EqualTo(fingerprint));
            }
            finally { CultureInfo.CurrentCulture = previous; }
            Assert.That(Bundle(new ResolvedRewardEntry("item", "item", "unit.scout", 1), Coin()).Fingerprint, Is.Not.EqualTo(fingerprint));
            Assert.That(Bundle(Coin(11)).Fingerprint, Is.Not.EqualTo(Bundle(Coin()).Fingerprint));
        }

        [Test]
        public void WholeBundleValidation_RejectsMissingRouteAndCombinedOverflowBeforeGrant()
        {
            var store = new RewardTestStore();
            var wallet = new DatasaveResourceWallet(store);
            var gateway = new RewardDeliveryGateway(new IRewardDestination[] { wallet });
            Assert.That(gateway.Validate("r", "fp", Bundle(Coin(), new ResolvedRewardEntry("x", "item", "unit.scout", 1))).Reason,
                Is.EqualTo(RewardDeliveryReason.UnknownKind));
            Assert.That(gateway.Validate("r", "fp", Bundle(Coin(long.MaxValue), Coin(1, "more"))).Reason,
                Is.EqualTo(RewardDeliveryReason.Overflow));
            Assert.That(gateway.Validate("r", "fp", Bundle(new ResolvedRewardEntry("x", "currency", "invalid", 1))).Reason,
                Is.EqualTo(RewardDeliveryReason.UnknownTarget));
            Assert.That(wallet.Deliver(default).Reason, Is.EqualTo(RewardDeliveryReason.InvalidRequest));
            Assert.That(wallet.GetBalance(new ResourceId("currency.coin")), Is.Zero);
            Assert.That(store.Disk.Count, Is.Zero);
        }
    }
}
