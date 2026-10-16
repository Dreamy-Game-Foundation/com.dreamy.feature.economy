# Spend, exchange and resource recovery (0.4.0)

A profile has one wallet, owned by its composition root. Gameplay submits intent; UI never writes balances. Resources use positive `long` costs/rewards. Balances may be zero. BigNumber and server-authoritative wallets need separate adapters/contracts.

## Transaction capability

`IResourceWallet` and its bool grant/exchange API are unchanged. Check the optional capability once at the root; custom legacy wallets are not required to implement it.

```csharp
var transactions = wallet as IResourceTransactionWallet;
var request = new ResourceSpendRequest("upgrade:profile42:quote17",
    new[] { new ResourceAmount(new ResourceId("currency.coin"), 100),
            new ResourceAmount(new ResourceId("currency.gem"), 10) }, "upgrade:sword");
var result = transactions?.Spend(request)
    ?? new ResourceTransactionResult(ResourceTransactionStatus.Unsupported, request.TransactionId);
if (result.IsNewCommit) { /* Present newly committed wallet changes. */ }
```

For an item represented in the same wallet, use `Exchange(new ResourceTransactionRequest(id, costs, rewards, context))`. Both lists must be nonempty. Repeated resource IDs are summed with checked arithmetic before balance validation; every deficit comes from the same snapshot. No debit, reward, receipt or event is published if validation or arithmetic fails. Reward overflow aborts the entire transaction.

Requests copy their lists. Default requests/amounts fail validation. Results and deficits are immutable snapshots, not reservations. `IsSuccess` includes `Committed` and `AlreadyCommitted`; feedback should use `IsNewCommit` to avoid replaying a new-purchase animation.

Use the same ID and payload to retry one intent. A new quote, price or intent needs a new ID and confirmation. Fingerprints include operation kind, context and normalized costs/rewards (ordinal ordering, invariant decimal encoding, SHA-256). Receipt lookup happens before balance validation. Changed payload returns `PayloadMismatch`; collision with an old ID-only receipt returns `LegacyReceiptUnverifiable`.

## Persistence, migration and owner constraints

Save schema 3 adds `TransactionReceipts`. Version 1/2 payloads retain balances, bounded legacy IDs, delivery tombstones and strict reward receipts; the new collection starts empty. No old fingerprint is inferred. The field addition needs no destructive migration. Rehearse with a copy of real saves before rollout; old clients cannot load a schema-3 envelope.

Strict transaction receipts are retained for the profile/save namespace lifetime, independently from the legacy history limit. Do not prune them without a game-owned policy proving old IDs cannot replay. All grant/delivery/spend/exchange IDs share a namespace. Existing legacy eviction and exception behavior remains in the bool API.

The wallet is synchronous and confined to one owner thread. Do not call it concurrently from worker threads or use multiple writers for the same save key. It builds a detached candidate and persists balances plus receipt together, then publishes events. The new typed API isolates subscriber exceptions; subscribe `NotificationFailed` at the root for diagnostics.

`IDatasaveService.Save` does not guarantee that an exception means no write occurred. After an exception the typed API reloads through `Load`: a matching durable receipt proves commit; its absence returns `PersistenceFailed`. **Custom stores must make Load read durable state, bypassing a stale cache**, as the supplied DatasaveService does. If reload fails or the outcome cannot be reconciled, `PersistenceUncertain` quarantines all wallet mutations until the root recreates/reconciles the owner. Do not automatically retry with a fresh ID.

A spend is atomic only inside the wallet. An upgrade saved by another owner needs that feature's durable workflow/journal; there is no generic atomic “spend then callback”. Cancel after commit only cancels presentation. A refund is a separate transaction.

## Optional prompt

Add `Dreamy.Economy.UI.Integration` to the caller's asmdef; views also use `Dreamy.Economy.Recovery.Unity`. Inject the already-owned balance source and gateway. Domain Runtime retains `noEngineReferences: true` and has no UI/Shop/UniTask dependency.

```csharp
var acquisition = new ResourceAcquisitionCoordinator(gameGateway);
var shortage = new NotEnoughResourcesPresenter(balanceSource, panelView, acquisition);
var flow = new ResourceActionFlow(wallet as IResourceTransactionWallet, shortage);
var silent = await flow.ExecuteAsync(request, InsufficientResourcePresentation.None, ownerToken);
var prompted = await flow.ExecuteAsync(request, InsufficientResourcePresentation.Prompt, ownerToken);
// prompted.Transaction remains the ORIGINAL result even after navigating or acquiring resources.
// Re-read the feature's quote/state and ask for confirmation before another spend.
```

Use `None` for background checks and `Prompt` for a player action. Only `InsufficientResources` can open a prompt. Missing presentation returns recovery `PresentationUnavailable` and preserves the original failure. One presenter owns one session; concurrent prompts return recovery `Busy`. Dispose the presenter and cancel the owner token at teardown. Inject a shared coordinator where the host wants navigation serialized across entry points.

The panel refreshes every deficit on wallet changes, hides unavailable routes, locks acquisition while navigating, and releases listeners on close/cancellation. The destination receives input while the popup remains subscribed. Navigation return does not purchase, retry, reserve resources, or prove that an IAP receipt arrived. No automatic resume mode is enabled; the feature must implement revalidation and explicit confirmation.

Direct entry does not need a fake spend failure:

```csharp
await acquisition.OpenForResourceAsync(new ResourceId("currency.gem"), 10, "home", ownerToken);
await acquisition.OpenForDeficitAsync(result.Deficits[0], "upgrade", ownerToken);
```

`IResourceAcquisitionGateway` exposes availability and async navigation. The request contains target ID, missing amount, complete deficits and source context, with no tab/prefab/game enum. A game may route to Shop, ads, gameplay or an energy screen. The host view owns localization, action labels, fonts and number formatting; the supplied view uses ResourceDisplayCatalog with readable ID/icon fallbacks.

## Managed popup integration

`NotEnoughResourcesPanel` derives from `UIPanel` (`Layer = Popup`, cached). Its supplied prefab is a **variant of Dreamy Feature's BaseFeaturePanel**, inheriting SafeArea, backdrop and tween ownership; it has no independent Canvas. Economy declares `com.dreamy.feature` 0.2.0 for that asset dependency. Parent it through the game's PanelManager/Canvas.

For normal game UI, use `ResourceRecoveryPanelPresentation` instead of manually creating a Canvas or toggling panel objects:

```csharp
var recovery = new ResourceRecoveryPanelPresentation(balanceSource, acquisition, notEnoughPrefab);
var flow = new ResourceActionFlow(wallet as IResourceTransactionWallet, recovery);
await flow.ExecuteAsync(request, InsufficientResourcePresentation.Prompt, ownerToken);
```

It creates/reuses the popup through PanelManager, owns the deficit presenter session, and waits for closing before releasing its session lock. Back/Return cancels the modal session. During acquisition it removes the invisible popup from the Back stack so the destination receives input, then restores it on return. Destroying/closing the origin cancels recovery.

Sandbox GameInstaller wires its serialized `notEnoughResourcesPrefab` into `RecoverShopResourcesAsync` and supplies that callback to ShopFeatureInstaller. BootstrapScene references the variant directly. The Shop-specific acquisition gateway focuses offers in the existing ShopPanel; its purchase controls remain usable during acquisition. Every new insufficient purchase, including the same offer during acquisition, requests the prompt again. ResourceRecoveryPanelPresentation cancels the older session, waits for its close tween, then opens the latest request on the cached panel. Older recovery completion cannot overwrite a newer purchase result.

## Shop and custom layouts

### Choose a shortage panel per game and resource

`IInsufficientResourcesPresentation` is the extension point for game-specific shortage UI. Implement
`ShowAsync(costs, contextId, token)` to open the game's panel, honor cancellation, and complete only
after closing it and releasing input. The complete costs are passed through so the implementation
can re-read balances and display all current deficits. Opening or returning from a panel never retries
the original purchase.

Use `ResourceRecoveryPresentationRouter` to choose among those implementations:

```csharp
var recovery = new ResourceRecoveryPresentationRouter(balanceSource,
    new Dictionary<ResourceId, IInsufficientResourcesPresentation>
    {
        [new ResourceId("currency.coin")] = gameCoinPresentation,
        [new ResourceId("currency.gem")] = gameGemPresentation
    }, fallback: new ResourceRecoveryPanelPresentation(balanceSource, acquisition, notEnoughPrefab));
var flow = new ResourceActionFlow(transactions, recovery);
```

The router copies the map, normalizes repeated cost IDs with checked addition, and chooses the first
**mapped, currently missing** resource in cost order. It opens one presentation per request. When no
missing resource has a mapping it uses the optional fallback; without one it returns
`PresentationUnavailable`. If all resources are now funded it returns `None` without opening UI.
It re-reads balances after waiting for the previous presentation to close. New calls cancel and wait
for the old session, including when switching between different panels. Keep calls on the wallet's
owner thread and pass its UI owner's lifetime token.

In the sandbox, `GameInstaller > Insufficient Resource Presentations` accepts a resource ID and a
`MonoBehaviour` component implementing `IInsufficientResourcesPresentation` for each entry. For
example, bind `currency.gem` to the game's gem panel presentation and `currency.coin` to its coin
presentation. Other resources use `Not Enough Resources Prefab`. An empty list retains the default
panel. Duplicate IDs, invalid IDs, and components without the interface fail initialization with a
configuration error. Plain C# implementations can be injected through the router constructor at the
game's composition root.

Shop 0.2.0 uses the typed capability when available and adds `ShopPurchaseStatus.InsufficientResources`, deficits and the underlying transaction result. Legacy wallets still return `ExchangeFailed`; a bool failure is never inferred as shortage. The optional ShopPresenter/ShopFeatureInstaller recovery callback accepts the presenter's lifetime token and is independent from purchase-success feedback.

`Dreamy.Shop.UI.Integration` supplies `ShopResourceAcquisitionGateway`, an immutable `ShopResourceDestinationMap` and `IShopResourceNavigator`. Map resource IDs to semantic IDs such as `currency-section` or `gems`. The gateway matches actual rewards and excludes offers costing that same missing resource; purchase-once routes require a balance provider and are excluded when owned. A navigator resolves semantic IDs to the game's tabs, scroll list, separate panel or NPC, reuses an existing destination, waits for layout, focuses, and completes when the player returns. It never purchases. Return `NoMatchingDestination` for unsupported resources/layouts.

The Shop Feature sample includes `ShopPanelResourceNavigator` for its scroll list. The Resource Recovery demo shows coin/gem semantic tab selection. Sandbox `CustomShopResourceAcquisitionGateway` focuses the existing UIShopPanel's gem/gold holders. `GameInstaller.ResourceAcquisition` exposes that coordinator. Host adapters must cancel before late focus and retain owner cancellation until navigation completes.

## Samples and validation

Import **Resource Recovery**, then open `ResourceRecoveryDemo.unity`. It has direct prefab references, safe area, multi-resource rows, silent/prompt actions, direct Gem navigation and a clearly marked demo grant button. The demo owns a dedicated persisted wallet (`DreamyRecoveryDemo`); production uses its existing wallet. Its tab gateway never seeds or grants balances. Use the explicit grant button to test partial/complete recovery and confirm again to spend.

Editor menu `Dreamy/Economy/Create Resource Recovery Prefabs` creates new assets under Assets/DreamyResourceRecoveryGenerated and refuses to overwrite an existing folder. Imported samples include a scene builder under `Dreamy/Economy/Create Recovery Demo Scene`, which refuses to overwrite the supplied scene. An EventSystem is included in the demo scene; embedding the panel in a game requires the game's EventSystem. Keep GUIDs when moving sample assets.

Run from this sandbox:

```sh
python3 LocalPackages/com.dreamy.feature.economy/Tests~/validate-resource-recovery.py
```

The harness compiles actual managed sources against sandbox references and executes contract/migration/recovery/Shop regressions. It requires the sandbox's .NET SDK and generated reference assemblies and does not replace a fresh consumer import or Unity/device validation. Native tests: `Dreamy.Economy.Runtime.Tests`, `Dreamy.Shop.Runtime.Tests`, `Dreamy.Shop.Recovery.Tests`. See the plan's implementation evidence for exact gates.


## One authored suggestion panel per resource

See `Samples~/Resource Recovery/ResourceSuggestions/README.md` for the default Coin/Gem variants, hierarchy and composition. Register typed panels with `ResourceSuggestionInstaller.Install<TPanel>` and bind the returned presentations in `ResourceRecoveryPresentationRouter`. PanelManager loads addresses and the presenter factory owns each opening; installers do not require suggestion prefab references. The view has one resource, message and Shop/Later; it closes before gateway navigation. Use this for game-specific single-resource prompts. The multi-deficit panel above is an optional fallback.
