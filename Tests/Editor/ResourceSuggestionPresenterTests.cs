using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.UI;
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
            public int Opens, Renders;
            public bool Closed;
            public UniTaskCompletionSource Closing;
            public void Open() => Opens++;
            public void Render(ResourceSuggestionState state) { State = state; Renders++; }
            public UniTask CloseAsync() { Closed = true; return Closing?.Task ?? UniTask.CompletedTask; }
            public void Shop() => AcquireRequested?.Invoke();
            public void Later() => LaterRequested?.Invoke();
        }
        private sealed class Gateway : IResourceAcquisitionGateway
        {
            public int Calls;
            public ResourceAcquisitionRequest Request;
            public ResourceAcquisitionAvailability Availability = ResourceAcquisitionAvailability.Available;
            public UniTaskCompletionSource<ResourceAcquisitionResult> Pending;
            public ResourceAcquisitionAvailability GetAvailability(ResourceId id) => Availability;
            public async UniTask<ResourceAcquisitionResult> OpenAsync(ResourceAcquisitionRequest request, CancellationToken token)
            {
                Calls++; Request = request;
                return Pending == null ? ResourceAcquisitionResult.Returned : await Pending.Task.AttachExternalCancellation(token);
            }
        }
        private static async Task<ResourceRecoveryStatus> Observe(UniTask<ResourceRecoveryStatus> task) => await task;
        private static ResourceAmount[] Costs() => new[] { new ResourceAmount(Coin, 60), new ResourceAmount(Gem, 10), new ResourceAmount(Coin, 40) };

        [Test]
        public async Task Shop_WaitsForClose_UsesOnlySelectedResource_AndCarriesAllDeficits()
        {
            var balances = new Balances(); var view = new View { Closing = new UniTaskCompletionSource() }; var gateway = new Gateway();
            using var presenter = new ResourceSuggestionPresenter(Coin, balances, view, new ResourceAcquisitionCoordinator(gateway));
            var result = Observe(presenter.ShowAsync(Costs(), "upgrade", default));
            Assert.That(view.State.ResourceId, Is.EqualTo(Coin)); Assert.That(view.State.Required, Is.EqualTo(100));
            view.Shop(); view.Shop();
            Assert.That(view.Closed, Is.True); Assert.That(gateway.Calls, Is.Zero);
            view.Later(); // Deliberate hide/destroy callbacks cannot cancel the chosen navigation.
            view.Closing.TrySetResult();
            Assert.That(await result, Is.EqualTo(ResourceRecoveryStatus.Returned));
            Assert.That(gateway.Calls, Is.EqualTo(1)); Assert.That(gateway.Request.TargetResourceId, Is.EqualTo(Coin));
            Assert.That(gateway.Request.MissingAmount, Is.EqualTo(100)); Assert.That(gateway.Request.Deficits.Count, Is.EqualTo(2));
            Assert.That(gateway.Request.SourceContextId, Is.EqualTo("upgrade"));
        }

        [Test]
        public async Task LaterAndBack_CloseWithoutNavigation_AndDetachBalanceListeners()
        {
            var balances = new Balances(); var view = new View(); var gateway = new Gateway();
            using var presenter = new ResourceSuggestionPresenter(Gem, balances, view, new ResourceAcquisitionCoordinator(gateway));
            var result = Observe(presenter.ShowAsync(Costs(), "test", default));
            view.Later();
            Assert.That(await result, Is.EqualTo(ResourceRecoveryStatus.Cancelled));
            int renders = view.Renders; balances.Fund(); view.Shop();
            Assert.That(view.Renders, Is.EqualTo(renders)); Assert.That(gateway.Calls, Is.Zero);
        }

        [Test]
        public async Task FundedOrUnavailableResource_CannotNavigate_AndNoWrongResourcePanelOpens()
        {
            var balances = new Balances(); var view = new View(); var gateway = new Gateway { Availability = ResourceAcquisitionAvailability.Unavailable };
            using var presenter = new ResourceSuggestionPresenter(Coin, balances, view, new ResourceAcquisitionCoordinator(gateway));
            Assert.That(await presenter.ShowAsync(new[] { new ResourceAmount(Gem, 1) }, null, default), Is.EqualTo(ResourceRecoveryStatus.None));
            Assert.That(view.Opens, Is.Zero);
            var result = Observe(presenter.ShowAsync(Costs(), null, default));
            view.Shop(); Assert.That(result.IsCompleted, Is.False);
            balances.Fund(); Assert.That(view.State.Missing, Is.Zero); Assert.That(view.State.CanAcquire, Is.False);
            view.Later(); await result;
            Assert.That(gateway.Calls, Is.Zero);
        }

        [Test]
        public async Task OwnerCancellationWhileNavigating_CompletesWithoutReopeningOrLateRender()
        {
            var balances = new Balances(); var view = new View(); var gateway = new Gateway { Pending = new UniTaskCompletionSource<ResourceAcquisitionResult>() };
            using var owner = new CancellationTokenSource();
            using var presenter = new ResourceSuggestionPresenter(Coin, balances, view, new ResourceAcquisitionCoordinator(gateway));
            var result = Observe(presenter.ShowAsync(Costs(), null, owner.Token));
            view.Shop(); Assert.That(gateway.Calls, Is.EqualTo(1));
            owner.Cancel(); Assert.That(await result, Is.EqualTo(ResourceRecoveryStatus.Cancelled));
            gateway.Pending.TrySetResult(ResourceAcquisitionResult.Returned); balances.Fund();
            Assert.That(view.Opens, Is.EqualTo(1)); Assert.That(view.Renders, Is.EqualTo(1));
        }

        [Test]
        public async Task CancelBeforeChoice_ClosesAndAllowsAnotherSession()
        {
            var view = new View(); var gateway = new Gateway(); var balances = new Balances();
            using var presenter = new ResourceSuggestionPresenter(Coin, balances, view, new ResourceAcquisitionCoordinator(gateway));
            using var owner = new CancellationTokenSource();
            var first = Observe(presenter.ShowAsync(Costs(), null, owner.Token));
            Assert.That(await presenter.ShowAsync(Costs(), null, default), Is.EqualTo(ResourceRecoveryStatus.Busy));
            owner.Cancel(); Assert.That(await first, Is.EqualTo(ResourceRecoveryStatus.Cancelled));
            var second = Observe(presenter.ShowAsync(Costs(), null, default)); view.Later(); await second;
            Assert.That(view.Opens, Is.EqualTo(2)); Assert.That(gateway.Calls, Is.Zero);
        }
    }
}
