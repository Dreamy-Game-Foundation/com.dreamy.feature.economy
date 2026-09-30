# Dreamy Economy

Shared resource identifiers, atomic wallet requests, observable balances, and reusable resource UI.

## Persistent wallet

`DatasaveResourceWallet` is the standard local wallet. It stores balances and a bounded transaction ledger through `IDatasaveService`, writes after every successful grant or exchange, and raises `BalanceChanged` for bound UI.

```csharp
IResourceWallet wallet = new DatasaveResourceWallet(
    datasave,
    initialBalances: new[] { new ResourceAmount(new ResourceId("currency.coin"), 100) });

ServiceLocator.Register<IResourceWallet>(wallet);
ServiceLocator.Register<IResourceBalanceProvider>((IResourceBalanceProvider)wallet);
```

Use stable transaction IDs for all grants, especially store transactions, so a purchase retry cannot grant the same reward twice.

## Resource UI holder

Use `Runtime.UI/Prefabs/ResourceUIHolder.prefab` directly or create prefab variants such as `CoinHolder` and `GemHolder`.

The holder contains an icon, TMP display name, TMP balance, layout, and a scale tween. Select its resource from the **Resource Type** dropdown. The bundled `ResourceDisplayCatalog` defines `currency.coin` and `currency.gem`; add more entries for energy, boosters, tickets, or game-specific resources.

At runtime the holder resolves `IResourceBalanceProvider` from `ServiceLocator`. If the provider also implements `IResourceBalanceSource`, its value updates immediately after a grant or exchange.

```csharp
public sealed class GameWallet : IResourceWallet, IResourceBalanceSource
{
    public event Action<ResourceBalanceChanged> BalanceChanged;

    public long GetBalance(ResourceId resourceId) { /* game save */ }
    public bool TryGrant(ResourceGrantRequest request) { /* atomic grant */ }
    public bool TryExchange(ResourceExchangeRequest request) { /* atomic exchange */ }
}

ServiceLocator.Register<IResourceWallet>(wallet);
ServiceLocator.Register<IResourceBalanceProvider>(wallet);
```

Call `ResourceUIHolder.Bind(provider)` when the host uses dependency injection instead of `ServiceLocator`.
