using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.RewardSample.Editor
{
    public static class RewardPresentationSmoke
    {
        public static async UniTask RunAsync(int cycles=100)
        {
            SessionState.SetString("RewardPresentation.Smoke","running");
            try
            {
                if(!Application.isPlaying)throw new InvalidOperationException("Open the demo in Play Mode first.");
                var demo=UnityEngine.Object.FindFirstObjectByType<RewardPresentationDemo>();
                var panel=UnityEngine.Object.FindFirstObjectByType<RewardPanel>(FindObjectsInactive.Include);
                var reference=demo.Source.Latest ?? demo.Source.Earn();
                var state=await demo.Source.QueryAsync(reference,CancellationToken.None);
                if(!state.IsCompleted)state=await demo.Source.ClaimAsync(new RewardClaimCommand(reference,state.Fingerprint));
                if(!state.IsCompleted)throw new InvalidOperationException("Resolve pending demo claim before running lifecycle smoke.");
                int before=panel.GetComponentsInChildren<RewardItemView>(true).Length;
                using(var queue=RewardPresentationInstaller.Install(DemoRewardService.Scope,panel))
                {
                    for(int i=0;i<cycles;i++)
                    {
                        var open=queue.OpenAsync(RewardPresentationRequest.Notice(state));
                        await UniTask.Yield();
                        if(ListenerCount(panel,"ClaimRequested")!=1)throw new InvalidOperationException("Duplicate/missing active presenter at cycle "+i+": "+ListenerCount(panel,"ClaimRequested"));
                        foreach(var button in panel.GetComponentsInChildren<Button>())if(button.name=="Close")button.onClick.Invoke();
                        var result=await open;
                        if(result.Observation!=RewardClaimObservation.Completed)throw new InvalidOperationException("Notice lost completion.");
                        if(ListenerCount(panel,"ClaimRequested")!=0)throw new InvalidOperationException("Leaked presenter subscription.");
                    }
                }
                int after=panel.GetComponentsInChildren<RewardItemView>(true).Length;
                if(after>Math.Max(before,state.Bundle.Entries.Count))throw new InvalidOperationException("Row count grew across reopen.");
                SessionState.SetString("RewardPresentation.Smoke","PASS cycles="+cycles+" rows="+after+" listeners=0");
            }
            catch(Exception exception){SessionState.SetString("RewardPresentation.Smoke","FAIL "+exception);throw;}
        }
        private static int ListenerCount(RewardPanel panel,string name)
        {
            var field=typeof(RewardPanel).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            return (field.GetValue(panel) as Delegate)?.GetInvocationList().Length ?? 0;
        }
    }
}
