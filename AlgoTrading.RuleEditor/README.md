# AlgoTrading Strategy Editor

This is the deliberately small, personal-trader strategy authoring application. It uses the existing nested condition editor and the shared `AlgoTrading.Models` rule binder; it does not create a second expression language or store runtime state in strategy files.

## Run

The deployment is framework-dependent and requires the .NET 10 Desktop Runtime on Windows. Start `AlgoTrading.RuleEditor.exe`.

## Authoring workflow

1. In **Overview**, enter one exchange, segment, instrument, session, and trade direction.
2. In **Entry**, author the nested condition tree. Optionally select a regular pivot or Critical Support/Resistance level. A binding name preserves the identity of the selected level across the retest sequence; equal-priced origins are not interchangeable.
3. In **Protection & partial exits**, configure the initial stop and ordered partial exits. `Original` means a percentage of the entry fill. `Remaining` means a percentage of the still-open position when the stage triggers.
4. In **Full exit**, author the condition that closes all remaining quantity.
5. In **Execution & summary**, select collision/re-entry behavior and review the generated explanation.
6. Select **Validate**, then **Save**. Files use the `.strategy.json` extension.
7. In **Backtest**, choose a completed-candle CSV, enter quantity, and run. The report is shown in the application.

The editor evaluates rules on completed candles only. It intentionally exposes no forming/future-candle option.

## Files, compatibility, and safety

- Full strategies use schema version `1`. Unsupported versions fail with a clear message; there is no internal version tree.
- A legacy single-rule JSON file can be opened. It is imported into a new unsaved strategy, leaving the original untouched. Complete the missing instrument, protection, and exit settings before saving.
- Read-only files open in read-only mode and save to a new path.
- Save detects an external file change and refuses to overwrite it; use Save As to preserve both copies.
- Undo/redo is deterministic and bounded to 50 complete strategy snapshots.
- Runtime state, orders, fills, positions, credentials, and connection strings are never persisted in strategy JSON.

## Runtime handoff

`Services/StrategyRuntimeAdapter.cs` is the stable non-UI boundary. `Load(json)`:

- rejects legacy, unsupported, incomplete, or invalid documents;
- binds entry and full-exit condition trees through the shared runtime;
- returns a deep-copied immutable-in-practice snapshot so later editor changes cannot modify a running strategy;
- retains protection, partial-exit, level/retest, and execution-policy metadata for the position/execution coordinator.

The built-in personal backtester resolves candle rules, position quantity, completed-candle stops, R/percent targets, partial exits, and long/short P&L. Pivot/critical-level strategies still require a level provider and are rejected explicitly by the backtester rather than silently using fabricated prices.

## Backtest CSV

Required columns are `OpenedAt,ClosedAt,Open,High,Low,Close,Volume`. Optional `Timeframe` defaults to the strategy evaluation timeframe, and optional `InstrumentToken` defaults to `1`. Timestamps must include an offset, such as `2026-01-02T09:15:00+05:30`. Mixed-timeframe strategies need rows for every required timeframe.

Backtest behavior is intentionally small and deterministic: entry and full-exit signals fill at the next candle open; protective stops and targets use completed candle OHLC; stop/target priority and one/all eligible target behavior come from the strategy; no fees or slippage are applied; an open position is closed at the last candle close. Partial targets must be positive R multiples (`1R`) or percentages from entry (`2%`).

Headless use:

```powershell
AlgoTrading.RuleEditor.exe --backtest strategy.strategy.json candles.csv report.txt
```

## Validation messages

Messages identify their strategy path, such as `Entry`, `Protection.Value`, or `PartialExits[0].QuantityPercent`. A blocked Save means the document is incomplete or cannot bind to the shared runtime. Corrupt files show the file path and serializer diagnostic.

## Verification

Run:

```powershell
dotnet build AlgoTrading.RuleEditor.csproj --no-restore
dotnet .\bin\Debug\net10.0-windows\AlgoTrading.RuleEditor.dll --verify editor-verification.txt
```

The verification covers the original rule/value/group/execution checks plus full-strategy round trip, legacy import, unsupported-version rejection, partial-exit quantity bases, validation, runtime snapshot isolation, and strategy-level undo.

## Explicit non-goals

- No broker order placement inside the editor.
- No automatic market-data creation.
- No hidden future/forming-candle evaluation.
- No multi-user approval workflow, database document-version tree, or institutional deployment infrastructure.
- No silent “nearest level” behavior.
