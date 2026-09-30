# Resource Holders

`CoinHolder` and `GemHolder` are variants of the package `ResourceUIHolder` prefab. Change **Resource Type** in the holder inspector to reuse the same prefab for energy, tickets, boosters, or other catalog entries.

Replace the placeholder icon or assign a sprite in `ResourceDisplayCatalog`. Connect an `IResourceBalanceProvider`; observable providers refresh the holder automatically.
