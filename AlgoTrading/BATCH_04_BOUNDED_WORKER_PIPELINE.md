# Batch 4 — Live bounded worker pipeline

## Scope

`BoundedMarketDataPipeline` is the in-process path for live provider-finalized source events. It is independent of
WebView/WebSocket protocol capture and invokes the Batch 3 processing contract only after ingress validation.

## Capacity and ordering policy

- Total configured capacity is divided deterministically across bounded instrument shards.
- Every shard uses `BoundedChannelFullMode.Wait`, a single reader, and multiple writers.
- Instrument token hashing always routes one instrument to one shard, preserving event order for that instrument.
- Different shards execute concurrently, allowing independent instruments to progress in parallel.
- `EnqueueAsync` applies back-pressure until capacity is available.
- `TryEnqueue` never evicts accepted work; it returns `QueueFull`.
- Capacity must be at least the shard count.

Only provider-finalized candles whose completion is observable at the event observation instant are accepted.
Stable source identity deduplicates repeated/replayed provider events before processing.

## Processing and failure policy

Each shard lazily creates one Batch 3 processor per instrument through
`IInstrumentCandleProcessorFactory`. `ClosedCandleCoordinatorFactory` is the production bridge from hydrated
selected calendars and an active configuration snapshot.

Coordinator processing happens once. The downstream sink receives the immutable result and is retried up to the
configured attempt count. This avoids mutating coordinator state twice during a sink retry. Terminal processor or
sink failure:

- is recorded with source identity, instrument, attempts, and message;
- increments failure metrics;
- releases the ingress deduplication identity so operations can replay it;
- does not terminate the shard worker or hide later events.

## Shutdown policy

- Graceful stop completes every writer and drains all accepted work.
- Immediate stop cancels workers, removes unprocessed identities, and reports every remaining/in-flight accepted
  item through dropped/unprocessed metrics.
- A stopped instance cannot restart.

## Metrics

Snapshots expose current pending depth, accepted/processed/rejected/dropped counts, sink retry count, terminal failure
count, safely unprocessed shutdown count, total processing duration, and maximum observed processing duration. No
fixed latency target is claimed; deployment-machine measurement is required.

## Files changed

- `AlgoTrading.Models/MarketData.Pipeline/*`: pipeline contracts, options, statuses, metrics, failures, factory, and
  bounded sharded worker.
- `AlgoTrading.Models/MarketData.Processing/ClosedCandleProcessingCoordinator.cs`: implements the worker processor
  contract.
- `AlgoTrading.RuleTests/BoundedMarketDataPipelineTests.cs` and `Program.cs`: deterministic concurrency and
  lifecycle verification.

## Verification

Synthetic tests cover a 206-instrument burst, per-instrument ordering, ingress deduplication, deterministic bounded
capacity, asynchronous back-pressure, graceful drain, immediate-stop reporting, transient sink retry metrics, and
continued processing after terminal consumer failures. No live socket, broker, exchange, or SQL endpoint is used.

Run:

```powershell
dotnet run --project AlgoTrading.RuleTests/AlgoTrading.RuleTests.csproj --no-restore
dotnet build AlgoTrading.csproj --no-restore -m:1
```

## Deployment manifest

Construct the factory only from preloaded calendars and the activated Batch 2 snapshot. Choose capacity and shard
count from measured deployment-machine load. Start the pipeline before attaching the protocol adapter. During
shutdown, stop provider ingress first and then call graceful stop unless operations explicitly accepts reported
unprocessed work.

No database schema or stored data changes are introduced.

## Rollback

Detach the live input adapter, request graceful drain, record any reported failures, and restore the prior Models and
application binaries together. No database rollback is required.

## Known limitations

- The pipeline is in-process; restart durability for calculated output is Batch 6.
- No Kite/WebView protocol adapter is changed in this batch.
- Hydrated-calendar construction and pre-market readiness are Batch 7.
