## Single-resource suggestion panels

Default Coin/Gem sample flow uses one prefab per resource with Shop/Later, plain color UI and
no icons. See [prefab structure and wiring](ResourceSuggestions/README.md).
`ResourceSuggestionPanelPresentation<TPanel>` closes before navigation and never auto-spends on return.
The multi-deficit panel remains available as a fallback.

# Resource Recovery demo

Open `ResourceRecoveryDemo.unity` and enter Play Mode. If Dreamy Editor Tools uses a bootstrap start scene, temporarily select this demo as the Play start scene.

Try silent spend, prompted spend, multi-resource costs, and direct Gem navigation. The destination demonstrates semantic Coin/Gem tabs and **does not simulate purchases**. Return and use **DEMO: add 100 Coin + 10 Gem** to fund the action; confirm again to spend. The demo wallet persists under `DreamyRecoveryDemo` and is separate from the game's profile.

For production inject the game's existing wallet, owner token, display catalog and `IResourceAcquisitionGateway`. Dispose the presenter with its owner. No automatic retry/resume is enabled. The reusable panel is a BaseFeaturePanel prefab variant using UIPanel/PanelManager, with inherited safe area/tweens, direct row references, scrollable deficits and Back/Return support. The scene supplies PanelManager on its Canvas and an EventSystem.

Dependencies are provided by Economy 0.4.0: Datasave, Dreamy UI 1.0.5, UniTask 2.5.10 and uGUI 2.0.0. A standalone scene and Editor-generated prefabs are included. For integration details see `Documentation~/resource-recovery.md` in the Economy package. Android/iOS, external fresh consumer, localization/theme and real billing require host validation.
