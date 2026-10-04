# Dreamy Economy Contracts

Package thuộc Dreamy Game Studio. Hướng dẫn dưới đây mô tả cấu trúc, cách cài vào project và tích hợp ở root/scene.

## Cài package

Dùng Unity 6000.0 trở lên. Sandbox đã tham chiếu package bằng `file:../LocalPackages/com.dreamy.feature.economy`. Project khác dùng Package Manager > + > Install package from disk và chọn package.json, hoặc Git URL của repository nội bộ. Cài cả dependency Dreamy/Git vào manifest của game; version dependency không tự cấu hình registry riêng.

Dependency trực tiếp theo package.json:

- `com.dreamy.core` (1.1.2)
- `com.dreamy.ui` (0.2.0)
- `com.dreamy.datasave` (0.2.0)

## Cấu trúc và asmdef

| Assembly | Reference | Phạm vi |
| --- | --- | --- |
| `Dreamy.Economy.UI.Editor` | Dreamy.Economy.UI.Runtime | Chỉ Editor |
| `Dreamy.Economy.Runtime` | Dreamy.Datasave.Runtime | Runtime |
| `Dreamy.Economy.UI.Runtime` | Dreamy.Economy.Runtime, Dreamy.Core.Runtime, Dreamy.UI.Runtime, Unity.TextMeshPro, UnityEngine.UI | Runtime |

Trong asmdef của game, thêm assembly chứa API trực tiếp sử dụng. Code bootstrap reference thêm Core/DataConfig/Datasave/Economy theo nhu cầu; code async reference UniTask. Code gọi type sample reference assembly sample. Giữ Editor reference trong asmdef Editor-only.

## Cấu trúc và cài wallet ở root

Runtime chứa ResourceId/ResourceAmount, request grant/exchange và wallet. Runtime.UI chứa ResourceUIHolder và ResourceDisplayCatalog; Editor chứa Inspector cho UI. Economy quản lý balance/transaction, game quyết định lúc cấp thưởng.

```csharp
using Dreamy.Core;
using Dreamy.Datasave;
using Dreamy.Economy;

// datasave là IDatasaveService dùng chung đã được root tạo.
var wallet = new DatasaveResourceWallet(datasave,
    initialBalances: new[] {
        new ResourceAmount(new ResourceId("currency.coin"), 100)
    });
ServiceLocator.Register<IResourceWallet>(wallet);
ServiceLocator.Register<IResourceBalanceProvider>(
    (IResourceBalanceProvider)wallet);
```

Wallet persist sau grant/exchange thành công và phát BalanceChanged. Grant phải có transaction ID ổn định. Ledger có giới hạn; game cần chính sách idempotency đủ dài cho retry purchase/reward. Root unregister hai interface khi teardown; mọi feature dùng cùng wallet, không tạo wallet mới trong panel.

## Holder và sample

Import Resource Holders; đặt Prefabs/CoinHolder.prefab hoặc GemHolder.prefab dưới Canvas safe area. Chọn Resource Type, thay icon và bổ sung entry trong ResourceDisplayCatalog. ID dùng category.name, ví dụ currency.coin/currency.gem.

Holder resolve IResourceBalanceProvider từ ServiceLocator. Có thể gọi ResourceUIHolder.Bind(provider) để inject; provider observable cập nhật UI ngay khi balance thay đổi. Code dùng holder cần Dreamy.Economy.UI.Runtime.

Holder là HUD component, không phải UIPanel. Nếu tải bằng Addressables, load GameObject, instantiate dưới Canvas rồi bind provider; không gọi PanelManager.Show<ResourceUIHolder>().
## Import sample

Mở Window > Package Manager, chọn Dreamy Economy Contracts > Samples > Import. Unity chép vào Assets/Samples/Dreamy Economy Contracts/0.2.0/. Chuyển cả folder nếu tùy biến, giữ .meta và reference prefab; không giữ bản script/asmdef hoặc Resources document trùng.

- **Resource Holders**: nguồn `Samples~/Resource Holders`.

## Addressables cho HUD/overlay

ResourceUIHolder không phải UIPanel. Có thể đặt trực tiếp dưới Canvas và bind/initialize như hướng dẫn trên. Nếu cần tải theo yêu cầu:

1. Đưa prefab của game vào Addressables Group, ví dụ UI Widgets.
2. Đặt Address là UI/ResourceUIHolder.prefab và khai báo constant tương ứng trong class UIAddress.
3. Dùng AssetLoader.LoadAsync<GameObject>(address), instantiate dưới Canvas rồi Bind hoặc Initialize với service/registry cần thiết.
4. Destroy instance khi kết thúc, chỉ unload cache khi không còn consumer. Build content trước khi thử player.

Luồng này cần Dreamy Assets và UniTask ở game. Không truyền type HUD/overlay vào PanelManager.Show<T>(), vì API đó yêu cầu UIPanel subclass.
