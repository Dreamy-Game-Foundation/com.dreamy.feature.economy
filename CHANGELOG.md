# Changelog

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

## [0.2.1] - 2026-10-12

- Expose resource icon and HUD target for host-owned reward presentation.
- Add optional automatic balance refresh and synchronize the resource holder prefab.

