using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreamy.Economy.UI
{
    [CreateAssetMenu(menuName = "Dreamy/Economy/Resource Display Catalog", fileName = "ResourceDisplayCatalog")]
    public sealed class ResourceDisplayCatalog : ScriptableObject
    {
        [SerializeField] private List<ResourceDisplayDefinition> resources = new();

        public IReadOnlyList<ResourceDisplayDefinition> Resources => resources;

        public bool TryGet(ResourceId resourceId, out ResourceDisplayDefinition definition)
        {
            foreach (ResourceDisplayDefinition candidate in resources)
            {
                if (candidate != null && candidate.Matches(resourceId))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }

    [Serializable]
    public sealed class ResourceDisplayDefinition
    {
        [SerializeField] private string resourceId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private Color iconColor = Color.white;
        [SerializeField] private ResourceAmountFormat amountFormat = ResourceAmountFormat.Compact;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? resourceId : displayName;
        public string Id => resourceId;
        public Sprite Icon => icon;
        public Color IconColor => iconColor;
        public ResourceAmountFormat AmountFormat => amountFormat;

        internal bool Matches(ResourceId id) =>
            string.Equals(resourceId, id.Value, StringComparison.Ordinal);
    }

    public enum ResourceAmountFormat
    {
        Raw,
        Grouped,
        Compact
    }
}
