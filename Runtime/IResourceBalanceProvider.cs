namespace Dreamy.Economy
{
    public interface IResourceBalanceProvider
    {
        long GetBalance(ResourceId resourceId);
    }
}
