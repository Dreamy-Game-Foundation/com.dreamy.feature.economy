using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Dreamy.Economy;
using Dreamy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Feature.Economy.Integration
{
    public sealed class EconomyDemoPanel : UIPanel
    {
        [SerializeField] private TMP_Text balancesText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button grantButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private string[] resourceIds = { "currency.coin", "currency.gem", "currency.gold", "item.chest" };
        private IResourceWallet wallet;
        private IResourceBalanceProvider balances;
        private IResourceBalanceSource source;
        private string lastGrantId;
        public override bool CanBack => true;

        public void Configure(IResourceWallet resourceWallet, IResourceBalanceProvider provider)
        {
            Unbind();
            wallet = resourceWallet ?? throw new ArgumentNullException(nameof(resourceWallet));
            balances = provider ?? throw new ArgumentNullException(nameof(provider));
            source = provider as IResourceBalanceSource;
            if (isActiveAndEnabled) Bind();
            Refresh();
        }

        private void OnEnable()
        {
            grantButton.onClick.AddListener(Grant);
            retryButton.onClick.AddListener(Retry);
            closeButton.onClick.AddListener(Close);
            Bind();
            Refresh();
        }
        private void Bind()
        {
            if (source == null) return;
            source.BalanceChanged -= BalanceChanged;
            source.BalanceChanged += BalanceChanged;
        }
        private void Unbind() { if (source != null) source.BalanceChanged -= BalanceChanged; }
        private void BalanceChanged(ResourceBalanceChanged _) => Refresh();
        private void Refresh()
        {
            retryButton.interactable = lastGrantId != null;
            grantButton.interactable = wallet != null;
            if (balances == null) return;
            var text = new StringBuilder();
            foreach (string id in resourceIds) text.AppendLine($"{id}: {balances.GetBalance(new ResourceId(id))}");
            balancesText.text = text.ToString();
        }
        private void Grant() { lastGrantId = "economy-demo/" + Guid.NewGuid().ToString("N"); ApplyGrant(); }
        private void Retry() { if (lastGrantId != null) ApplyGrant(); }
        private void ApplyGrant()
        {
            bool granted = wallet.TryGrant(new ResourceGrantRequest(lastGrantId, new ResourceAmount(new ResourceId("currency.coin"), 100)));
            statusText.text = granted ? "Grant accepted (same transaction grants once)" : "Grant failed; retry available";
            Refresh();
        }
        private void Close() => Hide().Forget();
        protected override void OnDisable()
        {
            grantButton.onClick.RemoveListener(Grant);
            retryButton.onClick.RemoveListener(Retry);
            closeButton.onClick.RemoveListener(Close);
            Unbind();
            base.OnDisable();
        }
        protected override void OnDestroy() { Unbind(); base.OnDestroy(); }
    }
}
