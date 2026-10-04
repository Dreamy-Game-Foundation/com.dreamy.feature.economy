# Resource Holders

Sample của Dreamy Economy Contracts. Import từ Window > Package Manager > Dreamy Economy Contracts > Samples > Import. Unity chép nội dung vào Assets/Samples/Dreamy Economy Contracts/0.2.0/Resource Holders/.

## Cấu trúc và tích hợp

Giữ nguyên folder, .meta, asmdef và reference prefab khi chuyển vào project. Chỉ giữ một bản script/asmdef và một JSON cho mỗi key Resources/DataConfig. Bootstrap config/save/wallet/audio tại GameInstaller trước khi bật UI, theo [README package](../../README.md). Link tương đối này dùng trong source package; sau import, mở README package từ Package Manager.

CoinHolder và GemHolder là variant của ResourceUIHolder. Đặt dưới safe area, chọn Resource Type, gán icon trong ResourceDisplayCatalog và cung cấp IResourceBalanceProvider. Provider observable tự cập nhật balance. Holder là HUD, không phải UIPanel.

## Addressables cho HUD/overlay

ResourceUIHolder không phải UIPanel. Có thể đặt trực tiếp dưới Canvas và bind/initialize như hướng dẫn trên. Nếu cần tải theo yêu cầu:

1. Đưa prefab của game vào Addressables Group, ví dụ UI Widgets.
2. Đặt Address là UI/ResourceUIHolder.prefab và khai báo constant tương ứng trong class UIAddress.
3. Dùng AssetLoader.LoadAsync<GameObject>(address), instantiate dưới Canvas rồi Bind hoặc Initialize với service/registry cần thiết.
4. Destroy instance khi kết thúc, chỉ unload cache khi không còn consumer. Build content trước khi thử player.

Luồng này cần Dreamy Assets và UniTask ở game. Không truyền type HUD/overlay vào PanelManager.Show<T>(), vì API đó yêu cầu UIPanel subclass.
