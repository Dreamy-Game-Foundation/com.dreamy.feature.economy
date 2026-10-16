using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.UI;
using NUnit.Framework;

namespace Dreamy.Economy.Tests
{
    public sealed class ResourceRecoveryPresentationRouterTests
    {
        private static readonly ResourceId Coin = new("currency.coin");
        private static readonly ResourceId Gem = new("currency.gem");

        private sealed class Balances : IResourceBalanceProvider
        {
            public readonly Dictionary<ResourceId, long> Values = new();
            public long GetBalance(ResourceId id) => Values.TryGetValue(id, out var value) ? value : 0;
        }

        private sealed class Presentation : IInsufficientResourcesPresentation
        {
            public int Calls;
            public IReadOnlyList<ResourceAmount> Costs;
            public string Context;
            public CancellationToken Token;
            public UniTaskCompletionSource<ResourceRecoveryStatus> Pending;
            public UniTask<ResourceRecoveryStatus> ShowAsync(IReadOnlyList<ResourceAmount> costs, string contextId, CancellationToken token)
            {
                Calls++;
                Costs = costs;
                Context = contextId;
                Token = token;
                return Pending != null ? Pending.Task : UniTask.FromResult(ResourceRecoveryStatus.Returned);
            }
        }

        [Test]
        public async Task RoutesOnlyMissingResources_AndPreservesFullCostsAndContext()
        {
            var balances = new Balances();
            balances.Values[Coin] = 10;
            var coins = new Presentation();
            var gems = new Presentation();
            var router = new ResourceRecoveryPresentationRouter(balances,
                new Dictionary<ResourceId, IInsufficientResourcesPresentation> { [Coin] = coins, [Gem] = gems });
            var result = await router.ShowAsync(new[] { new ResourceAmount(Coin, 10), new ResourceAmount(Gem, 5) }, "upgrade", default);
            Assert.That(result, Is.EqualTo(ResourceRecoveryStatus.Returned));
            Assert.That(coins.Calls, Is.Zero);
            Assert.That(gems.Calls, Is.EqualTo(1));
            Assert.That(gems.Costs.Count, Is.EqualTo(2));
            Assert.That(gems.Context, Is.EqualTo("upgrade"));
        }

        [Test]
        public async Task MultipleDeficits_SelectFirstMappedCost_AndNormalizeDuplicateIds()
        {
            var balances = new Balances();
            balances.Values[Gem] = 3;
            var coins = new Presentation();
            var gems = new Presentation();
            var router = new ResourceRecoveryPresentationRouter(balances,
                new Dictionary<ResourceId, IInsufficientResourcesPresentation> { [Coin] = coins, [Gem] = gems });
            await router.ShowAsync(new[] { new ResourceAmount(Gem, 2), new ResourceAmount(Coin, 10), new ResourceAmount(Gem, 2) }, null, default);
            Assert.That(coins.Calls, Is.Zero);
            Assert.That(gems.Calls, Is.EqualTo(1));
            Assert.That(gems.Costs[0].Amount, Is.EqualTo(4));
        }

        [Test]
        public async Task UnmappedDeficit_UsesFallback_OrReturnsUnavailable()
        {
            var fallback = new Presentation();
            var routes = new Dictionary<ResourceId, IInsufficientResourcesPresentation>();
            var costs = new[] { new ResourceAmount(Coin, 1) };
            var router = new ResourceRecoveryPresentationRouter(new Balances(), routes, fallback);
            Assert.That(await router.ShowAsync(costs, null, default), Is.EqualTo(ResourceRecoveryStatus.Returned));
            Assert.That(fallback.Calls, Is.EqualTo(1));
            router = new ResourceRecoveryPresentationRouter(new Balances(), routes);
            Assert.That(await router.ShowAsync(costs, null, default), Is.EqualTo(ResourceRecoveryStatus.PresentationUnavailable));
        }

        [Test]
        public async Task FundedAndCancelledRequests_DoNotOpenPanels()
        {
            var balances = new Balances();
            balances.Values[Coin] = 5;
            var fallback = new Presentation();
            var router = new ResourceRecoveryPresentationRouter(balances,
                new Dictionary<ResourceId, IInsufficientResourcesPresentation>(), fallback);
            Assert.That(await router.ShowAsync(new[] { new ResourceAmount(Coin, 5) }, null, default), Is.EqualTo(ResourceRecoveryStatus.None));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.That(await router.ShowAsync(new[] { new ResourceAmount(Gem, 1) }, null, cts.Token), Is.EqualTo(ResourceRecoveryStatus.Cancelled));
            Assert.That(fallback.Calls, Is.Zero);
        }

        [Test]
        public async Task SwitchingResources_CancelsOldPanel_AndWaitsForItsClose()
        {
            var coins = new Presentation { Pending = new UniTaskCompletionSource<ResourceRecoveryStatus>() };
            var gems = new Presentation();
            var router = new ResourceRecoveryPresentationRouter(new Balances(),
                new Dictionary<ResourceId, IInsufficientResourcesPresentation> { [Coin] = coins, [Gem] = gems });
            var first = router.ShowAsync(new[] { new ResourceAmount(Coin, 1) }, null, default);
            var second = router.ShowAsync(new[] { new ResourceAmount(Gem, 1) }, null, default);
            Assert.That(coins.Token.IsCancellationRequested, Is.True);
            Assert.That(gems.Calls, Is.Zero);
            coins.Pending.TrySetResult(ResourceRecoveryStatus.Returned);
            Assert.That(await first, Is.EqualTo(ResourceRecoveryStatus.Cancelled));
            Assert.That(await second, Is.EqualTo(ResourceRecoveryStatus.Returned));
            Assert.That(gems.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task QueuedRequest_RechecksBalancesAfterPreviousPanelCloses()
        {
            var balances = new Balances();
            var coins = new Presentation { Pending = new UniTaskCompletionSource<ResourceRecoveryStatus>() };
            var gems = new Presentation();
            var router = new ResourceRecoveryPresentationRouter(balances,
                new Dictionary<ResourceId, IInsufficientResourcesPresentation> { [Coin] = coins, [Gem] = gems });
            var first = router.ShowAsync(new[] { new ResourceAmount(Coin, 1) }, null, default);
            var second = router.ShowAsync(new[] { new ResourceAmount(Gem, 1) }, null, default);
            balances.Values[Gem] = 1;
            coins.Pending.TrySetResult(ResourceRecoveryStatus.Cancelled);
            await first;
            Assert.That(await second, Is.EqualTo(ResourceRecoveryStatus.None));
            Assert.That(gems.Calls, Is.Zero);
        }
    }
}
