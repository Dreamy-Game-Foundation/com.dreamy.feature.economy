# Changelog

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

## Unreleased follow-up — 2026-10-16

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


## [Unreleased]

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

