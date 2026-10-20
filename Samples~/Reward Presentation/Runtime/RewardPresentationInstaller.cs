using Dreamy.Economy.UI;

namespace Dreamy.Economy.RewardSample
{
    public static class RewardPresentationInstaller
    {
        // Queue scope/lifetime belongs to the root. Never call this for each panel opening.
        public static RewardPresentationQueue Install(string profileScope,RewardPanel panel,int capacity=16) =>
            new(profileScope,new RewardPanelPresentation(panel),capacity);
    }
}
