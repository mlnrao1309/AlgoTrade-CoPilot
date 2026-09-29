# Scope decision

The earlier 25-part backlog mixed essential personal-bot behavior with institutional infrastructure. This build closes the functional authoring and backtest layer with one simple strategy document and explicitly excludes version graphs, approval/correction workflows, multi-user infrastructure, and an editor-specific database.

Implemented now: strategy identity, one instrument/session/timeframe, long or short direction, the existing full nested rule language, initial protection, ordered partial exits with original/remaining basis, full exit, essential collision/re-entry policy, explanation, path-specific validation, legacy import, strict schema rejection, Save/Save As, unsaved-change prompts, read-only open, file conflict detection, bounded undo/redo, runtime snapshot binding, and deterministic completed-candle CSV backtesting.

Intentionally deferred to real runtime/data providers: symbol availability and expiry lookup, live calendar preview, pivot/critical-level price resolution, broker order/fill state, lot rounding, fees, and slippage. The local backtester rejects provider-dependent level strategies rather than faking values.
