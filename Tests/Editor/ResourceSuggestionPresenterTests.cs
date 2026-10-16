using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.UI;
using Dreamy.UI;
using NUnit.Framework;

namespace Dreamy.Economy.Tests
{
    public sealed class ResourceSuggestionPresenterTests
    {
        private static readonly ResourceId Coin = new("currency.coin"), Gem = new("currency.gem");
        private sealed class Balances : IResourceBalanceSource
        {
            public event Action<ResourceBalanceChanged> BalanceChanged;
            public long CoinAmount;
            public long GetBalance(ResourceId id) => id == Coin ? CoinAmount : 0;
            public void Fund() { CoinAmount = 100; BalanceChanged?.Invoke(new ResourceBalanceChanged(Coin, 100)); }
        }
        private sealed class View : IResourceSuggestionView
        {
            public event Action AcquireRequested;
            public event Action LaterRequested;
            public ResourceSuggestionState State;
            public int Renders;
            public void Render(ResourceSuggestionState state) { State = state; Renders++; }
            public void Shop() => AcquireRequested?.Invoke();
            public void Later() => LaterRequested?.Invoke();
        }
        private sealed class Gateway : IResourceAcquisitionGateway
        {
            public int Calls;
            public ResourceAcquisitionAvailability Availability = ResourceAcquisitionAvailability.Available;
            public ResourceAcquisitionAvailability GetAvailability(ResourceId id) => Availability;
            public UniTask<ResourceAcquisitionResult> OpenAsync(ResourceAcquisitionRequest request, CancellationToken token)
            { Calls++; return UniTask.FromResult(ResourceAcquisitionResult.Returned); }
        }
        private static ResourceAmount[] Costs() => new[] { new ResourceAmount(Coin, 60), new ResourceAmount(Gem, 10), new ResourceAmount(Coin, 40) };
        private static async Task<bool> Observe(ResourceSuggestionRequest request, CancellationToken token = default) => await request.WaitForChoiceAsync(token);

        [Test]
        public async Task Acquire_SurvivesPresenterDisposal_AndCannotBeOverwrittenByHide()
        {
            var balances = new Balances(); var view = new View(); var gateway = new Gateway();
            var request = new ResourceSuggestionRequest(Coin, Costs(), "upgrade");
            using var presenter = new ResourceSuggestionPresenter(request, balances, view, new ResourceAcquisitionCoordinator(gateway));
            presenter.Show();
            var result = Observe(request);
            Assert.That(view.State.Required, Is.EqualTo(100));
            Assert.That(view.State.ResourceId, Is.EqualTo(Coin));
            view.Shop(); view.Shop(); view.Later(); presenter.Dispose();
            Assert.That(await result, Is.True);
            int renders = view.Renders; balances.Fund();
            Assert.That(view.Renders, Is.EqualTo(renders));
            // Factory-owned presenter never navigates; the session must await close first.
            Assert.That(gateway.Calls, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task BackOrLater_EndsUnansweredChoice_AndDetachesListeners(bool dispose)
        {
            var balances = new Balances(); var view = new View(); var gateway = new Gateway();
            var request = new ResourceSuggestionRequest(Gem, Costs(), "test");
            using var presenter = new ResourceSuggestionPresenter(request, balances, view, new ResourceAcquisitionCoordinator(gateway));
            presenter.Show(); var result = Observe(request);
            if (dispose) presenter.Dispose(); else { view.Later(); presenter.Dispose(); }
            Assert.That(await result, Is.False);
            int renders = view.Renders; balances.Fund(); view.Shop();
            Assert.That(view.Renders, Is.EqualTo(renders)); Assert.That(gateway.Calls, Is.Zero);
        }

        [Test]
        public async Task UnavailableOrFundedResource_CannotChooseAcquire()
        {
            var balances = new Balances(); var view = new View();
            var gateway = new Gateway { Availability = ResourceAcquisitionAvailability.Unavailable };
            var request = new ResourceSuggestionRequest(Coin, Costs(), null);
            using var presenter = new ResourceSuggestionPresenter(request, balances, view, new ResourceAcquisitionCoordinator(gateway));
            presenter.Show(); var result = Observe(request);
            view.Shop(); Assert.That(result.IsCompleted, Is.False);
            gateway.Availability = ResourceAcquisitionAvailability.Available;
            balances.Fund(); view.Shop();
            Assert.That(view.State.Missing, Is.Zero); Assert.That(view.State.CanAcquire, Is.False);
            Assert.That(result.IsCompleted, Is.False);
            view.Later(); Assert.That(await result, Is.False);
        }

        [Test]
        public void Request_NormalizesAndSnapshotsCosts_PreservingOrderAndContext()
        {
            var costs = Costs(); var request = new ResourceSuggestionRequest(Gem, costs, "upgrade");
            costs[0] = new ResourceAmount(Coin, 1);
            Assert.That(request.Costs.Count, Is.EqualTo(2));
            Assert.That(request.Costs[0].ResourceId, Is.EqualTo(Coin));
            Assert.That(request.Costs[0].Amount, Is.EqualTo(100));
            Assert.That(request.Required, Is.EqualTo(10));
            Assert.That(request.ContextId, Is.EqualTo("upgrade"));
            Assert.Throws<OverflowException>(() => new ResourceSuggestionRequest(Coin,
                new[] { new ResourceAmount(Coin, long.MaxValue), new ResourceAmount(Coin, 1) }, null));
        }

        [Test]
        public async Task FactoryHost_ReopensWithFreshRequest_WithoutDuplicateSubscriptions()
        {
            var balances = new Balances(); var view = new View(); var gateway = new Gateway();
            var request = new ResourceSuggestionRequest(Coin, Costs(), null);
            var factory = new PanelPresenterFactory();
            factory.Register<View>(v => new ResourceSuggestionPresenter(request, balances, v, new ResourceAcquisitionCoordinator(gateway)));
            using var host = new PanelPresenterHost(factory, view);
            host.Show(); host.Show(); Assert.That(view.Renders, Is.EqualTo(1));
            var first = Observe(request); host.Dispose(); Assert.That(await first, Is.False);
            request = new ResourceSuggestionRequest(Gem, Costs(), null);
            host.Show(); var second = Observe(request); view.Shop();
            Assert.That(await second, Is.True); Assert.That(view.State.ResourceId, Is.EqualTo(Gem));
            host.Dispose(); int renders = view.Renders; balances.Fund();
            Assert.That(view.Renders, Is.EqualTo(renders));
        }

        [Test]
        public async Task Cancellation_CanEndWaitWithoutAnsweringAnotherSession()
        {
            var balances = new Balances(); var view = new View(); var gateway = new Gateway();
            var request = new ResourceSuggestionRequest(Coin, Costs(), null);
            using var presenter = new ResourceSuggestionPresenter(request, balances, view, new ResourceAcquisitionCoordinator(gateway));
            using var owner = new CancellationTokenSource();
            presenter.Show(); var result = Observe(request, owner.Token); owner.Cancel();
            try { await result; Assert.Fail("Expected cancellation."); } catch (OperationCanceledException) { }
            presenter.Dispose(); Assert.That(request.HasChoice, Is.True);
        }
    }
}
