using System;

namespace Dreamy.Economy
{
    [Serializable]
    public sealed class ResourceTransactionReceipt
    {
        public int SchemaVersion = 1;
        public string TransactionId;
        public string Kind;
        public string Fingerprint;
    }
}
