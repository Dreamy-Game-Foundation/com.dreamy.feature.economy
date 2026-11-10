# Changelog

## [0.8.1] - 2026-11-06

- Hide the optional Reward Presentation sample rebuild menu; use the supplied scene/prefabs. Runtime reward and wallet contracts are unchanged.
- Synchronize package metadata and README; no asset GUID or game-save migration. Prepared locally; no release tag or publication is implied.

## [0.8.0] - 2026-10-28

- Add per-holder `ResourceUIHolder.DeferRefresh()` scopes. Automatic balance events remain subscribed; both events and explicit refresh wait until the final scope releases and reads the current provider balance. Repeated disposal is safe.
- Invalidate old scopes on disable, destroy, unbind, bind and resource selection. Disabled holders detach events and retain their provider for resynchronization on enable; unbind/destroy release the provider. Old tokens cannot affect newer scopes.
- Preserve serialized fields and legacy `autoRefresh=false` manual mode. Add public `Unbind()` for explicit binding cleanup; no reward, save or Feedback logic enters the holder.
- Document before-mutation acquisition, owner cancellation/close cleanup and Home/Shop migration. Add focused PlayMode coverage for balance, nesting, lifecycle and async cleanup. Minor version bump reflects additive public APIs; no prefab/save migration is required.

## [0.7.0] - 2026-10-20 (source candidate; included in 0.8.x)

- Add engine-independent Reward Presentation contracts, scoped claim snapshots, FIFO queue and cancellation-safe sessions. Confirm waits for explicit Claim; Granted Notice never grants; RewardClaimFlow supports no-panel claiming.
- Add importable persisted Reward Presentation sample with cached UIPanel, SafeArea, reward rows, demo scene, final-save failure/recovery controls and Editor lifecycle smoke.
- Add contract/session/queue and real-file restart/recovery tests. Existing reward/wallet save schema remains v4; no presentation-owned player save is added.
- Daily/Missions optional scoped-offer adapters ship in their own integration assemblies; Economy does not reference feature domains.

- Remove redundant recovery and resource suggestion prefab generator menus and the recovery sample scene builder; use the supplied prefabs and scene. Keep the resource holder inspector.
- Trim the Economy UI Editor assembly references to the resource holder runtime assembly.

## [0.6.0] - 2026-10-16

- Load resource suggestion panels by address through PanelManager; register typed Coin/Gem presenters with PanelPresenterFactory. Remove direct prefab instantiation from suggestion presentation.
- Separate one-shot request state and navigation from the panel presenter lifecycle so accepted Acquire survives popup disposal and waits for close before navigation.
- Preserve prefab GUIDs, SafeArea and UI bindings during the Unity MCP migration; update standalone sample composition and integration documentation.
- Breaking migration: replace ResourceSuggestionPanelPresentation(prefab, ...) with ResourceSuggestionInstaller.Install<TPanel>(factory, resource, address, balances, acquisition). ResourceSuggestionPresenter now implements IPanelPresenter and takes ResourceSuggestionRequest. Use a distinct concrete panel type per resource and register its address.
- Validation in the sandbox: 90 managed tests, 17 recovery EditMode tests and 5 addressable popup PlayMode tests passed; Unity Console reported no errors.

## [0.5.0] - 2026-10-16

Single-resource panel follow-up (2026-10-16): add resource-specific plain-color Coin/Gem
prefab variants, passive view/presenter and close-before-navigation flow. Remove custom frame
and resource artwork. Keep multi-deficit presentation as optional fallback. New managed
regressions cover duplicate costs, close ordering, selected target, Later/Back intent,
funded/unavailable resources, cancellation, cleanup and reopen.


- Consolidate the current implementation for a local source release; clean UI consumer checks passed; native/mobile gates and the full-project UI test failures remain open.

## Repeat insufficient-purchase prompts — 2026-10-16

- Every insufficient purchase requests recovery again, including the same offer while acquisition is pending. Remove the offer-ID suppression in ShopPresenter.
- The managed popup presentation replaces the old session and waits for its close before reopening the cached panel; maintain one active popup and ignore stale purchase UI completions.
- Add regression coverage for repeated same-offer purchases, session replacement and multiple popup close/reopen cycles.

## Fix recovery close continuation — 2026-10-16

- Share pending shortage-panel close completion through UniTaskCompletionSource; only one runner awaits Hide. Simultaneous Close/CloseAsync calls and OnPreHide re-entry no longer register multiple continuations on a preserved pending UniTask.
- Add deterministic Editor tests for concurrent close waiters, re-entry, cached reopen and propagation of hide failures. Recovery Editor/Shop suites: 10/10 passed in Unity 6000.4.12f1.

## Historical 0.5.x/0.6.0 integration follow-up — 2026-10-16

- Wire Shop shortage recovery in the sandbox composition root; use the shared Popup stack.
- NotEnoughResourcesPanel now uses UIPanel and a BaseFeaturePanel prefab variant with inherited SafeArea/tweens.
- Reuse the existing Shop during acquisition; allow another offer without duplicate shortage sessions or stale result writes.

# 0.4.0 — 2026-10-15

Typed multi-resource spend/exchange, atomic detached commits, durable payload receipts, schema-3 additive migration, optional shortage presentation and resource acquisition adapters. Legacy wallet signatures and bounded-history behavior remain available. Includes Editor-generated recovery prefabs and persisted demo scene; default requires confirmation after return.

# Changelog

## 0.5.0 — shared inventory

- Store game-defined stackable/unique resource targets in the same wallet document and receipt ledger.
- Add CreateRewardDestination adapters, shared-owner Reload and one-time ImportInventory with payload-bound migration markers.
- Preserve legacy v1–3 fields in EconomySaveData v4; currency/spend/exchange preserve all inventory and import state.
- Block all reward adapters after an ambiguous owner write; isolate committed balance notifications from listener exceptions.
- Add unified-owner, migration, interleaved write, duplicate and persistence-failure regressions.


## Historical resolved-reward work (included in 0.5.0 and later)

- Add immutable typed resolved rewards, canonical versioned fingerprints and
  length-prefixed request and entry identities.
- Add strict currency destinations and whole-bundle route/overflow validation.
- Persist detached wallet candidates before committing memory and events.
  Keep exchange atomic and reject invalid/default amounts and checked overflow.
- Add economy save v2 with lifetime strict receipts and legacy collision
  tombstones; preserve the legacy constructor's bounded duplicate semantics.
- Add feature-embedded journal transitions and a synchronous, reentry-safe
  delivery coordinator with detached persistence callbacks.
- Add persisted-owner recreation, fault-boundary and file-backed DataSave tests.

## [0.3.1] - 2026-10-15

- Move ResourceUIHolder into the Resource Holders sample, preserving its GUID and variant references so imported prefabs can be edited by the host project.
- Simplify holder prefabs to RectTransform-based layout; remove HorizontalLayoutGroup, LayoutElement, CanvasGroup, and scale tween components.
- Update CoinHolder and GemHolder anchors for the simplified base prefab.
- Remove ResourceUIHolder.OnValidate; retain explicit inspector and runtime resource updates.
- Update Resource Holders integration documentation.

## [0.2.1] - 2026-10-12

- Expose resource icon and HUD target for host-owned reward presentation.
- Add optional automatic balance refresh and synchronize the resource holder prefab.
