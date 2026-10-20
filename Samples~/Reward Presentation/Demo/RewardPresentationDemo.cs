using System;
using Cysharp.Threading.Tasks;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;
using Dreamy.Economy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.RewardSample
{
    public sealed class RewardPresentationDemo : MonoBehaviour
    {
        [SerializeField] private RewardPanel panel;
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button earnButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button silentButton;
        [SerializeField] private Button noticeButton;
        [SerializeField] private Button reloadButton;
        [SerializeField] private Button faultButton;
        private DatasaveService save;
        private DatasaveResourceWallet wallet;
        private DemoRewardService source;
        private RewardPresentationQueue presentation;
        private RewardClaimSnapshot lastCompleted;
        private bool ready;
        public DemoRewardService Source => source;
        public DatasaveResourceWallet Wallet => wallet;
        private void Start() => Initialize().Forget(Debug.LogException);
        private async UniTask Initialize()
        {
            await UniTask.NextFrame(cancellationToken:this.GetCancellationTokenOnDestroy());
            save=new DatasaveService(new DatasaveOptions{DirectoryName="DreamyRewardPresentationDemo"});
            wallet=new DatasaveResourceWallet(save,rewardDefinitions:new[] {new ResourceRewardDefinition("item",new ResourceId("item.booster.hammer"))});
            wallet.BalanceChanged += BalanceChanged;
            source=new DemoRewardService(save,new RewardDeliveryGateway(new[] {wallet.CreateRewardDestination("currency"),wallet.CreateRewardDestination("item")}));
            presentation=RewardPresentationInstaller.Install(DemoRewardService.Scope,panel);
            presentation.Diagnostic += Debug.LogException;
            earnButton.onClick.AddListener(Earn); confirmButton.onClick.AddListener(Confirm);
            silentButton.onClick.AddListener(Silent); noticeButton.onClick.AddListener(Notice);
            reloadButton.onClick.AddListener(Reload); faultButton.onClick.AddListener(Fault);
            ready=true; Refresh("Earn a reward, then choose how to receive it. Demo saves persist.");
        }
        private void BalanceChanged(ResourceBalanceChanged _) => Refresh();
        private void Earn() { source.Earn(); Refresh("Earned entitlement saved. Balance has not increased."); }
        private void Confirm() => ConfirmAsync().Forget(Debug.LogException);
        private async UniTask ConfirmAsync()
        {
            var reference=source.FirstUnclaimed ?? source.Latest;
            if(reference == null) { Refresh("Press Earn first."); return; }
            var result=await presentation.OpenAsync(RewardPresentationRequest.Confirm(reference),source,this.GetCancellationTokenOnDestroy());
            if(this == null) return;
            if(result.Snapshot?.IsCompleted == true) lastCompleted=result.Snapshot;
            Refresh("UI: "+result.Outcome+" / claim: "+result.Observation);
        }
        private void Silent() => SilentAsync().Forget(Debug.LogException);
        private async UniTask SilentAsync()
        {
            var reference=source.FirstUnclaimed;
            if(reference == null) { Refresh("Press Earn first."); return; }
            var result=await RewardClaimFlow.ClaimAsync(reference,source,uiCancellationToken:this.GetCancellationTokenOnDestroy());
            if(this == null) return;
            if(result.IsCompleted) lastCompleted=result;
            Refresh("No panel. Claim: "+result.Status);
        }
        private void Notice() => NoticeAsync().Forget(Debug.LogException);
        private async UniTask NoticeAsync()
        {
            if(lastCompleted == null) { Refresh("Claim a reward first. Notice never grants."); return; }
            await presentation.OpenAsync(RewardPresentationRequest.Notice(lastCompleted),uiCancellationToken:this.GetCancellationTokenOnDestroy());
            if(this != null) Refresh("Notice closed. No extra grant.");
        }
        private void Reload()
        {
            wallet.Reload(); source.Reload(); Refresh("Durable owners reloaded. Open reward to resume pending claim.");
        }
        private void Fault() { source.FailNextFinalCommit=true; Refresh("Next final claim commit will fail; reload and resume without a duplicate grant."); }
        private void Refresh(string message=null)
        {
            if(!ready) return;
            balanceText.text="COINS  "+wallet.GetBalance(new ResourceId("currency.coin"))+"     HAMMERS  "+wallet.GetBalance(new ResourceId("item.booster.hammer"));
            if(message != null) statusText.text=message;
        }
        private void OnDestroy()
        {
            presentation?.Dispose();
            if(wallet != null) wallet.BalanceChanged -= BalanceChanged;
            earnButton.onClick.RemoveListener(Earn); confirmButton.onClick.RemoveListener(Confirm);
            silentButton.onClick.RemoveListener(Silent); noticeButton.onClick.RemoveListener(Notice);
            reloadButton.onClick.RemoveListener(Reload); faultButton.onClick.RemoveListener(Fault);
        }
    }
}
