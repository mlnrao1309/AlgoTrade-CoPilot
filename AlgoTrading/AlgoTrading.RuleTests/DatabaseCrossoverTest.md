# Database crossover integration test

This optional test reads SQL Server with Windows authentication. It never writes to the database. The normal regression run remains synthetic and does not connect to SQL Server.

## Run

Run the existing AlgoTrading.RuleTests project with arguments:

    --database-crossovers [output-directory]

For example, from the solution directory:

    dotnet run --project AlgoTrading.RuleTests/AlgoTrading.RuleTests.csproj -- --database-crossovers

Without an output-directory argument, each run creates a timestamped folder under the executable's logs directory. Supply a new directory when preserving previous run results; explicit output filenames are replaced on rerun.

## Settings

Set `ALGOTRADING_SQL_CONNECTION_STRING` before running the test. `database-test.settings.json` is copied alongside the executable but deliberately contains no machine name or credential. A local untracked connection string in that file remains supported for development.

The defaults select instrument 265 and timeframe 15minute. TimestampTimeZoneId is India Standard Time; TimestampRepresentsCandleOpen is true. This matches the importer using DateTime.Parse on the source candle timestamp on this machine. The test converts that timestamp to an instant and adds fifteen minutes to obtain completion time. LastUpdated is logged as source metadata, not used as candle completion time. Change these explicit settings if the source timestamp convention changes. Only rows completed by the captured observation time are evaluated.

The read uses the supplied SELECT columns, parameterized instrument and timeframe filters, and ORDER BY TimeStamp ASC. Duplicate timestamps and invalid price ranges fail the test rather than being silently discarded. No time range or row limit is applied.

## Strategy

All four conditions must match:

1. The five-period Exponential Moving Average of close crosses above the five-period Exponential Moving Average of that first average.
2. Current close exceeds SuperTrend with length ten and multiplier three.
3. Current close exceeds the five-period Exponential Moving Average of close.
4. Current close exceeds the previous candle high.

All expressions use the fifteen-minute timeframe. Every configured crossed-above event is logged, even if one of the other conditions fails. A full strategy match is distinguished from a crossover occurrence. Crossed-below events are not part of this strategy.

## Outputs and validation

- crossovers.csv: one row per crossover, with timestamps, previous/current average values, close, previous high, each condition status, overall match and next-candle timestamp when available.
- crossovers.jsonl: one JSON object per crossover, including full event context, nested indicator values, all selected database columns for previous/current/next rows, and individual condition explanations.
- rule.json: the saved generic rule definition used by the test.
- summary.txt: row coverage, timestamp convention, crossover count, full strategy match count and validation outcome.

Historical replay can include an already-completed next candle for context; it never participates in deciding the crossover or the strategy result at the event time.

The test independently calculates both exponential averages using their recurrence and compares every expected crossing timestamp with the event log. It also checks that the complete strategy result equals the conjunction of the four independently evaluated conditions and that repeated processing emits no duplicates. Zero crossovers is a valid result and is not fabricated into a passing signal. Database, configuration and validation errors fail the test.

All newly added test components use named classes and methods in separate files.
