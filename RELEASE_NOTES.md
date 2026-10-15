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
