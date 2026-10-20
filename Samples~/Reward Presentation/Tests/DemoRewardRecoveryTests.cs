using System;
using System.IO;
using System.Threading;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;
using Dreamy.Economy.UI;
using NUnit.Framework;

namespace Dreamy.Economy.RewardSample.Tests
{
    public sealed class DemoRewardRecoveryTests
    {
        private string directory;
        private DatasaveResourceWallet wallet;
        private DemoRewardService source;
        private DatasaveService Store() => new(new DatasaveOptions { DirectoryName=directory,CreateFileOnFirstLoad=false });
        private void Restart()
        {
            var save=Store();
            wallet=new DatasaveResourceWallet(save,rewardDefinitions:new[] {new ResourceRewardDefinition("item",new ResourceId("item.booster.hammer"))});
            source=new DemoRewardService(save,new RewardDeliveryGateway(new[] {wallet.CreateRewardDestination("currency"),wallet.CreateRewardDestination("item")}));
        }
        [SetUp] public void Setup() { directory=Path.Combine(Path.GetTempPath(),"DreamyRewardPresentation-"+Guid.NewGuid().ToString("N"));Restart(); }
        [TearDown] public void Cleanup() { if(Directory.Exists(directory))Directory.Delete(directory,true); }
        private RewardClaimSnapshot Query(RewardClaimReference reference) => source.QueryAsync(reference,CancellationToken.None).GetAwaiter().GetResult();
        private RewardClaimSnapshot Claim(RewardClaimReference reference) => source.ClaimAsync(new RewardClaimCommand(reference,Query(reference).Fingerprint)).GetAwaiter().GetResult();
        [Test] public void EarnAndQueryDoNotCreditWalletAndSurviveRestart()
        {
            var id=source.Earn();Assert.AreEqual(0,wallet.GetBalance(new ResourceId("currency.coin")));
            Restart();Assert.IsTrue(id.Equals(source.FirstUnclaimed));Assert.AreEqual(RewardClaimStatus.Available,Query(id).Status);
            Assert.AreEqual(0,wallet.GetBalance(new ResourceId("currency.coin")));
        }
        [Test] public void ClaimCreditsOnceAcrossAllOwnerRestarts()
        {
            var id=source.Earn();Assert.AreEqual(RewardClaimStatus.Claimed,Claim(id).Status);Restart();
            Assert.AreEqual(RewardClaimStatus.AlreadyClaimed,Claim(id).Status);
            Assert.AreEqual(100,wallet.GetBalance(new ResourceId("currency.coin")));Assert.AreEqual(1,wallet.GetBalance(new ResourceId("item.booster.hammer")));
        }
        [Test] public void FinalCommitFailureRecoversWithoutDuplicateEffects()
        {
            var id=source.Earn();source.FailNextFinalCommit=true;
            Assert.AreEqual(RewardClaimStatus.RequiresReload,Claim(id).Status);
            Assert.AreEqual(100,wallet.GetBalance(new ResourceId("currency.coin")));
            Restart();Assert.AreEqual(RewardClaimStatus.Pending,Query(id).Status);
            Assert.AreEqual(RewardClaimStatus.Claimed,Claim(id).Status);Restart();
            Assert.AreEqual(100,wallet.GetBalance(new ResourceId("currency.coin")));Assert.AreEqual(1,wallet.GetBalance(new ResourceId("item.booster.hammer")));
            Assert.AreEqual(RewardClaimStatus.AlreadyClaimed,Query(id).Status);
        }
        [Test] public void WrongFingerprintCannotConsumeEarnedReward()
        {
            var id=source.Earn();var result=source.ClaimAsync(new RewardClaimCommand(id,"wrong")).GetAwaiter().GetResult();
            Assert.AreEqual(RewardClaimStatus.Blocked,result.Status);Assert.AreEqual(0,wallet.GetBalance(new ResourceId("currency.coin")));
        }
        [Test] public void WrongProfileCannotClaim()
        {
            var id=source.Earn();var other=new RewardClaimReference("other",id.SourceId,id.ClaimId);
            Assert.AreEqual(RewardClaimStatus.Unavailable,Query(other).Status);Assert.AreEqual(0,wallet.GetBalance(new ResourceId("currency.coin")));
        }
        [Test] public void TwoRunsKeepSeparateEntitlements()
        {
            var first=source.Earn(100);var second=source.Earn(200);Claim(second);Restart();
            Assert.IsTrue(first.Equals(source.FirstUnclaimed));Claim(first);Assert.AreEqual(300,wallet.GetBalance(new ResourceId("currency.coin")));
        }
        [Test] public void RepeatedNoticeConstructionHasNoDeliveryEffect()
        {
            var id=source.Earn();var completed=Claim(id);
            for(int i=0;i<20;i++)RewardPresentationRequest.Notice(completed);
            Restart();Assert.AreEqual(100,wallet.GetBalance(new ResourceId("currency.coin")));
        }
    }
}
