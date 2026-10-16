namespace Dreamy.Economy
{
    // Optional capability. Custom legacy wallets need not implement it.
    public interface IResourceTransactionWallet
    {
        ResourceTransactionResult Spend(ResourceSpendRequest request);
        ResourceTransactionResult Exchange(ResourceTransactionRequest request);
    }
}
