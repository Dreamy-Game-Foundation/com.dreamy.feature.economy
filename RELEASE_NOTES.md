# Local source release 0.5.0 — 2026-10-16

Single-resource panel follow-up (2026-10-16): add resource-specific plain-color Coin/Gem
prefab variants, passive view/presenter and close-before-navigation flow. Remove custom frame
and resource artwork. Keep multi-deficit presentation as optional fallback. New managed
regressions cover duplicate costs, close ordering, selected target, Later/Back intent,
funded/unavailable resources, cancellation, cleanup and reopen.


Annotated tag `v0.5.0` identifies the reviewed source, not production certification.
Fresh validation: targeted Unity EditMode 133/133 passed; Economy/Shop managed checks 88 passed; Progression managed checks 59 passed
(one Unity-only case is excluded by that harness). Connectivity clean runtime-only Unity consumer
and Android/iOS C# branch compiles passed. A separate clean consumer compiled all four packages
and their unmodified UI samples, validating required shortage bindings and prefab scripts.
Five tests in the unchanged Dreamy UI suite failed in the full-project EditMode run;
full-project green and native/device readiness are not certified. See the sandbox review report
`docs/evidence/resource-recovery-release/REVIEW.md` for Unity suite results and open gates.
The earlier implementation notes below describe historical validation and pre-tag working state.

---

# 0.5.0 — Shared wallet and inventory

Date: 2026-10-16. Working tree implementation; not tagged/published.

- DatasaveResourceWallet owns currency plus game-defined card/chest/cosmetic/item balances in EconomySaveData v4.
- CreateRewardDestination routes multiple reward kinds through that one owner/save and global fingerprint receipt ledger.
- ImportInventory commits old counts, original receipts and snapshot-bound import markers once; host owns legacy schema decoding.
- Reload reconciles ambiguous writes while preserving service references/subscriptions; all adapters stop until reconciliation.
- Existing constructors/currency behavior remain available. Strict legacy retry after Save exceptions now requires Reload.
- v1–3 save fields are preserved; v4 needs newer clients. Do not downgrade or run old split writers after cutover.

See Documentation~/shared-inventory.md for game wiring and migration. New inventory support stores
quantity/ownership; game-specific instance metadata, loot rolling, conversion, backend/cloud policies stay outside.
Progression journals remain separate; this does not make a whole multi-entry bundle atomic.

---

# 0.4.0 implementation (not tagged/published)

Typed multi-resource spend/exchange, atomic detached commits, durable payload receipts, schema-3 additive migration, optional shortage presentation and resource acquisition adapters. Legacy wallet signatures and bounded-history behavior remain available. Includes Editor-generated recovery prefabs and persisted demo scene; default requires confirmation after return.

Economy schema 3 cannot be loaded by old clients. Strict receipts live with the profile; do not prune for size. Custom stores must reload durable state on ambiguous write failures. Sandbox validation: 63 managed test cases plus Shop presenter harness, 127/127 Unity EditMode tests, Play Mode recovery demo, fresh sample import in sandbox, and StandaloneLinux64 managed player compilation (95 assemblies). Fresh external consumer, full native/mobile build/device and SDK billing gates remain unverified. Source validation evidence is recorded in docs/plans/DREAMY_ECONOMY_RESOURCE_RECOVERY_PLAN.md in the sandbox.

# Ghi chú phiên bản 0.3.0

Ngày: 2026-10-15. Phiên bản trước: 0.2.1.

Thêm bundle reward cụ thể, stable identity/fingerprint, gateway/destination, embedded journal/coordinator và strict owner receipts. Wallet lưu detached candidate; giữ đường TryGrant/TryExchange tương thích.

## Tương thích và phạm vi

Giữ GUID asset/script và đường dẫn prefab cũ. Thư mục sample đã import trong
Assets/Samples giữ version/path lịch sử; import mới dùng version package mới.
Daily/Missions dùng Feature view contract: consumer phải thêm reference trực tiếp
Dreamy.Feature.Runtime ở asmdef dùng API và compile lại. Không coi đây là thay DLL
nhị phân mà không rebuild. Game có save cũ cần rehearsal migration trên bản copy.

Scope đã kiểm chứng là sandbox offline casual/puzzle bằng Unity Editor tests.
Không chứng nhận mọi consumer, production inventory, server/cloud authority,
SDK billing, player/device, Home/Addressables hoặc rollback rollout chưa chạy.
Tag source không thay các gate đó. Xem README và evidence của sandbox.
