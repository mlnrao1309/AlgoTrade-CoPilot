# Scope decision

The earlier 25-part backlog mixed essential personal-bot behavior with institutional infrastructure. This build closes the functional authoring layer with one simple strategy document and explicitly excludes version graphs, approval/correction workflows, multi-user infrastructure, internal databases, and a backtest engine.

Implemented now: strategy identity, one instrument/session/timeframe, long or short direction, the existing full nested rule language, readable pivot/critical-level selectors, stable level binding metadata, a bounded retest window, opposite-cross invalidation, initial protection, ordered partial exits with original/remaining basis, full exit, essential collision/re-entry policy, explanation, path-specific validation, legacy import, strict schema rejection, Save/Save As, unsaved-change prompts, read-only open, file conflict detection, bounded undo/redo, and runtime snapshot binding.

Intentionally deferred to the existing runtime/data providers: symbol availability and expiry lookup, live calendar preview, pivot/critical-level price resolution, order/fill state, quantity/lot rounding, fees/slippage, and backtesting. Those cannot be truthfully implemented inside an offline editor without the provider/coordinator contracts and should not be faked.
