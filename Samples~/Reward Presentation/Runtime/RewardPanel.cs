using System;
using System.Collections.Generic;
using Dreamy.Economy.UI;
using Dreamy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.RewardSample
{
    public sealed class RewardPanel : UIPanel,IRewardPresentationView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform itemContainer;
        [SerializeField] private RewardItemView itemPrefab;
        [SerializeField] private ResourceDisplayCatalog displayCatalog;
        private readonly List<RewardItemView> items=new();
        public override bool CanBack => true;
        public override bool CanCache => true;
        public override UILayer Layer => UILayer.Popup;
        public event Action ClaimRequested;
        public event Action CloseRequested;
        public event Action Detached;
        private void OnEnable() { claimButton.onClick.AddListener(Claim); closeButton.onClick.AddListener(CloseRequestedAction); }
        protected override void OnDisable()
        {
            claimButton.onClick.RemoveListener(Claim); closeButton.onClick.RemoveListener(CloseRequestedAction);
            Detached?.Invoke(); base.OnDisable();
        }
        private void Claim() => ClaimRequested?.Invoke();
        private void CloseRequestedAction() => CloseRequested?.Invoke();
        public void Prepare(RewardPresentationRequest request)
        {
            titleText.text=request.Title; statusText.text="Loading rewards..."; claimButton.gameObject.SetActive(false); closeButton.interactable=false;
            foreach(var item in items) item.gameObject.SetActive(false);
        }
        public void Render(RewardPresentationState state)
        {
            titleText.text=state.Request.Title; closeButton.interactable=true;
            closeButton.GetComponentInChildren<TMP_Text>().text=state.Snapshot?.IsCompleted == true ? "CONTINUE" : "CLOSE";
            var bundle=state.Snapshot?.Bundle;
            int count=bundle?.Entries.Count ?? 0;
            while(items.Count < count) items.Add(Instantiate(itemPrefab,itemContainer));
            for(int i=0;i<items.Count;i++)
            { items[i].gameObject.SetActive(i<count); if(i<count) items[i].Bind(bundle.Entries[i],displayCatalog); }
            claimButton.gameObject.SetActive(state.CanClaim || state.IsExecuting);
            claimButton.interactable=state.CanClaim;
            claimLabel.text=state.Snapshot?.CanRetry == true ? "RETRY" : "CLAIM REWARDS";
            statusText.text=state.Error ?? (state.IsLoading ? "Loading rewards..." : state.IsExecuting ? "Saving rewards..." :
                state.Snapshot?.IsCompleted == true ? "Rewards received. You can continue." :
                state.Snapshot?.Reason ?? (state.Snapshot?.Status == RewardClaimStatus.Available ? "Tap Claim to receive these rewards." : state.Snapshot?.Status.ToString()));
        }
    }
}
