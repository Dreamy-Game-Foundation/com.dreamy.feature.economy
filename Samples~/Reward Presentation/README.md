# Reward Presentation sample

Import from Economy 0.7.0 candidate via Package Manager > Samples > Reward Presentation. Open `Scenes/RewardPresentationDemo.unity` and play it standalone. Disable a host-specific “Play from Bootstrap” override when trying this scene; restore that preference afterwards.

Dependencies: Economy, Datasave, UI/Core/Assets and UniTask as resolved by package manifests; uGUI/TMP and imported **TMP Essential Resources**. The supplied scene was authored with the Input System UI module (`com.unity.inputsystem`); install it for this scene or replace its EventSystem module for your project's active input handling. No Daily/Missions/Shop package or sandbox `_Project` code is needed by this sample.

## Controls

1. **Earn Reward** persists a demo completion entitlement (100 coins + one hammer). It does not credit balances.
2. **Open Claim Panel** previews an unclaimed entitlement. Claim credits only after persistence; closing first keeps it available. Cached panel reopens with a single session.
3. **Claim Without Panel** claims through the same source and opens no UI.
4. **Show Received Notice** displays the last completion observed in this session. It never grants. This is a session notice, not a durable unseen-notice inbox.
5. **Reload / Recover** reloads wallet and feature state. Reopen and Claim to resume pending work.
6. **Inject Final Save Failure** makes the next final claimed commit fail after effects are delivered. Reload/retry does not add those effects again.

Demo saves use the separate `DreamyRewardPresentationDemo` directory. No production wallet or save namespace is reset by sample controls. `DemoRewardService` is an example feature owner; a game should inject its existing owner via IRewardClaimSource.

## Integrate / reskin

Call `RewardPresentationInstaller.Install(profileScope, panel)` once at the root, keep/dispose the returned queue with that scope, and inject your source. The supplied host uses a cached scene panel. An addressable host can implement IRewardPresentationHost instead. The session owns its bindings; do not also register a second presenter for the same panel.

RewardPanel/RewardItem prefabs include required bindings; visual names/icons come from ResourceDisplayCatalog with ID/amount fallback. Move assets through Unity with .meta files to own/reskin them. Use existing TMP Essentials GUIDs or deliberately replace fonts through Unity.

A prebuilt scene and prefabs are supplied. Optional menu `Dreamy/Economy/Samples/Build Reward Presentation Demo` regenerates only this sample's assets; close the demo scene first. In Play Mode, the Editor helper `RewardPresentationSmoke.RunAsync(100)` checks repeated show/close and subscription/row counts.

See package Documentation~/reward-presentation.md for Confirm/Notice/optional no-panel usage, cancellation, results, feature adapters and recovery limits. Native/mobile, remote purchase and cloud guarantees require consumer-specific validation.
