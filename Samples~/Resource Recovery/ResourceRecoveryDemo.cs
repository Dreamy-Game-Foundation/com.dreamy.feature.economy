using System;
using Cysharp.Threading.Tasks;
using Dreamy.Datasave;
using Dreamy.UI;
using Dreamy.Economy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.Samples
{
    // Standalone demonstration only. A real game injects its already-owned wallet and acquisition gateway.
    public sealed class ResourceRecoveryDemo : MonoBehaviour
    {
        [SerializeField] private NotEnoughResourcesPanel panel;
        [SerializeField] private string coinSuggestionAddress = "Panel/CoinSuggestionPanel.prefab";
        [SerializeField] private string gemSuggestionAddress = "Panel/GemSuggestionPanel.prefab";
        [SerializeField] private RecoveryDemoDestination destination;
        [SerializeField] private Button silentButton;
        [SerializeField] private Button promptButton;
        [SerializeField] private Button multiButton;
        [SerializeField] private Button directButton;
        [SerializeField] private Button grantButton;
        [SerializeField] private TMP_Text statusText;
        private PanelPresenterFactory suggestionFactory;
        private DatasaveResourceWallet wallet;
        private NotEnoughResourcesPresenter presenter;
        private ResourceActionFlow flow;
        private ResourceAcquisitionCoordinator acquisition;
        private static readonly ResourceId Coin = new("currency.coin"), Gem = new("currency.gem");

        private void Start() => InitializeAsync().Forget(Debug.LogException);

        private async UniTask InitializeAsync()
        {
            // Let PanelManager finish its initial child scan before registering this scene-root panel.
            await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy());
            await panel.Init();
            await panel.PostInit();
            wallet = new DatasaveResourceWallet(new DatasaveService(new DatasaveOptions { DirectoryName = "DreamyRecoveryDemo" }));
            wallet.NotificationFailed += Debug.LogException;
            acquisition = new ResourceAcquisitionCoordinator(destination);
            presenter = new NotEnoughResourcesPresenter(wallet, panel, acquisition);
            var routes = new System.Collections.Generic.Dictionary<ResourceId, IInsufficientResourcesPresentation>();
            // Standalone scene owns its factory; game integration registers in the existing root factory.
            if (PanelManager.Instance.PresenterFactory != null)
                throw new InvalidOperationException("Run the recovery demo standalone; a game presenter factory is already installed.");
            suggestionFactory = new PanelPresenterFactory();
            routes.Add(Coin, ResourceSuggestionInstaller.Install<CoinSuggestionPanel>(
                suggestionFactory, Coin, coinSuggestionAddress, wallet, acquisition));
            routes.Add(Gem, ResourceSuggestionInstaller.Install<GemSuggestionPanel>(
                suggestionFactory, Gem, gemSuggestionAddress, wallet, acquisition));
            PanelManager.Instance.PresenterFactory = suggestionFactory;
            flow = new ResourceActionFlow(wallet, new ResourceRecoveryPresentationRouter(wallet, routes, presenter));
            panel.Close(); destination.gameObject.SetActive(false);
            silentButton.onClick.AddListener(Silent);
            promptButton.onClick.AddListener(Prompt);
            multiButton.onClick.AddListener(Multi);
            directButton.onClick.AddListener(Direct);
            grantButton.onClick.AddListener(GrantDemoResources);
            Refresh();
        }
        private void Silent() => Spend(false, false).Forget(Debug.LogException);
        private void Prompt() => Spend(true, false).Forget(Debug.LogException);
        private void Multi() => Spend(true, true).Forget(Debug.LogException);
        private async UniTask Spend(bool prompt, bool multi)
        {
            var costs = multi ? new[] { new ResourceAmount(Coin, 100), new ResourceAmount(Gem, 10) } : new[] { new ResourceAmount(Coin, 100) };
            var result = await flow.ExecuteAsync(new ResourceSpendRequest(Guid.NewGuid().ToString("N"), costs, "recovery-demo"),
                prompt ? InsufficientResourcePresentation.Prompt : InsufficientResourcePresentation.None, this.GetCancellationTokenOnDestroy());
            if (this == null) return;
            Refresh($"Transaction: {result.Transaction.Status}; recovery: {result.Recovery}");
        }
        private void Direct() => OpenDirect().Forget(Debug.LogException);
        private async UniTask OpenDirect()
        {
            var result = await acquisition.OpenForResourceAsync(Gem, 10, "direct-demo", this.GetCancellationTokenOnDestroy());
            if (this != null) Refresh("Navigation: " + result);
        }
        private void GrantDemoResources()
        {
            // Explicit demo button, separate from navigation. Never replace a production purchase with this.
            wallet.TryGrant(new ResourceGrantRequest(Guid.NewGuid().ToString("N"), new ResourceAmount(Coin, 100)));
            wallet.TryGrant(new ResourceGrantRequest(Guid.NewGuid().ToString("N"), new ResourceAmount(Gem, 10)));
            Refresh("Demo resources added. Confirm an action to spend.");
        }
        private void Refresh(string message = "Choose an action. Demo resources persist across restarts.") =>
            statusText.text = $"Coin: {wallet.GetBalance(Coin)}   Gem: {wallet.GetBalance(Gem)}\n{message}";
        private void OnDestroy()
        {
            if (PanelManager.HasInstance && ReferenceEquals(PanelManager.Instance.PresenterFactory, suggestionFactory))
                PanelManager.Instance.PresenterFactory = null;
            presenter?.Dispose();
            silentButton.onClick.RemoveListener(Silent); promptButton.onClick.RemoveListener(Prompt);
            multiButton.onClick.RemoveListener(Multi); directButton.onClick.RemoveListener(Direct);
            grantButton.onClick.RemoveListener(GrantDemoResources);
        }
    }
}
