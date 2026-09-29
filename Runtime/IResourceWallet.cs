namespace Dreamy.Economy
{
    public interface IResourceWallet
    {
        bool TryGrant(ResourceGrantRequest request);
    }
}
