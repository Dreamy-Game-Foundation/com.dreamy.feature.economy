using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.UI;
using Dreamy.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.Tests
{
    public sealed class ResourceRecoveryCloseTests
    {
        private GameObject root;
        private NotEnoughResourcesPanel panel;
        private UITweenPlayer player;
        private PendingTween tween;

        // No timer or frame dependency: the test controls exactly when the hide finishes.
        private sealed class PendingTween : ITween
        {
            public bool IsAutoRun => true;
            public bool IsEnabled => true;
            public int Hides;
            public UniTaskCompletionSource Pending = new();
            public UniTask Init() => UniTask.CompletedTask;
            public UniTask Show(CancellationToken token) => UniTask.CompletedTask;
            public UniTask Hide(CancellationToken token) { Hides++; return Pending.Task; }
            public void Kill() { }
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Recovery close regression");
            root.AddComponent<PanelManager>();
            var go = new GameObject("Shortage", typeof(RectTransform), typeof(CanvasGroup));
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            var button = new GameObject("Cancel", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(go.transform);
            player = go.AddComponent<UITweenPlayer>();
            panel = go.AddComponent<NotEnoughResourcesPanel>();
            SetField(panel, "cancelButton", button);
            var status = new GameObject("Status", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            status.transform.SetParent(go.transform);
            SetField(panel, "statusText", status);
            // EditMode AddComponent does not run MonoBehaviour.Awake; wire the real button lifecycle explicitly.
            typeof(NotEnoughResourcesPanel).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
            panel.Init().GetAwaiter().GetResult();
            panel.Show().GetAwaiter().GetResult();
            player.Init().GetAwaiter().GetResult();
            tween = new PendingTween();
            // Install a controlled ITween in the real player, without exposing a production test hook.
            var cache = (List<ITween>)typeof(UITweenPlayer).GetField("cachedTweens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            cache.Clear();
            cache.Add(tween);
            SetField(player, "cacheDirty", false);
        }

        [TearDown]
        public void TearDown()
        {
            tween.Pending.TrySetResult();
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public async Task CloseAndAwaitWhileHidePending_ShareOneHide_ThenCanReopenAndCloseAgain()
        {
            panel.Close(); // Presenter cleanup: starts Hide and observes it via Forget.
            var first = Observe(panel.CloseAsync()); // Application flow finally: await the same operation.
            var second = Observe(panel.CloseAsync());
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(second.IsCompleted, Is.False);
            Assert.That(tween.Hides, Is.EqualTo(1));
            tween.Pending.TrySetResult();
            await Task.WhenAll(first, second);
            Assert.That(panel.gameObject.activeSelf, Is.False);

            panel.Open();
            Assert.That(panel.gameObject.activeSelf, Is.True);
            // OnEnable invalidates the player cache; reinstall the controlled tween for the second cycle.
            var cache = (List<ITween>)typeof(UITweenPlayer).GetField("cachedTweens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            cache.Clear(); cache.Add(tween); SetField(player, "cacheDirty", false);
            tween.Pending = new UniTaskCompletionSource();
            var next = Observe(panel.CloseAsync());
            Assert.That(next.IsCompleted, Is.False);
            Assert.That(tween.Hides, Is.EqualTo(2));
            tween.Pending.TrySetResult();
            await next;
            Assert.That(panel.gameObject.activeSelf, Is.False);
        }

        [Test]
        public async Task OnPreHideReentry_UsesAlreadyPublishedCompletion()
        {
            Task reentrant = null;
            panel.OnPreHide += () => reentrant = Observe(panel.CloseAsync());
            var outer = Observe(panel.CloseAsync());
            Assert.That(reentrant, Is.Not.Null);
            Assert.That(reentrant.IsCompleted, Is.False);
            Assert.That(tween.Hides, Is.EqualTo(1));
            tween.Pending.TrySetResult();
            await Task.WhenAll(outer, reentrant);
        }

        [Test]
        public async Task HideFailure_ReachesEveryWaiter_WithoutSecondContinuationRegistration()
        {
            var first = Observe(panel.CloseAsync());
            var second = Observe(panel.CloseAsync());
            tween.Pending.TrySetException(new InvalidOperationException("Injected hide failure"));
            foreach (var waiter in new[] { first, second })
            {
                try { await waiter; Assert.Fail("Expected hide failure"); }
                catch (InvalidOperationException exception) { Assert.That(exception.Message, Is.EqualTo("Injected hide failure")); }
            }
            Assert.That(tween.Hides, Is.EqualTo(1));
        }

        private sealed class FundedBalanceSource : IResourceBalanceSource
        {
            public event Action<ResourceBalanceChanged> BalanceChanged;
            public long GetBalance(ResourceId id) => 100;
        }

        [Test]
        public async Task Presentation_NewRequestReplacesPendingSession_AndRepeatedPurchasesAlwaysReopen()
        {
            var presentation = new ResourceRecoveryPanelPresentation(new FundedBalanceSource(),
                new ResourceAcquisitionCoordinator(null), panel);
            var costs = new[] { new ResourceAmount(new ResourceId("currency.coin"), 10) };
            var first = ObserveResult(presentation.ShowAsync(costs, "first", default));
            var second = ObserveResult(presentation.ShowAsync(costs, "second", default));
            Assert.That(first.IsCompleted, Is.False, "Replacement waits for the old hide tween.");
            Assert.That(second.IsCompleted, Is.False);
            Assert.That(tween.Hides, Is.EqualTo(1));
            tween.Pending.TrySetResult();
            Assert.That(await first, Is.EqualTo(ResourceRecoveryStatus.Cancelled));
            Assert.That(panel.gameObject.activeSelf, Is.True, "Second request opens after the previous close.");
            Assert.That(second.IsCompleted, Is.False);
            var button = (Button)typeof(NotEnoughResourcesPanel).GetField("cancelButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            button.onClick.Invoke();
            Assert.That(await second, Is.EqualTo(ResourceRecoveryStatus.Cancelled));
            for (int i = 0; i < 3; i++)
            {
                var next = ObserveResult(presentation.ShowAsync(costs, "repeat", default));
                Assert.That(panel.gameObject.activeSelf, Is.True);
                Assert.That(next.IsCompleted, Is.False);
                button.onClick.Invoke();
                Assert.That(await next, Is.EqualTo(ResourceRecoveryStatus.Cancelled));
                Assert.That(panel.gameObject.activeSelf, Is.False);
            }
        }

        private static async Task<ResourceRecoveryStatus> ObserveResult(UniTask<ResourceRecoveryStatus> task) => await task;

        private static async Task Observe(UniTask task) => await task;
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
