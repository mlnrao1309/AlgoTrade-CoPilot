# Batch 3 — Closed-candle processing coordinator

## Scope

`ClosedCandleProcessingCoordinator` is a stateful, single-instrument boundary between provider-finalized source
candles and the existing aggregation implementations. It consumes the validated Batch 2 configuration snapshot and
one selected, fully explicit trading-calendar revision.

Only these sources are accepted:

- `INTRADAY_5M` with canonical `5minute` candles.
- `DAILY_1D` with canonical daily candles.

The event must be provider-finalized and its completion must not be later than the observation instant.

## Input policy

- Malformed OHLC/volume, wrong instrument, wrong source timeframe, and session-boundary mismatches are rejected.
- An identical replay returns `Duplicate` and emits nothing.
- A conflicting value reusing the same finalized source identity is rejected and directed to the explicit historical
  correction path.
- Older daily input returns `RejectedOutOfOrder`.
- A missing 5-minute source invalidates the remainder of that session. No affected target is fabricated.
- Session state is reset only when the selected calendar supplies the next explicit session.
- Closed days return `ClosedSession`; missing calendar coverage returns `CalendarUnavailable`.

Every result carries the selected calendar revision for diagnostics.

## Aggregation behavior

Active intraday targets are aggregated through the existing `CandleAggregator`, anchored at the explicit session
open. A source candle is never split across a target boundary. Completed 15m, 25m, 60m, and 75m targets are emitted
only when their actual target close is observed. The final target in a session is force-sealed at the official close.

A bounded integration method, `IntradayCandleClock.GetAggregationCompletion`, permits one target longer than an
explicit shortened special session to seal at that session close. The existing strict `GetCompletion` contract is
unchanged.

Daily source candles are aggregated through the existing `CalendarCandleAggregator`. Weekly/monthly output is
emitted only on the final explicit trading session in a fully covered period. A missing daily source or incomplete
calendar coverage produces an unavailable/incomplete status.

Target identities contain instrument, configured timeframe, source opening/completion instants, and calendar
revision. Each identity is emitted once per coordinator instance.

## Files changed

- `AlgoTrading.Models/ITradingSessionCalendar.cs`: exposes the selected immutable revision.
- `AlgoTrading.Models/IntradayCandleClock.cs`: aggregation-only special-session completion method.
- `AlgoTrading.Models/CandleAggregator.cs`: uses the aggregation completion boundary.
- `AlgoTrading.Models/MarketData.Processing/*`: provider event, statuses, result/output envelopes, and coordinator.
- `AlgoTrading.RuleTests/ClosedCandleProcessingCoordinatorTests.cs` and `Program.cs`: synthetic deterministic
  verification.

## Verification

Synthetic fixtures cover normal 5m to 15m/25m/60m/75m aggregation, shortened final bars, explicit closed days,
explicit special sessions, missing intraday/daily sources, wrong input, out-of-order input, duplicate replay,
session-state reset, daily-to-weekly/monthly aggregation, no early future visibility, and calendar revision
provenance. These fixtures demonstrate platform behavior only; they are not exchange-market validation.

Run:

```powershell
dotnet run --project AlgoTrading.RuleTests/AlgoTrading.RuleTests.csproj --no-restore
dotnet build AlgoTrading.csproj --no-restore -m:1
```

## Deployment manifest

Deploy `AlgoTrading.Models.dll` with the main application and DataAccess binary built against it. Configure and load
the selected calendar and Batch 2 snapshot before constructing one coordinator per instrument. Do not send forming
candles or raw WebSocket protocol frames to this boundary.

No database schema or stored market data is changed by this batch.

## Rollback

Restore the prior Models, DataAccess, and application binaries together. No data rollback is required because this
batch adds in-memory processing only.

## Known limitations

- Coordinator deduplication is in memory. Restart-safe output identity is Batch 6.
- Processing is single-instrument and synchronous by design. The bounded multi-instrument worker pipeline is Batch 4.
- A live WebSocket adapter is not introduced here.
