# AlgoTrading personal backtest application

This package contains two focused Windows applications:

- `AlgoTrading.RuleEditor`: author, validate, save, and backtest a strategy.
- `AlgoTrading`: authenticate with Kite and import market/instrument data into SQL when a database is configured.

The strategy editor is the primary backtest-first application. It has no internal document-version tree, approval workflow, live order placement, multi-user infrastructure, or invented correction process.

## Build and verify

```powershell
dotnet restore .\AlgoTrading\AlgoTrading.RuleTests\AlgoTrading.RuleTests.csproj
dotnet build .\AlgoTrading\AlgoTrading.RuleTests\AlgoTrading.RuleTests.csproj -c Release -m:1 /p:UseSharedCompilation=false
dotnet .\AlgoTrading\AlgoTrading.RuleTests\bin\Release\net10.0\AlgoTrading.RuleTests.dll

dotnet restore .\AlgoTrading.RuleEditor\AlgoTrading.RuleEditor.csproj
dotnet build .\AlgoTrading.RuleEditor\AlgoTrading.RuleEditor.csproj -c Release -m:1 /p:UseSharedCompilation=false
dotnet .\AlgoTrading.RuleEditor\bin\Release\net10.0-windows\AlgoTrading.RuleEditor.dll --verify .\editor-verification.txt
```

`-m:1` and `UseSharedCompilation=false` avoid restricted Windows named-pipe failures in sandboxed build environments; they are not application requirements.

## Configuration

The repository contains no SQL credential or machine-specific server name. Set `ALGOTRADING_SQL_CONNECTION_STRING` before launching the market-data application, or place a connection string in the deployed `appsettings.json`. The strategy editor and CSV backtester do not require SQL.

## Deliberate boundaries

This release is backtest-first. It does not submit broker orders. Level-provider strategies (regular pivots or critical levels) can be authored, but the local CSV backtester rejects them until a real level source is connected. It never invents a level or treats unavailable data as a match.
