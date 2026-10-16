using System;
using Cysharp.Threading.Tasks;
using Dreamy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.UI
{
    // Message, colors and button labels are authored in the resource prefab variant.
    public class ResourceSuggestionPanel : UIPanel, IResourceSuggestionView
    {
        [SerializeField] private string resourceId;
        [SerializeField] private Button acquireButton;
        [SerializeField] private Button laterButton;
        [SerializeField] private TMP_Text balanceText;
        private UniTaskCompletionSource closing;
        private bool destroying;
        private bool wired;
        public ResourceId ResourceId => new(resourceId);
        public override UILayer Layer => UILayer.Popup;
        public override bool CanBack => true;
        public override bool CanCache => false;
        public event Action AcquireRequested;
        public event Action LaterRequested;

        public ResourceSuggestionRequest Request { get; private set; }
        public void Prepare(ResourceSuggestionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.ResourceId != ResourceId) throw new ArgumentException("Suggestion panel/resource mismatch.", nameof(request));
            Request = request;
        }

        protected virtual void Awake() => Wire();
        private void Wire()
        {
            if (wired) return;
            wired = true;
            acquireButton.onClick.AddListener(Acquire);
            laterButton.onClick.AddListener(Later);
            OnPreHide += Hiding;
            OnPostHide += Hidden;
        }
        private void Hiding()
        {
            closing ??= new UniTaskCompletionSource();
            Later();
        }
        private void Hidden() => closing?.TrySetResult();
        private void Acquire() => AcquireRequested?.Invoke();
        private void Later() => LaterRequested?.Invoke();
        public void Open()
        {
            Wire();
            Show().Forget(exception => { Debug.LogException(exception, this); Later(); });
        }
        public void Render(ResourceSuggestionState state)
        {
            if (state.ResourceId != ResourceId) throw new ArgumentException("Suggestion prefab/resource mismatch.");
            acquireButton.interactable = state.CanAcquire;
            if (balanceText != null)
                balanceText.text = state.Missing == 0 ? "Resources ready. Return and confirm your action."
                    : state.Availability == ResourceAcquisitionAvailability.Unavailable ? "No destination available."
                    : $"{ResourceAmountFormatter.Format(state.Available, ResourceAmountFormat.Grouped)} / {ResourceAmountFormatter.Format(state.Required, ResourceAmountFormat.Grouped)}";
        }
        public UniTask CloseAsync()
        {
            if (destroying || this == null) return UniTask.CompletedTask;
            if (closing != null) return closing.Task;
            closing = new UniTaskCompletionSource();
            CompleteCloseAsync(closing).Forget();
            return closing.Task;
        }
        private async UniTask CompleteCloseAsync(UniTaskCompletionSource completion)
        {
            try { await Hide(); completion.TrySetResult(); }
            catch (OperationCanceledException exception) { completion.TrySetCanceled(exception.CancellationToken); }
            catch (Exception exception) { completion.TrySetException(exception); }
        }
        protected override void OnDisable() { base.OnDisable(); Later(); }
        protected override void OnDestroy()
        {
            destroying = true;
            if (wired)
            {
                acquireButton.onClick.RemoveListener(Acquire);
                laterButton.onClick.RemoveListener(Later);
                OnPreHide -= Hiding;
                OnPostHide -= Hidden;
            }
            Later();
            closing?.TrySetResult();
            base.OnDestroy();
        }
    }
}
