# Wallet Demo

Import Wallet Demo from Dreamy Economy Contracts in Package Manager. Import TMP Essential Resources and install Dreamy UI / UniTask.

Instantiate EconomyDemoPanel.prefab under PanelManager. Await Init/PostInit, call Configure(hostWallet, hostBalanceProvider), then Show. Back closes the panel. The sample never installs a second wallet.

Grant 100 Coins creates a transaction ID; Retry Last Grant uses that same ID so an idempotent wallet accepts it without granting twice. The panel displays currency.coin, currency.gem, currency.gold and item.chest. Customize resourceIds for the host catalogs. DatasaveResourceWallet balances persist across sessions; this panel has no save-reset action.

Use this sample beside Shop, Daily Reward, Missions, Progression and Lucky Wheel to observe their rewards in the same wallet. Runtime code has no dependency on the sandbox project.
