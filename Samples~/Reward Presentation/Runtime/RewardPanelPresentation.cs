using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.Economy.UI;
using Dreamy.UI;

namespace Dreamy.Economy.RewardSample
{
    // Inject a game-owned scene panel, or replace this host with an addressable/factory adapter.
    public sealed class RewardPanelPresentation : IRewardPresentationHost
    {
        private readonly RewardPanel panel;
        public RewardPanelPresentation(RewardPanel panel) { this.panel=panel; }
        public async UniTask<IRewardPresentationView> OpenAsync(RewardPresentationRequest request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(panel == null || !PanelManager.HasInstance) return null;
            bool interrupted=false;
            Action hiding=()=>interrupted=true;
            panel.OnPreHide += hiding;
            try
            {
                panel.Prepare(request); await panel.Show(); token.ThrowIfCancellationRequested();
                return interrupted || !panel.isActiveAndEnabled ? null : panel;
            }
            finally { panel.OnPreHide -= hiding; }
        }
        public async UniTask CloseAsync(IRewardPresentationView view)
        { if(panel != null) await panel.Hide(); }
    }
}
