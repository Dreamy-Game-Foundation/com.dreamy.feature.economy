# Hướng dẫn dùng wallet/reward và đánh giá hiện tại

Đánh giá source migration ngày 2026-10-15. Code mới trong working tree không
đồng nghĩa đã có trong release/tag mang version hiện tại của package.json.

## Phạm vi phù hợp

Contract dùng lại được cho tài nguyên số nguyên, stable identity, save owner rõ
ràng và lệnh synchronous trên một owner thread. Chưa thể kết luận phù hợp mọi game.

| Nhu cầu | Hiện trạng và phần game phải cung cấp |
| --- | --- |
| Casual/puzzle/offline, coin/gem, Daily/Missions | Có reference wallet và strict claim flow; cần consumer/config/save/UI validation của game. |
| RPG/card/collection | Bundle/gateway dùng lại được; inventory/card destination do game cung cấp. DemoItemDestination chỉ là fixture. |
| Game đã có resource save | Adapter tới owner cũ; không tạo economy save thứ hai cho cùng tài nguyên. |
| Idle vượt long hoặc tài nguyên phân số | Chưa có BigNumber/decimal contract; amount hiện là long dương. |
| Multiplayer/server-authoritative | Có gateway seam; chưa có production authority/network protocol được xác minh. Local wallet không chứng minh quyền nhận thưởng trên server. |
| Cloud save/nhiều thiết bị | Chưa có cross-device conflict resolution, distributed lock/transaction hoặc receipt-lineage synchronization. |
| Crafting nhiều loại cost hoặc trading giữa người chơi | TryExchange hiện có một cost và nhiều rewards trong cùng wallet; chưa có multi-cost hoặc cross-player transaction contract. |
| IAP/paid reward | Durable Shop fulfillment, real receipt/restore/redelivery và SDK/device gates chưa hoàn tất. |
| Reward tần suất cao, ledger dài hạn | Reference wallet save synchronous mỗi mutation và giữ strict receipts; cần đo IO/serialization/ledger growth theo tải thật. |

ResourceId phân biệt hoa/thường, dùng dot-separated segments: currency.coin,
currency.gem, item.card.scout. Reference currency wallet kiểm cú pháp ID, không biết
catalog currency của game; host có danh sách currency giới hạn phải validate thêm.

## Các contract và đường sử dụng

| API | Khi dùng |
| --- | --- |
| IResourceBalanceProvider / IResourceBalanceSource | Đọc balance / nghe BalanceChanged cho HUD. |
| IResourceWallet.TryGrant | Đường compatibility cấp một resource; bool không chứng minh strict delivery. |
| IResourceWallet.TryExchange | Cost và rewards trong cùng wallet owner. |
| IRewardDestination | Owner thật lưu effect + receipt cùng document. |
| IRewardDeliveryGateway | Validate bundle, route từng kind tới destination. |
| RewardJournal / RewardDeliveryCoordinator | Feature-owned claim recovery và finalization. |
| Dreamy.Feature | View/presenter contracts và subscription lifecycle; không sở hữu wallet/save/reward rules. |

Daily/Missions có strict gateway constructors. Lucky Wheel/Progression và Shop IAP
vẫn dùng TryGrant legacy; không coi mọi feature có cùng mức recovery.

## 1. Composition root tạo owner một lần

```csharp
using Dreamy.Core;
using Dreamy.Datasave;
using Dreamy.Economy;
using Dreamy.Economy.Rewards;

// datasave: IDatasaveService đã sẵn sàng; profileScope ổn định qua restart/retry.
var wallet = new DatasaveResourceWallet(
    datasave, saveKey: profileScope + ".economy",
    initialBalances: new[]
    {
        new ResourceAmount(new ResourceId("currency.coin"), 100)
    });
ServiceLocator.Register<IResourceWallet>(wallet);
ServiceLocator.Register<IResourceBalanceProvider>(wallet);

IResourceBalanceSource balances = wallet;
IRewardDestination currencyOwner = wallet;
var gateway = new RewardDeliveryGateway(new IRewardDestination[] { currencyOwner });
```

Initial balances chỉ áp dụng khi chưa có save. Bundle có item thì root thêm
destination kind item do game cung cấp. Mỗi kind chỉ đăng ký một destination;
adapter có thể tự route target item bên trong nó.

Inject cùng wallet/gateway vào các feature, không tạo trong panel. Một writer cho
mỗi profile/save key; busy guard hiện là per instance, không bảo vệ hai wallet cùng
ghi một key. Profile switch phải dừng consumer cũ trước khi thay owner; root chỉ
unregister các service mình sở hữu khi teardown.

Game đã có wallet giữ save authority đó. Strict adapter phải lưu mutation,
transaction ID và payload fingerprint trong cùng detached owner candidate.
Không wrap một bool wallet bất kỳ rồi gọi nó là strict.

Host asmdef reference các assembly dùng trực tiếp: Economy.Runtime, Datasave.Runtime,
Core.Runtime nếu dùng ServiceLocator; Daily/Missions/Feature/UI thêm theo API dùng.

## 2. Nhận reward của feature: gọi service Claim

```csharp
// Config đã load/validate; dailyClock là IRewardClock; dependencies do root inject.
var daily = new Dreamy.DailyReward.DailyRewardModel(
    dailyConfig, datasave, gateway, dailyClock, profileScope + ".daily");
var missions = new Dreamy.Missions.MissionModel(
    missionConfig, datasave, gateway, profileScope + ".missions");

var dailyResult = daily.Claim();
var missionResult = missions.Claim(missionId);
```

Missions ví dụ trên dùng Permanent không selection. Với periodic inject IMissionClock;
có selection/reroll inject IMissionRandom qua named arguments clock: và random:.
Policy kỳ, timezone/cutoff và selection do config/game quyết định.

UI gọi service rồi render result. Không gọi thêm wallet grant sau Claim: feature
đã giao thưởng. Pending thì retry Daily qua Claim, Missions qua RecoverClaims
(kể cả old-window pending), giữ cùng request/payload. Đóng panel không xoá pending.
Chỉ báo claim thành công sau final claimed commit.

Mystery reward cần authority resolve concrete target trước delivery. Daily có
IDailyRewardBundleResolver: intent persist trước callback; cùng request ID phải
replay cùng outcome khi resolved save lỗi. Contract hiện synchronous; không block
main thread chờ HTTP. Missing authority giữ Pending, không random client fallback.

## 3. Gameplay cộng currency trực tiếp

Khi chỉ có một currency effect và không có claim transition ở owner khác cần
finalize, gameplay service có thể gọi strict currency destination trực tiếp:

```csharp
public static RewardDeliveryResult GrantBattleCoins(
    IRewardDestination currencyOwner, string profileScope,
    string battleId, long amount)
{
    var entry = new ResolvedRewardEntry(
        "coins", "currency", "currency.coin", amount);
    var bundle = new ResolvedRewardBundle(1, "battle-v1", new[] { entry });
    string requestId = RewardIdentity.Request(
        profileScope, "battle", "victory", battleId, "base-reward");
    string fingerprint = RewardIdentity.Fingerprint(
        "battle-win-v1", requestId, bundle.Fingerprint);
    return currencyOwner.Deliver(new RewardDeliveryRequest(
        RewardIdentity.EntryTransaction(requestId, entry.EntryId),
        fingerprint, bundle.Fingerprint, entry));
}
```

Method dùng namespace Dreamy.Economy.Rewards; amount phải dương. Game kiểm tra
eligibility và giữ battleId. Retry/restart dùng lại profile scope, occurrence ID,
entry ID và original payload; không tạo GUID mới mỗi callback hay dùng thời điểm
retry làm identity. Fingerprint phát hiện payload mismatch, không chống gian lận.

- Delivered: vừa commit effect; AlreadyDelivered: receipt đã tồn tại, không cộng lại.
- Pending: chưa xem là claim success; retry cùng identity/payload theo owner contract.
- Failed: xử lý Reason; không đổi ID để vượt collision/unknown target/overflow.

Direct delivery không lưu battle completion. Nếu cần ghi claimed/progress hoặc
giao nhiều entry/owner, feature dùng coordinator và embed journal + original context
trong save của nó. Không loop Deliver rồi coi toàn claim là crash-safe.
Xem [API integration](reward-api.md) và [ownership/recovery](reward-delivery.md).

## 4. Mua bằng tiền game

```csharp
// purchaseId là identity của một lần mua; retry cùng ID và payload.
bool success = wallet.TryExchange(new ResourceExchangeRequest(
    purchaseId,
    new ResourceAmount(new ResourceId("currency.gem"), 10),
    new[] { new ResourceAmount(new ResourceId("currency.coin"), 1000) }));
```

Reference wallet save cost + rewards trong một candidate. Không tách trừ tiền rồi
grant. Boundary này không tự atomic với inventory/SDK khác. TryGrant/TryExchange
giữ bounded duplicate history, mặc định 4096; bool true có thể là duplicate đã xử lý.
Đây không phải strict lifetime payload verification. Không đổi legacy ID namespace
hoặc đoán fingerprint để cấp lại receipt cũ.

## 5. HUD và feedback

Đọc balances.GetBalance(new ResourceId("currency.coin")); bind/unbind BalanceChanged
theo lifetime HUD/presenter. Event balance không chứng minh multi-owner claim đã xong.
Feedback mừng claim theo final service result; AlreadyDelivered không phải grant mới.
ResourceUIHolder nằm trong Economy.UI.Runtime, bind cùng source hoặc dùng registration
ở root theo README; view không sửa balance/save.

## Giới hạn bảo đảm và evidence

Strict owner receipts giữ theo lifetime profile/save namespace; không prune nếu
chưa có replay-rejection protocol thay thế. Journal ghi tiến độ; receipt chặn lặp.
Bundle nhiều owner có thể nhận một phần rồi Pending: có resume, không atomic toàn bộ.
Restore backup phải xét bộ saves cùng generation, không rollback journal riêng.

Sandbox ngày 15/10 có baseline Economy 41/Missions 52/Shop 8 pass; sau refactor có
Feature 4/Missions 54 EditMode và Daily 42 PlayMode pass. Đây là các run riêng, không
cộng thành final-suite certificate. Smoke prefab Daily không thay Home/Addressables
hoặc target-device integration. Evidence đầy đủ nằm ở sandbox, không đóng gói trong
release Economy.

Trước production còn consumer compile/tests, old-save migration/restart/rollback,
profile ownership, đo performance/ledger, UI lifetime và build/device gates. Game
có network/cloud/billing phải chứng minh authority/receipt/redelivery/conflict policy
tương ứng. Đánh giá này không chứng nhận mọi package hay mọi loại game.
