# One suggestion prefab per resource

Use `ResourceSuggestionPanel.prefab` as the shared layout base. `CoinSuggestionPanel.prefab`
and `GemSuggestionPanel.prefab` are variants. A game creates its own variants and authors the
message, labels, colors and resource ID. These are temporary plain uGUI Image/TMP controls;
there is no resource icon, imported art, custom frame or baked screenshot. Import TMP Essential
Resources first; the sample font uses the standard LiberationSans SDF asset GUID.

Hierarchy:

```
ResourceSuggestionPanel (UIPanel, CanvasGroup, UITweenPlayer)
  Backdrop (input blocker, inherited)
  SafeArea (inherited)
    Content (centered adaptive layout)
      Dialog (Image, LayoutElement)
        InnerRim / Surface (Image)
        Message (TMP)
        ResourceName (TMP placeholder)
        Balance (TMP, optional)
        Actions (HorizontalLayoutGroup)
          Shop (Button, Image) / Label (TMP)
          Later (Button, Image) / Label (TMP)
```

Create one `ResourceSuggestionPanelPresentation(prefab, sharedBalances, acquisition)` per
resource; register them in `ResourceRecoveryPresentationRouter`. The router selects one mapped
currently missing resource in normalized cost order. No automatic sequence of popups is shown
for multi-cost actions. A subsequent confirmed attempt can show the next missing resource.

The presenter snapshots/normalizes costs, refreshes the selected balance, blocks duplicate
requests and ignores Shop when funded/unavailable. Shop detaches view listeners and awaits
close before opening the acquisition gateway with the selected ResourceId, current missing
amount, context and all current deficits. Later/Back closes without navigation. Owner
cancellation closes UI/cancels pending navigation. Return never auto-retries spend or grants.

All variants use the same component type. Presentation deliberately creates the chosen prefab
per opening, calls Init/PostInit, and lets PanelManager own Popup layer/stack/Back. It does not
use Create<T>'s type-only cache, which could substitute Coin for Gem. Panels are not cached;
closing destroys them. Keep the router at the composition root to serialize replacement.

Sandbox: GameInstaller.resourceSuggestionPrefabs contains game-owned Coin/Gem variants under
Assets/_Project/Prefabs/UI/ResourceSuggestions. Custom presentation components remain supported;
duplicate resource IDs across both maps fail configuration. The standalone recovery demo now
routes Coin/Gem to these panels; the older multi-deficit UI remains an optional fallback.

The builder menu `Dreamy/Economy/Create Single Resource Suggestion Prefab` creates a new base
under Assets and refuses to overwrite by default. `CreateVariant` creates a resource variant.
Replace placeholder colors/text in game-owned variants. Views only publish intent; persistence,
purchasing, navigation and localization ownership remain with the host.
