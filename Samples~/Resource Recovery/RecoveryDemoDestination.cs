using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.Samples
{
    // Demonstrates resource -> semantic tab selection. Navigation never mutates a wallet.
    public sealed class RecoveryDemoDestination : MonoBehaviour, IResourceAcquisitionGateway
    {
        [SerializeField] private Button coinTab;
        [SerializeField] private Button gemTab;
        [SerializeField] private Button returnButton;
        [SerializeField] private TMP_Text title;
        private UniTaskCompletionSource returned;
        private void Awake()
        {
            coinTab.onClick.AddListener(Coin); gemTab.onClick.AddListener(Gem); returnButton.onClick.AddListener(Return);
        }
        private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Return(); }
        private void Coin() => Select("Coin");
        private void Gem() => Select("Gem");
        private void Select(string name) => title.text = $"{name} tab (navigation demo)\nNo purchase is simulated here. Return to the demo and use its explicit grant button.";
        private void Return() => returned?.TrySetResult();
        public ResourceAcquisitionAvailability GetAvailability(ResourceId id) =>
            id.Value == "currency.coin" || id.Value == "currency.gem" ? ResourceAcquisitionAvailability.Available : ResourceAcquisitionAvailability.Unavailable;
        public async UniTask<ResourceAcquisitionResult> OpenAsync(ResourceAcquisitionRequest request, CancellationToken token)
        {
            if (GetAvailability(request.TargetResourceId) == ResourceAcquisitionAvailability.Unavailable) return ResourceAcquisitionResult.NoMatchingDestination;
            if (returned != null) return ResourceAcquisitionResult.Busy;
            returned = new UniTaskCompletionSource();
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
            try
            {
                lifetime.Token.ThrowIfCancellationRequested(); gameObject.SetActive(true);
                Select(request.TargetResourceId.Value == "currency.coin" ? "Coin" : "Gem");
                await returned.Task.AttachExternalCancellation(lifetime.Token);
                return ResourceAcquisitionResult.Returned;
            }
            finally { returned = null; if (this != null) gameObject.SetActive(false); }
        }
        private void OnDisable() => returned?.TrySetResult();
        private void OnDestroy()
        {
            coinTab.onClick.RemoveListener(Coin); gemTab.onClick.RemoveListener(Gem); returnButton.onClick.RemoveListener(Return);
            returned?.TrySetResult();
        }
    }
}
