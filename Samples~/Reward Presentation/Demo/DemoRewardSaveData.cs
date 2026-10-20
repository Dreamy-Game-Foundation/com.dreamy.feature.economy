using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;

namespace Dreamy.Economy.RewardSample
{
    [Serializable]
    public sealed class DemoRewardSaveData : SaveData
    {
        public List<DemoRewardRecord> Claims = new();
        public override void OnAfterLoad() { Claims ??= new List<DemoRewardRecord>(); }
        public DemoRewardSaveData Clone()
        {
            var copy = new DemoRewardSaveData();
            foreach (var record in Claims) copy.Claims.Add(record.Clone());
            return copy;
        }
    }
    [Serializable]
    public sealed class DemoRewardRecord
    {
        public string Id;
        public long Coins;
        public bool Claimed;
        public RewardJournal Journal;
        public DemoRewardRecord Clone() => new() { Id = Id, Coins = Coins, Claimed = Claimed, Journal = Journal?.Clone() };
    }
}
