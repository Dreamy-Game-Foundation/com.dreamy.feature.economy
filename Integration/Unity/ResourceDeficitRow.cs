using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.UI
{
    public sealed class ResourceDeficitRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Button acquireButton;
        private ResourceId resource;
        public event Action<ResourceId> AcquireRequested;
        private void Awake() => acquireButton.onClick.AddListener(Acquire);
        private void OnDestroy() => acquireButton.onClick.RemoveListener(Acquire);
        private void Acquire() => AcquireRequested?.Invoke(resource);

        public void Render(ResourceDeficit deficit, ResourceDisplayCatalog catalog, bool canAcquire, bool opening)
        {
            resource = deficit.ResourceId;
            ResourceDisplayDefinition display = null;
            if (catalog != null) catalog.TryGet(resource, out display);
            var format = display?.AmountFormat ?? ResourceAmountFormat.Grouped;
            string Format(long amount) => ResourceAmountFormatter.Format(amount, format);
            icon.sprite = display?.Icon;
            icon.enabled = icon.sprite != null;
            amountText.text = $"{display?.DisplayName ?? resource.Value}\n{Format(deficit.Available)} / {Format(deficit.Required)}  (need {Format(deficit.Missing)})";
            acquireButton.gameObject.SetActive(canAcquire);
            acquireButton.interactable = !opening;
        }
    }
}
