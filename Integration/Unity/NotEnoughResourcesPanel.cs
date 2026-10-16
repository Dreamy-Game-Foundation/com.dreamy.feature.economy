using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class NotEnoughResourcesPanel : UIPanel, INotEnoughResourcesView
    {
        [SerializeField] private Transform rowContainer;
        [SerializeField] private ResourceDeficitRow rowPrefab;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private ResourceDisplayCatalog displayCatalog;
        private readonly List<ResourceDeficitRow> rows = new();
        private UniTaskCompletionSource closing;
        private int version;
        private bool destroying;
        public override bool CanBack => !opening;
        public override bool CanCache => true;
        public override UILayer Layer => UILayer.Popup;
        private bool opening;
        public event Action<ResourceId> AcquireRequested;
        public event Action CancelRequested;
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            OnPreHide += Cancel;
            cancelButton.onClick.AddListener(Cancel);
        }
        protected override void OnDisable()
        {
            CancelRequested?.Invoke();
            base.OnDisable();
        }
        protected override void OnDestroy()
        {
            destroying = true;
            version++;
            OnPreHide -= Cancel;
            cancelButton.onClick.RemoveListener(Cancel);
            foreach (var row in rows) if (row != null) row.AcquireRequested -= Acquire;
            CancelRequested?.Invoke();
            base.OnDestroy();
        }
        private void Cancel() => CancelRequested?.Invoke();
        private void Acquire(ResourceId id) => AcquireRequested?.Invoke(id);
        public void Open() => OpenAsync(++version).Forget(exception => { Debug.LogException(exception, this); Cancel(); });

        private async UniTask OpenAsync(int expectedVersion)
        {
            if (closing != null) await closing.Task;
            if (destroying || version != expectedVersion) return;
            await Show();
            if (!destroying && version == expectedVersion) ApplyNavigationVisibility();
        }

        public void Close() => CloseAsync().Forget(exception => Debug.LogException(exception, this));

        public UniTask CloseAsync()
        {
            version++;
            if (destroying) return UniTask.CompletedTask;
            if (closing != null && closing.Task.Status == UniTaskStatus.Pending) return closing.Task;
            // Publish the shared completion before Hide: OnPreHide can re-enter CloseAsync.
            var completion = new UniTaskCompletionSource();
            closing = completion;
            CompleteCloseAsync(completion).Forget();
            return completion.Task;
        }

        private async UniTask CompleteCloseAsync(UniTaskCompletionSource completion)
        {
            // Exactly one consumer awaits Hide. CompletionSource supports simultaneous callers.
            try
            {
                await Hide();
                completion.TrySetResult();
            }
            catch (OperationCanceledException exception)
            {
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        private void ApplyNavigationVisibility()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            canvasGroup.alpha = opening ? 0 : 1;
            canvasGroup.interactable = canvasGroup.blocksRaycasts = !opening;
            if (!PanelManager.HasInstance || !gameObject.activeInHierarchy) return;
            if (opening) PanelManager.Instance.MarkHidden(this);
            else PanelManager.Instance.MarkShown(this);
        }

        public void Render(IReadOnlyList<ResourceDeficit> deficits, Func<ResourceId, ResourceAcquisitionAvailability> availability,
            bool opening, ResourceAcquisitionResult? lastNavigation)
        {
            this.opening = opening;
            // Keep the session subscribed while the destination owns input and Back.
            ApplyNavigationVisibility();
            while (rows.Count < deficits.Count)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.AcquireRequested += Acquire;
                rows.Add(row);
            }
            bool hasRoute = false;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(i < deficits.Count);
                if (i >= deficits.Count) continue;
                bool available = availability(deficits[i].ResourceId) != ResourceAcquisitionAvailability.Unavailable;
                hasRoute |= available;
                rows[i].Render(deficits[i], displayCatalog, available, opening);
            }
            statusText.text = deficits.Count == 0 ? "Resources ready. Return and confirm your action."
                : lastNavigation == ResourceAcquisitionResult.Failed || lastNavigation == ResourceAcquisitionResult.NoMatchingDestination
                    ? "Could not open a destination. You can return or try again."
                : !hasRoute ? "No way to acquire these resources here. You can return."
                : "Choose a resource to acquire, or return.";
        }
    }
}
