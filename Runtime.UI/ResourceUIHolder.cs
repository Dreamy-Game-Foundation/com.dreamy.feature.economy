using System;
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
        [SerializeField] private bool autoRefresh = true;

        private IResourceBalanceProvider balanceProvider;
        private IResourceBalanceSource balanceSource;
        private ResourceId currentResourceId;
        private ResourceAmountFormat amountFormat = ResourceAmountFormat.Compact;
        private RefreshDeferralState refreshDeferral;

        private sealed class RefreshDeferralState
        {
            public ResourceUIHolder Owner;
            public int Count;
        }

        private sealed class RefreshDeferral : IDisposable
        {
            private RefreshDeferralState state;

            public RefreshDeferral(RefreshDeferralState state) => this.state = state;

            public void Dispose()
            {
                RefreshDeferralState released = state;
                state = null;
                if (released == null || released.Owner == null) return;
                if (--released.Count != 0) return;
                ResourceUIHolder owner = released.Owner;
                released.Owner = null;
                owner.refreshDeferral = null;
                owner.RefreshBalance();
            }
        }

        public ResourceId ResourceId => currentResourceId;
        public Transform IconTarget => iconImage != null ? iconImage.transform : transform;
        public Sprite Icon => iconImage != null ? iconImage.sprite : null;

        private void OnEnable()
        {
            ApplyResource(resourceId);
            if (balanceProvider != null)
            {
                Bind(balanceProvider);
                return;
            }

            if (ServiceLocator.TryGet<IResourceBalanceProvider>(out IResourceBalanceProvider provider))
            {
                Bind(provider);
                return;
            }

            if (ServiceLocator.TryGet<IResourceWallet>(out IResourceWallet wallet) &&
                wallet is IResourceBalanceProvider walletProvider)
            {
                Bind(walletProvider);
                return;
            }

            RefreshBalance();
        }

        private void OnDisable()
        {
            DetachBalanceSource();
            InvalidateRefreshDeferral();
        }

        private void OnDestroy() => Unbind();

        public void Bind(IResourceBalanceProvider provider)
        {
            Unbind();
            balanceProvider = provider;
            balanceSource = autoRefresh && isActiveAndEnabled ? provider as IResourceBalanceSource : null;
            if (balanceSource != null) balanceSource.BalanceChanged += OnBalanceChanged;
            RefreshBalance();
        }

        public void SetResource(string value)
        {
            InvalidateRefreshDeferral();
            resourceId = value;
            ApplyResource(value);
            RefreshBalance();
        }

        public void RefreshBalance()
        {
            if (refreshDeferral != null || amountText == null) return;
            long balance = balanceProvider?.GetBalance(currentResourceId) ?? 0L;
            amountText.text = ResourceAmountFormatter.Format(balance, amountFormat);
        }

        /// <summary>
        /// Holds balance text until all current scopes are disposed, then reads the latest
        /// provider balance. Create before mutating the wallet and dispose on completion or
        /// cancellation. Use on the Unity thread. Disable, destroy, Unbind, Bind and
        /// SetResource invalidate all outstanding scopes; old tokens then have no effect.
        /// </summary>
        /// <exception cref="InvalidOperationException">The holder is disabled or unbound.</exception>
        public IDisposable DeferRefresh()
        {
            if (!isActiveAndEnabled || balanceProvider == null)
                throw new InvalidOperationException("Bind and enable the resource holder before deferring refresh.");

            if (refreshDeferral == null) refreshDeferral = new RefreshDeferralState { Owner = this };
            refreshDeferral.Count++;
            return new RefreshDeferral(refreshDeferral);
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
                RefreshBalance();
            }
        }

        /// <summary>Detaches the provider and invalidates all outstanding refresh scopes.</summary>
        public void Unbind()
        {
            DetachBalanceSource();
            InvalidateRefreshDeferral();
            balanceProvider = null;
        }

        private void DetachBalanceSource()
        {
            if (balanceSource != null) balanceSource.BalanceChanged -= OnBalanceChanged;
            balanceSource = null;
        }

        private void InvalidateRefreshDeferral()
        {
            if (refreshDeferral == null) return;
            // Outstanding tokens must not retain this holder or touch a newer binding.
            refreshDeferral.Owner = null;
            refreshDeferral = null;
        }
    }
}
