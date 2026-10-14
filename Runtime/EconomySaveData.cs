using System;
using System.Collections.Generic;
using Dreamy.Datasave;

namespace Dreamy.Economy
{
    [Serializable]
    public sealed class EconomySaveData : SaveData
    {
        public override int Version => 2;
        public List<ResourceBalanceSaveEntry> Balances = new();
        public List<string> TransactionIds = new();
        public List<RewardReceiptSaveEntry> DeliveryReceipts = new();
        public List<string> LegacyDeliveryTombstones = new();

        public override void OnAfterLoad()
        {
            Balances ??= new List<ResourceBalanceSaveEntry>();
            TransactionIds ??= new List<string>();
            DeliveryReceipts ??= new List<RewardReceiptSaveEntry>();
            LegacyDeliveryTombstones ??= new List<string>();
        }
    }

    [Serializable]
    public sealed class RewardReceiptSaveEntry
    {
        public string TransactionId;
        public string Fingerprint;
    }

    [Serializable]
    public sealed class ResourceBalanceSaveEntry
    {
        public string ResourceId;
        public long Amount;
    }
}
