using Dreamy.Economy.Rewards;
using Dreamy.Economy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Economy.RewardSample
{
    public sealed class RewardItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Image icon;
        public void Bind(ResolvedRewardEntry entry,ResourceDisplayCatalog catalog)
        {
            ResourceDisplayDefinition definition=null;
            if(catalog != null && ResourceId.TryParse(entry.TargetId,out var id)) catalog.TryGet(id,out definition);
            nameText.text=definition?.DisplayName ?? entry.TargetId;
            amountText.text=ResourceAmountFormatter.Format(entry.Amount,definition?.AmountFormat ?? ResourceAmountFormat.Grouped);
            icon.sprite=definition?.Icon;
            icon.enabled=icon.sprite != null;
        }
    }
}
