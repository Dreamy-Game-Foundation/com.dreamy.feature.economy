using Dreamy.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.UI
{
    [DisallowMultipleComponent]
    public sealed class ResourceUIHolder : MonoBehaviour
    {
        [SerializeField] private string resourceId = "currency.coin";
        [SerializeField] private ResourceDisplayCatalog catalog;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text displayNameText;

        private IResourceBalanceProvider balanceProvider;
        private IResourceBalanceSource balanceSource;
        private ResourceId currentResourceId;
        private ResourceAmountFormat amountFormat = ResourceAmountFormat.Compact;

        public ResourceId ResourceId => currentResourceId;

        private void OnEnable()
        {
            ApplyResource(resourceId);
            if (ServiceLocator.TryGet<IResourceBalanceProvider>(out IResourceBalanceProvider provider))
            {
                Bind(provider);
                return;
            }

            if (ServiceLocator.TryGet<IResourceWallet>(out IResourceWallet wallet) &&
                wallet is IResourceBalanceProvider walletProvider)
            {
                Bind(walletProvider);
            }
        }

        private void OnDisable() => Unbind();

        private void OnValidate()
        {
            ApplyResource(resourceId);
            if (!Application.isPlaying) RefreshBalance();
        }

        public void Bind(IResourceBalanceProvider provider)
        {
            Unbind();
            balanceProvider = provider;
            balanceSource = provider as IResourceBalanceSource;
            if (balanceSource != null) balanceSource.BalanceChanged += OnBalanceChanged;
            RefreshBalance();
        }

        public void SetResource(string value)
        {
            resourceId = value;
            ApplyResource(value);
            RefreshBalance();
        }

        public void RefreshBalance()
        {
            if (amountText == null) return;
            long balance = balanceProvider?.GetBalance(currentResourceId) ?? 0L;
            amountText.text = ResourceAmountFormatter.Format(balance, amountFormat);
        }

        private void ApplyResource(string value)
        {
            if (!Dreamy.Economy.ResourceId.TryParse(value, out currentResourceId))
            {
                Debug.LogWarning($"Invalid resource ID '{value}'.", this);
                currentResourceId = default;
                return;
            }

            if (catalog != null && catalog.TryGet(currentResourceId, out ResourceDisplayDefinition display))
            {
                amountFormat = display.AmountFormat;
                if (iconImage != null)
                {
                    if (display.Icon != null) iconImage.sprite = display.Icon;
                    iconImage.color = display.IconColor;
                    iconImage.enabled = iconImage.sprite != null;
                }
                if (displayNameText != null) displayNameText.text = display.DisplayName;
                return;
            }

            amountFormat = ResourceAmountFormat.Compact;
            if (iconImage != null) iconImage.enabled = iconImage.sprite != null;
            if (displayNameText != null) displayNameText.text = currentResourceId.Value;
        }

        private void OnBalanceChanged(ResourceBalanceChanged change)
        {
            if (change.ResourceId == currentResourceId && amountText != null)
            {
                amountText.text = ResourceAmountFormatter.Format(change.Balance, amountFormat);
            }
        }

        private void Unbind()
        {
            if (balanceSource != null) balanceSource.BalanceChanged -= OnBalanceChanged;
            balanceSource = null;
            balanceProvider = null;
        }
    }
}
