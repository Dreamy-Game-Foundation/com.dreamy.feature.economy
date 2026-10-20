using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;
using Dreamy.Economy.UI;

namespace Dreamy.Economy.RewardSample
{
    // Sample feature owner, not a generic production reward authority.
    public sealed class DemoRewardService : IRewardClaimSource
    {
        public const string Scope = "reward-demo";
        public const string SaveKey = "earned-rewards";
        private readonly IDatasaveService save;
        private readonly IRewardDeliveryGateway gateway;
        private DemoRewardSaveData data;
        private bool busy, requiresReload;
        public bool FailNextFinalCommit { get; set; }
        public DemoRewardService(IDatasaveService save, IRewardDeliveryGateway gateway)
        { this.save = save; this.gateway = gateway; Reload(); }
        public void Reload()
        {
            if (busy) throw new InvalidOperationException("Demo source busy.");
            data = save.Load<DemoRewardSaveData>(SaveKey).Clone();
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var record in data.Claims)
                if(record == null || string.IsNullOrWhiteSpace(record.Id) || record.Coins <= 0 || !ids.Add(record.Id) ||
                    (record.Journal != null && !record.Journal.IsValid()) ||
                    (record.Claimed != (record.Journal?.State == RewardJournalState.Completed)))
                    throw new InvalidOperationException("Invalid demo claim save.");
            requiresReload = false;
        }
        public RewardClaimReference Earn(long coins = 100)
        {
            if (busy || requiresReload) throw new InvalidOperationException("Reload before earning another reward.");
            if (coins <= 0) throw new ArgumentOutOfRangeException(nameof(coins));
            var candidate = data.Clone();
            var record = new DemoRewardRecord { Id = Guid.NewGuid().ToString("N"), Coins = coins };
            candidate.Claims.Add(record); Commit(candidate);
            return Reference(record.Id);
        }
        public RewardClaimReference Latest => data.Claims.Count == 0 ? null : Reference(data.Claims[data.Claims.Count - 1].Id);
        public RewardClaimReference FirstUnclaimed
        {
            get { foreach(var record in data.Claims) if(!record.Claimed) return Reference(record.Id); return null; }
        }
        private static RewardClaimReference Reference(string id) => new(Scope,"completion",id);
        private DemoRewardRecord Find(RewardClaimReference reference)
        {
            if(reference == null || reference.Scope != Scope || reference.SourceId != "completion") return null;
            return data.Claims.Find(r => r.Id == reference.ClaimId);
        }
        private static ResolvedRewardBundle Bundle(DemoRewardRecord record) => new(1,"demo-v1",new[]
        {
            new ResolvedRewardEntry("coin","currency","currency.coin",record.Coins),
            new ResolvedRewardEntry("booster","item","item.booster.hammer",1)
        });
        private static string Fingerprint(DemoRewardRecord record) => RewardIdentity.Fingerprint("demo-entitlement-v1",record.Id,Bundle(record).Fingerprint);
        private RewardClaimSnapshot State(RewardClaimReference reference)
        {
            var record=Find(reference);
            if(record == null) return new RewardClaimSnapshot(reference,"missing",null,RewardClaimStatus.Unavailable,"UnknownClaim");
            return new RewardClaimSnapshot(reference,Fingerprint(record),Bundle(record),
                requiresReload ? RewardClaimStatus.RequiresReload : record.Claimed ? RewardClaimStatus.AlreadyClaimed :
                record.Journal != null ? RewardClaimStatus.Pending : RewardClaimStatus.Available,
                requiresReload ? "Reload required" : null, canRetry: record.Journal != null && !requiresReload);
        }
        public UniTask<RewardClaimSnapshot> QueryAsync(RewardClaimReference reference,CancellationToken token)
        { token.ThrowIfCancellationRequested(); return UniTask.FromResult(State(reference)); }
        public UniTask<RewardClaimSnapshot> ClaimAsync(RewardClaimCommand command) => UniTask.FromResult(Claim(command));
        private RewardClaimSnapshot Claim(RewardClaimCommand command)
        {
            var state=State(command.Reference);
            if(state.Fingerprint != command.ExpectedFingerprint)
                return new RewardClaimSnapshot(command.Reference,command.ExpectedFingerprint,state.Bundle,RewardClaimStatus.Blocked,"PayloadMismatch");
            if(requiresReload || state.IsCompleted || state.Status == RewardClaimStatus.Unavailable) return state;
            if(busy) return new RewardClaimSnapshot(command.Reference,state.Fingerprint,state.Bundle,RewardClaimStatus.Busy,"Busy",true);
            busy=true;
            try
            {
                var record=Find(command.Reference);
                var requestId=RewardIdentity.Request(Scope,"demo","level-completion",record.Id,"reward");
                var coordinator=new RewardDeliveryCoordinator(gateway,record.Journal);
                Action<RewardJournal> persist=journal =>
                {
                    var candidate=data.Clone(); candidate.Claims.Find(r=>r.Id == record.Id).Journal=journal.Clone(); Commit(candidate);
                };
                var result=coordinator.Begin(requestId,state.Fingerprint,persist);
                if(result.IsSuccess) result=coordinator.Resolve(state.Bundle,persist);
                if(result.IsSuccess) result=coordinator.Deliver(persist);
                if(result.IsSuccess) result=coordinator.Complete(journal =>
                {
                    if(FailNextFinalCommit) { FailNextFinalCommit=false; throw new InvalidOperationException("Injected final-commit failure"); }
                    var candidate=data.Clone(); var next=candidate.Claims.Find(r=>r.Id == record.Id);
                    next.Journal=journal.Clone(); next.Claimed=true; Commit(candidate);
                });
                if(result.IsSuccess) return new RewardClaimSnapshot(command.Reference,state.Fingerprint,state.Bundle,RewardClaimStatus.Claimed);
                if(result.Reason == RewardDeliveryReason.PersistenceFailed) requiresReload=true;
                return new RewardClaimSnapshot(command.Reference,state.Fingerprint,state.Bundle,
                    requiresReload ? RewardClaimStatus.RequiresReload : result.Retryable ? RewardClaimStatus.Pending : RewardClaimStatus.Blocked,
                    result.Reason.ToString(), result.Retryable && !requiresReload);
            }
            finally { busy=false; }
        }
        private void Commit(DemoRewardSaveData candidate)
        {
            try { save.Save(candidate,SaveKey); }
            catch { requiresReload=true; throw; }
            data=candidate;
        }
    }
}
