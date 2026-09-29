# Batch 1 — Durable historical-ingestion checkpoint integration

## Scope

Historical ingestion now treats `dbo.IngestionSyncState` as the durable continuation boundary for the two
authoritative source streams:

- `INTRADAY_5M` maps to Kite `5minute` and database timeframe `5`.
- `DAILY_1D` maps to Kite `day` and database timeframe `D`.

Derived 15-minute and 60-minute candles are no longer downloaded by the default historical source list. They remain
the responsibility of the aggregation pipeline.

Each range is inclusive. A normal run reads the instrument/stream checkpoint and begins on the day after
`LastCompletedChunkEndDate`. The effective range is the intersection of the explicitly requested range and the
configured provider coverage supplied in `HistoricalBackfillRequest`.

The SQL repository parses and validates the provider payload, stages candles in a transaction-local table, upserts
the stable candle identity `(InstrumentToken, TimeFrame, TimeStamp)`, and advances the checkpoint in the same
serializable SQL transaction. An empty but valid `data.candles` array still completes the requested coverage.
Malformed, duplicate, or out-of-range provider rows fail the chunk.

Failures stop that instrument/stream at its first gap. Other independent streams and instruments may continue.
Download or persistence failure does not advance state. Budget expiration returns a resumable report and leaves the
last committed checkpoint accurate.

## Data corrections

Corrections are explicit: construct `HistoricalBackfillRequest` with `refreshCorrections: true`. This bypasses the
old checkpoint for the requested correction range and updates existing candle identities. Correction writes do not
move a later continuation checkpoint backwards. An ordinary resume never silently performs a correction refresh.

## Files changed

- `AlgoTrading.Models/MarketData.Ingestion/*`: stream identities, requests, repository/source/clock abstractions,
  coordinator, checkpoint, and report.
- `AlgoTrading.DataAccess/Historical/SqlHistoricalIngestionRepository.cs`: atomic SQL candle/checkpoint writer.
- `AlgoTrading.DataAccess/AlgoTrading.DataAccess.csproj`: explicit SQL client dependency aligned to the existing
  test dependency.
- `Models/HistoricalDataModel.cs`: Kite adapter and runtime wiring to the coordinator/repository.
- `ViewModels/WebViewViewModel.cs`: report namespace import only; no editor behavior was changed.
- `AlgoTrading.RuleTests/HistoricalIngestionTests.cs` and `Program.cs`: deterministic Batch 1 verification.

## Verification

The deterministic suite covers fresh start, restart, failed download, failed persistence, idempotent replay, budget
expiration, instrument/stream isolation, prefix-versus-full replay equivalence, and explicit correction refresh.
Tests use synthetic fakes and make no NSE, Kite, or SQL connection.

Run:

```powershell
dotnet run --project AlgoTrading.RuleTests/AlgoTrading.RuleTests.csproj --no-restore
dotnet build AlgoTrading.csproj --no-restore -m:1
```

## Deployment manifest

1. Preserve the application binaries and configuration currently deployed.
2. Verify `002_CriticalLevels.sql` has already created `dbo.IngestionSyncState`.
3. Deploy the rebuilt main application, `AlgoTrading.Models.dll`, and `AlgoTrading.DataAccess.dll` together.
4. Do not delete or rebuild `dbo.Instruments_OHLC` or `dbo.IngestionSyncState`.
5. Run a bounded synthetic or non-production backfill first and confirm the checkpoint advances only after candles
   are visible.

No database migration is required by this batch.

## Rollback

The pre-change Git commit is `17fac9eaa76663bb3aedfa9da3a1d79483c62951`. The pre-existing tracked working-tree
diff was saved separately before editing. To roll back deployment, restore the prior application binaries as one
unit. Do not delete candle or checkpoint rows: the older application ignores checkpoints, and preserving them keeps
the forward path recoverable. Reapplying this batch safely resumes from the retained state.

## Known limitations

- The SQL transaction path is compiled and covered through the repository contract, but no live database is touched
  by the deterministic test suite. Isolated-database verification belongs in the end-to-end batch.
- Existing duplicate rows already present in `dbo.Instruments_OHLC` are not destructively cleaned by this batch.
  New writes through this repository are idempotent by stable candle identity.
- The compatibility `years` overload remains for the current UI caller. New background callers should use the
  explicit `HistoricalBackfillRequest` overload with configured coverage.
