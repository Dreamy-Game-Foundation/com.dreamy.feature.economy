# Dreamy Economy Contracts

Reusable reward panels, optional no-panel claiming and feature adapters: [Reward Presentation](Documentation~/reward-presentation.md).

Spend/exchange nhiều tài nguyên và recovery tùy chọn: [API, migration và sample](Documentation~/resource-recovery.md).

## Bắt đầu dùng currency và reward

Đọc [cách tích hợp và mức độ sẵn sàng](Documentation~/usage-and-readiness.md):
composition root dùng một wallet/profile, claim qua service, strict grant trực tiếp,
TryExchange, custom inventory owner, HUD và các giới hạn recovery/production.

Contract hiện phù hợp tài nguyên số nguyên trong scope đã kiểm chứng; không phải
giải pháp sẵn sàng cho mọi game. BigNumber, server/cloud authority và durable IAP
cần contract/adapter và validation riêng. Xem RELEASE_NOTES.md để biết phiên bản source và các gate chưa chạy.

Package thuộc Dreamy Game Studio. Hướng dẫn dưới đây mô tả cấu trúc, cách cài vào project và tích hợp ở root/scene.

## Cài package

Dùng Unity 6000.0 trở lên. Sandbox đã tham chiếu package bằng `file:../LocalPackages/com.dreamy.feature.economy`. Project khác dùng Package Manager > + > Install package from disk và chọn package.json, hoặc Git URL của repository nội bộ. Cài cả dependency Dreamy/Git vào manifest của game; version dependency không tự cấu hình registry riêng.

Dependency trực tiếp theo package.json:

- `com.dreamy.core` (1.1.2)
- `com.dreamy.ui` (1.0.5)
- `com.dreamy.datasave` (0.2.0)
- `com.dreamy.feature` (0.2.0)
- `com.cysharp.unitask` (2.5.10), `com.unity.ugui` (2.0.0)

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

Wallet persist sau grant/exchange thành công và phát BalanceChanged. Grant phải có transaction ID ổn định. Đường legacy TryGrant/TryExchange có bounded duplicate history; strict delivery receipts được giữ riêng theo lifetime profile/save namespace. Root unregister hai interface khi teardown; mọi feature dùng cùng wallet, không tạo wallet mới trong panel.

## Giao reward có kiểu dữ liệu rõ ràng

`Dreamy.Economy.Rewards` cung cấp bundle cụ thể bất biến, identity ổn định,
destination strict và journal được embed trong save của feature nhận thưởng.
Xem [tích hợp API](Documentation~/reward-api.md) và
[ownership/recovery](Documentation~/reward-delivery.md) để biết trình tự và giới hạn.

DatasaveResourceWallet triển khai IRewardDestination cho kind currency. Strict
receipts và tombstone collision legacy được giữ riêng, không bị giới hạn 4096 của
history legacy. TryGrant/TryExchange cũ vẫn dùng bounded history của constructor.
Strict delivery từ chối receipt chỉ có ID mà không xác minh được payload; không
giả định payload giống nhau hoặc cấp lại. Wallet lưu detached balance/receipt
candidate trước khi cập nhật runtime/cache/event; exchange vẫn là một mutation
atomic trong cùng owner.

## Holder và sample

Import Resource Holders; đặt Prefabs/CoinHolder.prefab hoặc GemHolder.prefab dưới Canvas safe area. Chọn Resource Type, thay icon và bổ sung entry trong ResourceDisplayCatalog. ID dùng category.name, ví dụ currency.coin/currency.gem.

Holder resolve IResourceBalanceProvider từ ServiceLocator. Có thể gọi ResourceUIHolder.Bind(provider) để inject; provider observable cập nhật UI ngay khi balance thay đổi. Code dùng holder cần Dreamy.Economy.UI.Runtime.

Holder mặc định bật `Auto Refresh` và nghe thay đổi số dư. Giữ bật để Home
đồng bộ cả khi Shop thay đổi wallet mà navigation chỉ HideTween/ShowTween.
`autoRefresh=false` vẫn là manual mode: không subscribe balance events, chỉ cập
nhật khi bind, enable, đổi resource, gọi `RefreshBalance()` hoặc release scope cuối.
Không cần thay serialized fields hay prefab references.
`IconTarget` và `Icon` cung cấp điểm đến/sprite mà không làm Economy phụ thuộc Feedback.

### Trì hoãn hiển thị số dư (0.8.0)

`IDisposable ResourceUIHolder.DeferRefresh()` giữ nguyên amount text trên **một
instance holder**. Với auto refresh bật, holder vẫn nghe balance events; cả event
và `RefreshBalance()` đều không đổi text trong scope. Nhiều scope có thể chồng
nhau và release theo mọi thứ tự. Release scope cuối đọc số dư mới nhất từ provider,
kể cả spend/tăng/giảm của luồng khác; không dùng snapshot hoặc cộng reward. Dispose
lặp an toàn. Đây là cơ chế chờ toàn bộ scope trên holder, chưa phải tăng số theo
từng icon hoặc từng transaction.

**Tạo scope TRƯỚC claim/purchase làm wallet thay đổi.** Nếu đợi kết quả thành công
rồi mới tạo scope thì quá muộn: balance event đã phát và text có thể đã cập nhật.
Holder phải được bind và enabled trước khi tạo scope, nếu không API ném
`InvalidOperationException`. Gọi API và dispose trên Unity main thread.

Ví dụ sau thuộc presenter của consumer. Các delegate đại diện cho API claim và
hiệu ứng của game; Economy không grant/spend/save hoặc chạy icon fly. Owner phải
gọi `Close()` từ hook đóng/hủy panel, kể cả khi panel chỉ ẩn bằng tween. Owner chỉ
được giải phóng sau khi đã cancel/cleanup và quan sát kết quả task đang chạy.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Economy.UI;

public sealed class RewardDisplayOwner
{
    private CancellationTokenSource pendingCancellation;
    private IDisposable pendingScope;

    public async Task ClaimAndPresentAsync(
        ResourceUIHolder target,
        Func<CancellationToken, Task<bool>> claimAsync,
        Func<CancellationToken, Task> playEffectAsync)
    {
        Close(); // Clean up this owner's previous presentation, if any.
        var scope = target.DeferRefresh(); // BEFORE claim, not after its result.
        var cancellation = new CancellationTokenSource();
        pendingCancellation = cancellation;
        pendingScope = scope;
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (await claimAsync(cancellation.Token))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                await playEffectAsync(cancellation.Token);
            }
        }
        finally
        {
            // Success, failure, exception and cancellation all release the hold.
            scope.Dispose();
            if (ReferenceEquals(pendingScope, scope))
            {
                pendingScope = null;
                pendingCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    public void Close()
    {
        var cancellation = pendingCancellation;
        var scope = pendingScope;
        pendingCancellation = null;
        pendingScope = null;
        try { cancellation?.Cancel(); }
        finally { scope?.Dispose(); } // Release immediately, even if async is pending.
    }
}
```

Caller phải await task và xử lý `OperationCanceledException`/exception theo owner
của mình. Claim/effect phải tôn trọng cancellation và dọn tween/callback do chúng
sở hữu; không để callback cũ tiếp tục mutate UI sau khi panel đóng. Chỉ `Dispose`
trong finally lúc task hoàn thành là chưa đủ nếu task vẫn chạy khi panel đóng.
Cancellation sau khi wallet đã commit không hoàn tác phần thưởng hoặc purchase.

Disable, destroy, `Unbind()`, mọi lần `Bind(...)` (kể cả cùng provider) và
`SetResource(...)` (kể cả cùng ID) vô hiệu hóa toàn bộ scope cũ. Token cũ dispose
sẽ không refresh hay release scope mới. Disable gỡ subscription nhưng giữ provider;
enable bind lại và đọc balance mới nhất. Bind khi disabled chưa subscribe, enable
mới subscribe. `Unbind()`/destroy gỡ subscription và xóa provider; unbind giữ text
hiện tại đến lần refresh/bind kế tiếp. Enable khi không có binding sẽ thử resolve
ServiceLocator. Không dùng rebind/SetResource như refresh callback trong hiệu ứng;
chúng bắt đầu binding/resource mới và kết thúc mọi hold cũ.

Consumer migration:

- Home: chuyển prefab/instance đang tắt `autoRefresh` sang bật. Không dùng OnEnable
  làm cách sửa stale balance khi navigation chỉ chạy tween.
- Daily/Missions dùng holder Home: lấy scope từ **holder Home**, trước claim;
  giữ đến khi effect hoàn tất, cancel, exception hoặc panel đóng.
- Shop dùng holder Shop: chỉ defer **holder Shop** trước purchase. Home tiếp tục
  auto refresh, nên đóng Shop không để lại số dư cũ.
- Owner của mỗi luồng theo dõi và release scope của chính mình; các luồng cùng
  holder phải chờ toàn bộ scope, các holder khác nhau không chặn nhau.

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


## Shared wallet and inventory (0.5.0)

Dùng một DatasaveResourceWallet cho toàn bộ Coin/Gem/Card/Chest/Cosmetic. Đăng ký
ResourceRewardDefinition cho các target của game và tạo adapter bằng CreateRewardDestination.
Tất cả balances/ownership và receipts nằm chung EconomySaveData v4; mỗi adapter không tạo save riêng.
Xem [shared-inventory](Documentation~/shared-inventory.md) để biết wiring, unique items,
Reload sau lỗi ghi và ImportInventory để migrate save cũ đúng một lần.
