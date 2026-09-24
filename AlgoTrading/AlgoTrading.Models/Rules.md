# Rule definitions and strategy execution binding

This layer provides saveable definitions and validated execution for a future visual rule builder. Users will choose readable values, operators and groups; the builder will construct these definitions. It is not a visual editor, text-language parser, strategy scheduler or order executor.

## Supported selections

- All, Any and Not condition groups, including nested groups.
- Enable/disable a child condition and attach comments. Empty groups and groups with no enabled conditions cannot execute.
- Current or previous candle open, high, low, close and volume.
- Fixed numbers; addition, subtraction, multiplication, division and nested brackets.
- Rounding, with midpoint values rounded away from zero.
- Simple Moving Average, Exponential Moving Average, Relative Strength Index, the three Bollinger bands and SuperTrend.
- Indicator sources can be candle fields, arithmetic expressions or nested indicators. An omitted source means close on the indicator timeframe. SuperTrend always uses high, low and close together.
- Greater than, greater than or equal to, less than, less than or equal to, equal, not equal, crossed above and crossed below.
- Previous-value references and counts of matching conditions over completed-candle windows.

Named pivot-point selections, market-segment mapping, a visual editor and strategy scheduling are outside this increment. Pivot arithmetic can be expressed using previous daily high, low and close; the engine does not claim to reproduce Chartink-specific pivot conventions.

## Binding contract

1. Construct a RuleDefinition from the builder's selections. Name the evaluation timeframe explicitly.
2. Save/load through RuleDefinitionJson. Definitions contain data, not delegates or executable strings. Required fields and unrecognized document fields are checked.
3. Call RuleBinder.Bind. Invalid selections are rejected before execution. The returned plan lists the required timeframes and expanded nested indicator calculations, with dependencies listed before their consumers. Description supplies a readable preview.
4. Fetch the requested completed candle series for one instrument. Timeframe identifiers are exact provider-defined keys such as "15 minute" and "2 hour". They are not parsed or silently resampled.
5. Construct RuleMarketData with CompletedCandle entries. The provider must pass finalized candles and their actual close timestamps, including exchange/session boundaries. Candle.Timestamp is not assumed to be an opening or closing timestamp. Close times must be unique and increasing per timeframe. The provider is responsible for historical completeness and choosing session versus fixed-duration bars.
6. Call BoundRule.BindData once per immutable market-data snapshot. Then call BoundRuleExecution.Evaluate(asOf) repeatedly for historical evaluation. Supply a new snapshot when newly finalized data arrives.

Both bindings copy caller-owned lists. Editing a saved rule or an input candle array does not change an existing execution. Evaluation of a binding is synchronized and its computed indicator series are reused.

## Completed candles and crossovers

The evaluation clock is the rule's explicit EvaluationTimeframe. An asOf request between candle closes evaluates at the latest completed evaluation candle and reports that timestamp. A scheduler can use this timestamp to avoid firing twice for the same candle. This layer does not submit orders or deduplicate signals.

Each operand uses the latest candle whose ClosedAt is at or before the evaluation timestamp. Different timeframe arrays are never matched by index. If two timeframes close simultaneously, both finalized values are available. There is no partial-candle option.

Crossed above means previous left <= previous right AND current left > current right. Crossed below means previous left >= previous right AND current left < current right. Previous means the previous completed evaluation-clock candle; both operands are independently aligned at that earlier timestamp. A crossover inside Count uses the count's timeframe as its evaluation clock.

For a fifteen-minute moving average crossing a two-hour moving average, select fifteen minutes as the rule evaluation timeframe. The two-hour value is held until another two-hour candle closes. No future daily or two-hour final values are used in historical comparisons.

Selecting low as Relative Strength Index input computes the indicator from lows. The crossover still compares the indicator output, not the candle price. To compare a price directly, select a candle field without an indicator.

## History and calculation conventions

Simple Moving Average and Bollinger bands require a complete window of finite source values. Relative Strength Index requires Length price changes, then uses Wilder smoothing; a flat series returns 50. Moving averages of Relative Strength Index wait for the nested warmup. Exponential Moving Average seeds from its first valid source value, matching the existing candle indicator implementation. SuperTrend includes the first candle's high-low range in its initial Average True Range and begins with an upward direction, matching the existing implementation.

Initial unavailable values do not permanently poison nested averages. Division by zero, nonfinite results and insufficient history are unavailable. Comparisons return InsufficientData in that case, including NotEqual. Not preserves this state; it cannot turn missing history into a signal. All returns false if any child is false; Any returns true if any child is true; otherwise unavailable children keep the group unavailable.

Counts require the full requested window and available results for every counted condition. Offsets count actual supplied candles, not wall-clock intervals. Negative offsets are rejected. Numeric equality is exact; use explicit rounding when rounded equality is intended.

Ordinary comparisons are considered before crossover-dependent conditions within an All group. Group boundaries and logical meaning are preserved. Conditions may short-circuit; the explanation identifies the deciding condition rather than claiming every child was evaluated.

## Regression suite

Run the sibling AlgoTrading.RuleTests console project with dotnet run. It has no external test-package dependency and fails with a nonzero exit code if an assertion fails. It covers the three requested strategy shapes, nested calculations, selected sources, mixed timeframe crossovers, simultaneous closes, future-data isolation, unavailable history, validation, serialization and immutable bindings.

## Crossover events

Use BoundRule.CreateCrossoverMonitor to create a persistent monitor for one rule and one instrument. Subscribe using named event-handler methods to CrossoverDetected and, if next-candle context is needed, CrossoverNextCandleAvailable. Call the monitor's Process method with the latest immutable completed-candle data snapshot and the observation timestamp. Keep the same monitor when new finalized candles arrive. Ordinary BoundRuleExecution.Evaluate remains a side-effect-free query; the event monitor is the execution entry point for event-driven strategies.

On its first Process call the monitor scans all supplied completed history. Pass startAfter when creating the monitor to exclude older signals while retaining that history for indicator calculation. Subsequent calls scan only newly completed candles, including candles between polling calls. Observation times must move forward. The monitor is in-memory: create a new monitor to replay history or apply corrected historical data. It is not a durable event store.

CrossoverDetected is raised for each enabled crossover clause, independently of whether another condition makes the full strategy fail. OverallRuleEvaluation distinguishes a crossover from a full strategy match. Disabled clauses, including descendants of disabled groups, are excluded. A crossover used inside Count is reported at its actual occurrence time on the count timeframe; evaluating the same lookback window repeatedly does not emit it repeatedly. Structurally separate clauses have separate ConditionPath identifiers, even if their text is identical.

Each immutable CrossoverOccurrence includes:

- Event identity, rule name, condition path and readable condition description.
- Instrument, crossover direction and evaluation timeframe.
- Actual occurrence timestamp and observation timestamp.
- Previous and current evaluation-clock candles, including the original Candle values and explicit close times.
- Both operands' previous and current calculated values. Indicator metadata includes function, length and multiplier. Source inputs are recursively represented, so an exponential average of another exponential average retains both levels and their candle source. Relative Strength Index values are separate from its underlying price.
- Each candle-backed operand's actual source candle, timeframe and sampling timestamp. In mixed-timeframe comparisons, the slower operand may legitimately refer to the same completed candle in previous and current samples.
- Optional next evaluation-clock candle and operand values. Count operands report their aggregate value rather than duplicating every internal lookback comparison.

Next is included in the initial event only if that next candle has already completed by the Process observation timestamp, as during historical replay. A future row already present in a backtest array is not enough: its close timestamp must have been reached. The next candle never participates in crossover detection or OverallRuleEvaluation at the occurrence timestamp.

When the next candle was unavailable, a later Process call raises CrossoverNextCandleAvailable once, with the same EventId and updated immutable context. It does not raise CrossoverDetected a second time or mutate the original payload. Supply complete historical series so that the immediate next candle is retained between snapshots; actual missing provider data cannot be reconstructed by the monitor.

Process calls on one monitor cannot overlap or re-enter from event handlers. Event state is committed before callbacks run. Every subscriber is attempted even if another throws; subscriber errors are aggregated after delivery. Retrying Process does not replay committed events. Consumers requiring durable retries must persist received events themselves. New crossover notifications are ordered by occurrence timestamp, then condition path; next-candle updates are delivered separately afterward.

The event regression tests use synthetic completed candles, including the requested ten-period exponential average of highs crossing a nested ten-period exponential average of closes. They cover Relative Strength Index payloads, both crossover directions, mixed timeframes, future-row exclusion, already-known next candles, later next-candle completion, replacement snapshots, disabled conditions, Count deduplication and subscriber failure.
