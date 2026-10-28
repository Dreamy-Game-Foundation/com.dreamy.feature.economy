using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Economy.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Dreamy.Economy.Tests
{
    public sealed class ResourceUIHolderTests
    {
        private static readonly ResourceId Coin = new("currency.coin");
        private static readonly ResourceId Gem = new("currency.gem");
        private readonly List<GameObject> roots = new();
        private Balances balances;
        private ResourceUIHolder holder;
        private TMP_Text text;

        private sealed class Balances : IResourceBalanceSource
        {
            private Action<ResourceBalanceChanged> changed;
            private readonly Dictionary<ResourceId, long> values = new();
            public int Listeners => changed?.GetInvocationList().Length ?? 0;
            public int Reads;
            public event Action<ResourceBalanceChanged> BalanceChanged
            {
                add => changed += value;
                remove => changed -= value;
            }
            public long GetBalance(ResourceId id)
            {
                Reads++;
                return values.TryGetValue(id, out long value) ? value : 0;
            }
            public void Set(ResourceId id, long value, bool notify = true)
            {
                values[id] = value;
                if (notify) changed?.Invoke(new ResourceBalanceChanged(id, value));
            }
        }

        [SetUp]
        public void SetUp()
        {
            balances = new Balances();
            balances.Set(Coin, 10);
            holder = CreateHolder(balances, true, out text);
        }

        private ResourceUIHolder CreateHolder(Balances provider, bool autoRefresh, out TMP_Text amount)
        {
            var root = new GameObject("Resource holder test", typeof(RectTransform));
            roots.Add(root);
            root.SetActive(false);
            amount = root.AddComponent<TextMeshProUGUI>();
            var view = root.AddComponent<ResourceUIHolder>();
            SetField(view, "amountText", amount);
            SetField(view, "autoRefresh", autoRefresh);
            view.SetResource(Coin.Value);
            view.Bind(provider);
            root.SetActive(true);
            return view;
        }

        private static void SetField(ResourceUIHolder view, string name, object value) =>
            typeof(ResourceUIHolder).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
            Assert.That(balances.Listeners, Is.Zero);
        }

        [Test]
        public void AutoRefresh_TracksEvents_AndFiltersOtherResources()
        {
            Assert.That(text.text, Is.EqualTo("10"));
            balances.Set(Coin, 20);
            Assert.That(text.text, Is.EqualTo("20"));
            int reads = balances.Reads;
            balances.Set(Gem, 9);
            Assert.That(text.text, Is.EqualTo("20"));
            Assert.That(balances.Reads, Is.EqualTo(reads));
            Assert.That(balances.Listeners, Is.EqualTo(1));
        }

        [TestCase(35)]
        [TestCase(3)]
        public void Defer_BlocksEventsAndManualRefresh_ThenReadsLatestProvider(long latest)
        {
            var scope = holder.DeferRefresh();
            balances.Set(Coin, 50);
            holder.RefreshBalance();
            Assert.That(text.text, Is.EqualTo("10"));
            Assert.That(balances.Listeners, Is.EqualTo(1));
            // No event: release must read the provider rather than the last event's snapshot.
            balances.Set(Coin, latest, false);
            scope.Dispose();
            Assert.That(text.text, Is.EqualTo(latest.ToString()));
            balances.Set(Coin, 80);
            Assert.That(text.text, Is.EqualTo("80"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedScopes_OnlyFinalReleaseRefreshes_AndRepeatedDisposeIsSafe(bool reverse)
        {
            var first = holder.DeferRefresh();
            var second = holder.DeferRefresh();
            var early = reverse ? second : first;
            var last = reverse ? first : second;
            balances.Set(Coin, 40);
            early.Dispose();
            early.Dispose();
            holder.RefreshBalance();
            Assert.That(text.text, Is.EqualTo("10"));
            balances.Set(Coin, 4);
            last.Dispose();
            Assert.That(text.text, Is.EqualTo("4"));
            int reads = balances.Reads;
            last.Dispose();
            Assert.That(balances.Reads, Is.EqualTo(reads));
        }

        [Test]
        public void CallerUsing_ReleasesOnException()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (holder.DeferRefresh())
                {
                    balances.Set(Coin, 25);
                    throw new InvalidOperationException("Presentation failed");
                }
            });
            Assert.That(text.text, Is.EqualTo("25"));
            balances.Set(Coin, 26);
            Assert.That(text.text, Is.EqualTo("26"));
        }

        [UnityTest]
        public IEnumerator CallerFinally_ReleasesWhenAsyncPresentationIsCancelled()
        {
            using var cancellation = new CancellationTokenSource();
            Task run = RunPresentationAsync(cancellation.Token);
            Assert.That(text.text, Is.EqualTo("10"));
            cancellation.Cancel();
            while (!run.IsCompleted) yield return null;
            Assert.That(run.IsCanceled, Is.True);
            Assert.That(text.text, Is.EqualTo("30"));
        }

        private async Task RunPresentationAsync(CancellationToken cancellation)
        {
            IDisposable scope = holder.DeferRefresh();
            try
            {
                balances.Set(Coin, 30);
                var completion = new TaskCompletionSource<bool>();
                using (cancellation.Register(() => completion.TrySetCanceled())) await completion.Task;
            }
            finally { scope.Dispose(); }
        }

        [UnityTest]
        public IEnumerator PanelClose_CancelsAndReleasesImmediately_WhileTaskIsStillPending()
        {
            using var cancellation = new CancellationTokenSource();
            var finish = new TaskCompletionSource<bool>();
            IDisposable scope = holder.DeferRefresh();
            Task run = FinishLaterAsync(scope, finish.Task, cancellation.Token);
            balances.Set(Coin, 40);
            // The owner's close hook runs even when navigation only plays HideTween.
            cancellation.Cancel();
            scope.Dispose();
            Assert.That(holder.gameObject.activeInHierarchy, Is.True);
            Assert.That(cancellation.IsCancellationRequested, Is.True);
            Assert.That(run.IsCompleted, Is.False);
            Assert.That(text.text, Is.EqualTo("40"));
            using (holder.DeferRefresh())
            {
                balances.Set(Coin, 50);
                finish.SetResult(true);
                while (!run.IsCompleted) yield return null;
                Assert.That(run.IsCanceled, Is.True);
                Assert.That(text.text, Is.EqualTo("40"), "Late finally must not release a newer scope.");
            }
            Assert.That(text.text, Is.EqualTo("50"));
        }

        private static async Task FinishLaterAsync(IDisposable scope, Task finish, CancellationToken cancellation)
        {
            try
            {
                await finish;
                cancellation.ThrowIfCancellationRequested();
            }
            finally { scope.Dispose(); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisableEnable_InvalidatesScopes_DetachesAndResubscribes(bool deactivateObject)
        {
            IDisposable old = holder.DeferRefresh();
            if (deactivateObject) holder.gameObject.SetActive(false);
            else holder.enabled = false;
            Assert.That(balances.Listeners, Is.Zero);
            balances.Set(Coin, 20);
            Assert.That(text.text, Is.EqualTo("10"));
            if (deactivateObject) holder.gameObject.SetActive(true);
            else holder.enabled = true;
            Assert.That(text.text, Is.EqualTo("20"));
            Assert.That(balances.Listeners, Is.EqualTo(1));
            using (holder.DeferRefresh())
            {
                balances.Set(Coin, 30);
                old.Dispose();
                old.Dispose();
                Assert.That(text.text, Is.EqualTo("20"));
            }
            Assert.That(text.text, Is.EqualTo("30"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Rebind_InvalidatesOldTokens_AndDetachesOldSource(bool unbindFirst)
        {
            IDisposable old = holder.DeferRefresh();
            var replacement = new Balances();
            replacement.Set(Coin, 60);
            if (unbindFirst)
            {
                holder.Unbind();
                Assert.That(balances.Listeners, Is.Zero);
                Assert.Throws<InvalidOperationException>(() => holder.DeferRefresh());
            }
            holder.Bind(replacement);
            Assert.That(balances.Listeners, Is.Zero);
            Assert.That(replacement.Listeners, Is.EqualTo(1));
            Assert.That(text.text, Is.EqualTo("60"));
            using (holder.DeferRefresh())
            {
                replacement.Set(Coin, 70);
                balances.Set(Coin, 900);
                old.Dispose();
                Assert.That(text.text, Is.EqualTo("60"));
            }
            Assert.That(text.text, Is.EqualTo("70"));
            holder.Unbind();
            Assert.That(replacement.Listeners, Is.Zero);
        }

        [Test]
        public void BindSameProvider_InvalidatesOldTokens_WithoutDuplicateSubscriptions()
        {
            IDisposable old = holder.DeferRefresh();
            balances.Set(Coin, 20);
            holder.Bind(balances);
            Assert.That(text.text, Is.EqualTo("20"));
            Assert.That(balances.Listeners, Is.EqualTo(1));
            using (holder.DeferRefresh())
            {
                balances.Set(Coin, 30);
                old.Dispose();
                Assert.That(text.text, Is.EqualTo("20"));
            }
            Assert.That(text.text, Is.EqualTo("30"));
        }

        [Test]
        public void SetResource_InvalidatesOldTokens_AndTracksNewCurrency()
        {
            IDisposable old = holder.DeferRefresh();
            balances.Set(Gem, 5);
            holder.SetResource(Gem.Value);
            Assert.That(text.text, Is.EqualTo("5"));
            using (holder.DeferRefresh())
            {
                balances.Set(Gem, 8);
                balances.Set(Coin, 900);
                old.Dispose();
                holder.RefreshBalance();
                Assert.That(text.text, Is.EqualTo("5"));
            }
            Assert.That(text.text, Is.EqualTo("8"));
            Assert.That(balances.Listeners, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Destroy_DetachesSource_AndMakesOldTokenHarmless()
        {
            IDisposable old = holder.DeferRefresh();
            Object.Destroy(holder.gameObject);
            yield return null;
            Assert.That(balances.Listeners, Is.Zero);
            Assert.DoesNotThrow(() => old.Dispose());
            Assert.DoesNotThrow(() => old.Dispose());
        }

        [Test]
        public void TwoHolders_DeferOne_DoesNotBlockTheOther()
        {
            var shop = CreateHolder(balances, true, out TMP_Text shopText);
            using (shop.DeferRefresh())
            {
                balances.Set(Coin, 3);
                Assert.That(text.text, Is.EqualTo("3"));
                Assert.That(shopText.text, Is.EqualTo("10"));
                Assert.That(balances.Listeners, Is.EqualTo(2));
            }
            Assert.That(shopText.text, Is.EqualTo("3"));
        }

        [Test]
        public void AutoRefreshFalse_PreservesManualMode_AndDefersExplicitRefresh()
        {
            var manual = CreateHolder(balances, false, out TMP_Text manualText);
            Assert.That(balances.Listeners, Is.EqualTo(1)); // Only the automatic fixture holder.
            balances.Set(Coin, 20);
            Assert.That(manualText.text, Is.EqualTo("10"));
            manual.RefreshBalance();
            Assert.That(manualText.text, Is.EqualTo("20"));
            using (manual.DeferRefresh())
            {
                balances.Set(Coin, 30);
                manual.RefreshBalance();
                Assert.That(manualText.text, Is.EqualTo("20"));
            }
            Assert.That(manualText.text, Is.EqualTo("30"));
            balances.Set(Coin, 40);
            Assert.That(manualText.text, Is.EqualTo("30"));
            manual.enabled = false;
            manual.enabled = true;
            Assert.That(manualText.text, Is.EqualTo("40"));
            Assert.That(balances.Listeners, Is.EqualTo(1));
        }

        [Test]
        public void BindWhileDisabled_DoesNotSubscribe_UntilEnabled()
        {
            holder.enabled = false;
            holder.Bind(balances);
            Assert.That(balances.Listeners, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => holder.DeferRefresh());
            balances.Set(Coin, 25);
            holder.enabled = true;
            Assert.That(text.text, Is.EqualTo("25"));
            Assert.That(balances.Listeners, Is.EqualTo(1));
        }

        [Test]
        public void ScopeCreatedAfterMutation_CannotUndoAlreadyDisplayedBalance()
        {
            balances.Set(Coin, 20);
            using (holder.DeferRefresh()) Assert.That(text.text, Is.EqualTo("20"));
        }
    }
}
