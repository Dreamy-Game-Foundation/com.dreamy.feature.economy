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

Register each concrete panel with `ResourceSuggestionInstaller.Install<TPanel>(factory,
resourceId, address, sharedBalances, acquisition)`. Register the returned presentation in
`ResourceRecoveryPresentationRouter`. The installer holds service dependencies and addresses;
it does not hold prefab references. Register game-owned prefabs in Addressables under those keys.

`CoinSuggestionPanel` and `GemSuggestionPanel` inherit the shared view. They have distinct types
because both `PanelManager` and `PanelPresenterFactory` identify views by their concrete type.
Do not register two resource prefabs using the same concrete view type.

The presentation snapshots/normalizes costs into a `ResourceSuggestionRequest`, creates the view
through `PanelManager.Create<TPanel>(address)`, prepares the request before `Show`, and awaits the
choice. The factory creates a `ResourceSuggestionPresenter : IPanelPresenter` for that opening.
The presenter renders balances and handles Acquire/Later intent; it detaches when the UI host
disposes it on Hide/disable/destroy. The one-shot request preserves an accepted Acquire through
presenter disposal. The presentation awaits close before opening acquisition, recalculates all
current deficits, and passes the selected resource, missing amount and source context.

Later/Back closes without navigation. Owner cancellation closes UI/cancels pending navigation.
Return never auto-retries spend or grants. Panels are not cached; closing destroys them through
UIPanel. The router serializes replacement requests, even when they select different resources.

The sandbox registers `Panel/CoinSuggestionPanel.prefab` and `Panel/GemSuggestionPanel.prefab`
for the game-owned variants under `Assets/_Project/Prefabs/UI/ResourceSuggestions`.
`GameInstaller` composes these routes and registers presenters without prefab suggestion fields.
Custom presentation components remain supported; duplicate resource IDs fail configuration.
The standalone recovery demo owns its own factory and uses the same addresses. Importing the
sample alone does not register Addressables entries; configure these keys for a standalone consumer.

For authored changes, edit the prefab variants through Unity MCP and preserve their base layout,
SafeArea, serialized bindings and GUIDs. `Tools/Dreamy/Sandbox/Validate Resource Suggestion Panels`
only validates typed project prefabs and addresses; it does not generate or attach prefabs.
