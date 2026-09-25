# Candle timing fixtures — Batch 07

These are manually specified synthetic fixtures, not database exports or market observations. No database connection is required to run CandleTimeTests.

## Independent expected values

For the supplied example session 09:15–15:30 IST, elapsed duration is 375 minutes. Hourly starts occur every 60 minutes from 09:15: the sixth starts at 14:15 and completes at 15:15, while the seventh starts at 15:15 and completes at session close, 15:30. For 20-minute intervals, 18 full intervals consume 360 minutes; for 45-minute intervals, eight consume 360 minutes. Both therefore have a final 15-minute interval. These facts determine the explicit expected-close columns in candle-time-boundaries.csv.

The 18:00–19:10 example is deliberately different from the daytime session and is not a claim about an actual exchange date. A 20-minute candle starting 19:00 has only ten session minutes remaining. The UTC input at 03:45 is the same instant as 09:15 IST; subtracting the fixed five-hour-thirty-minute offset establishes the timestamp expectation independently of the computer timezone.

Invalid rows cover misalignment, starts outside the session, nonpositive/oversized intervals and reversed session bounds. Durations longer than a supplied session are explicitly rejected in this batch. Session break policies, aliases and calendar timeframes are not inferred here.

candle-time-replay.csv contains two finalized hourly-series candles. Their closes are 99 then 101; comparing with 100 produces one crossed-above event at 15:30, never before. The first candle closes at 15:15 and the second is shortened to fifteen minutes. Prefix-only and full-history rule evaluations must agree. Repeated processing must preserve a single event and must not invent a next candle.

Fixed OHLC values in boundary tests are neutral validation scaffolding. Separate malformed-candle checks deliberately use reversed high/low, negative volume and a different instrument. Tests explicitly pass the provider-finalized flag: this flag is an assertion by a future provider, not a completeness check performed by the clock.

## Integration boundary

MarketTimestamp handles IST database wall times. Candle retains SourceTimestamp and exposes OpenedAt with an explicit IST offset; its existing Timestamp property continues to return UTC. Unspecified constructor input now means IST. UTC input keeps its instant; machine-local DateTime is rejected, requiring an explicit UTC conversion by that caller. No audited active caller in AlgoTrading or its editor supplied machine-local candle timestamps.

IntradayCandleClock takes a caller-supplied TradingSession and a duration. It validates alignment, instrument and candle values and supplies CompletedCandle for the existing rules engine only after completion and explicit provider finalization. It does not look up holidays, verify missing source rows, aggregate candles or infer the duration from a timeframe label. The caller must supply the duration associated with that series. It supports one continuous trading session per invocation.

The existing SQL crossover diagnostic still uses its configured nominal duration and historical completeness assumptions. Only its timestamp conversion has been aligned in this batch. Calendar lookup and full provider integration remain subsequent batches; do not treat that diagnostic as the new session-aware production provider.
