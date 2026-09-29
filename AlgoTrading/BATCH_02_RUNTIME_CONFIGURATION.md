# Batch 2 — Runtime configuration activation

## Scope

`Config_Timeframes` and `Config_CriticalLevels` now cross a validated startup boundary before any background
market-data processing is marked ready.

`PlatformConfigurationProvider.LoadActiveConfigurationAsync` reads both tables without filtering first.
`PlatformConfigurationValidator` validates the complete timeframe set and the structural fields/JSON of every
calculator row. Active calculator rows receive method-specific validation. The resulting immutable
`PlatformConfigurationSnapshot` contains:

- a `TimeframeCatalog` whose `ActiveCodes` are the only downstream aggregation targets;
- only active calculator rules whose applied and reference timeframes are also active;
- a deterministic SHA-256 configuration revision over all rows and enabled states.

`PlatformConfigurationRuntime` is the thread-safe runtime boundary. The main application registers the provider and
runtime in dependency injection and initializes them before creating the main window. Invalid or unreachable runtime
configuration produces a clear startup error and no market-data-ready state.

## Validation policy

- Intraday codes must use canonical `Xm` form, have a matching positive five-minute multiple, and use
  `INTRADAY_5M`.
- Calendar codes must be exactly `1D`, `1W`, or `1M`, use zero minutes, and use `DAILY_1D`.
- Applied and non-empty reference timeframes must exist.
- `MinimumBarsRequired` must be positive.
- Every parameter payload must be a JSON object.
- `PIVOT_STANDARD` must identify `SQL_RANGE_EXTENSION_V1` and use `1D` applied/reference timeframes.
- EMA/SMA crossover lengths must be positive, fast must be less than slow, the function must match the method, and
  minimum bars must be at least slow length plus one.
- Unsupported active methods fail startup. Disabled unsupported methods remain stored but cannot execute.

Disabling a timeframe removes it from active targets and suppresses any rule depending on it. Disabling a calculator
removes it from the activated rule list.

## Empty and unavailable configuration

Runtime activation does not synthesize a live pipeline:

- Empty `Config_Timeframes`: startup fails as not configured.
- Empty `Config_CriticalLevels`: startup succeeds with no calculator work.
- Unreachable tables: startup fails with contextual configuration diagnostics.

The older `GetTimeframeCatalogAsync` catalog-only API retains its prior default catalog when the timeframe table is
empty for compatibility with non-runtime callers. The startup/runtime API never uses that fallback.

## Seed correction

For new or deliberately re-seeded configuration:

- EMA crossover is now fast 5 / slow 20 with `MinimumBarsRequired = 21`.
- `SWING_REVERSAL` is disabled because no formula/version/acceptance fixture has been approved.
- Re-running the seed explicitly disables an existing `SWING_REVERSAL` row while preserving operator enable/disable
  choices for supported rules.

## Files changed

- `AlgoTrading.Models/Configuration/*`: immutable active rules, method codes, and startup snapshot.
- `AlgoTrading.DataAccess/Infrastructure/PlatformConfigurationProvider.cs`: validated runtime load.
- `AlgoTrading.DataAccess/Infrastructure/PlatformConfigurationValidator.cs`: validation and activation.
- `AlgoTrading.DataAccess/Infrastructure/PlatformConfigurationRuntime.cs` and interface/exception files: startup
  state boundary.
- `AlgoTrading.DataAccess/Sql/003_ConfigSeed.sql`: safe EMA sample and disabled unapproved swing sample.
- `App.xaml.cs`: dependency registration and fail-closed startup initialization.
- `AlgoTrading.RuleTests/PlatformConfigurationTests.cs` and `Program.cs`: deterministic verification.

## Verification

Tests cover active loading/use, disabled timeframe suppression, disabled calculator suppression, invalid active
parameters, empty tables, invalid source streams, and invalid JSON. They use in-memory rows and make no SQL or market
requests.

Run:

```powershell
dotnet run --project AlgoTrading.RuleTests/AlgoTrading.RuleTests.csproj --no-restore
dotnet build AlgoTrading.csproj --no-restore -m:1
```

## Deployment manifest

1. Back up the current binaries and record current configuration rows.
2. Review active rows against the validation policy before deploying.
3. Apply `003_ConfigSeed.sql` only when the seed update is intended; it disables `SWING_REVERSAL`.
4. Deploy the main application, Models, and DataAccess binaries together.
5. Start the application and record the loaded configuration revision.

No price, pivot, or calendar data is deleted or rebuilt.

## Rollback

Restore the prior application, Models, and DataAccess binaries together. If the seed correction was applied and the
unapproved swing row must be restored for a non-production comparison, restore that row from the configuration backup
instead of guessing its prior values. Do not reactivate it in the market-data pipeline without separate formula and
fixture approval.

## Known limitations

- This batch activates configuration and exposes the safe snapshot. The closed-candle coordinator that consumes it is
  Batch 3.
- Configuration is loaded once at startup. Controlled live reload is not introduced here.
