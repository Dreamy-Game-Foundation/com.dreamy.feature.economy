using System;
using System.Collections.Generic;
using Dreamy.Datasave;

namespace Dreamy.Economy
{
    [Serializable]
    public sealed class EconomySaveData : SaveData
    {
        public List<ResourceBalanceSaveEntry> Balances = new();
        public List<string> TransactionIds = new();

        public override void OnAfterLoad()
        {
            Balances ??= new List<ResourceBalanceSaveEntry>();
            TransactionIds ??= new List<string>();
        }
    }

    [Serializable]
    public sealed class ResourceBalanceSaveEntry
    {
        public string ResourceId;
        public long Amount;
    }
}
