# Deterministic Tick-Based, Server-Authoritative Game Engine (Pure C#)

This repo is a **server-authoritative**, **deterministic**, **fixed-tick** simulation core written in **pure C#**.
Networking, timing, and presentation are adapters around that core.

The key idea is to keep a **single authoritative source of truth**:

- **ServerEngine** owns the authoritative simulation and tick counter.
- Players send **intent** (commands), never state.
- The server emits a compact, ordered stream of **replication ops** (**RepOp**) that clients use to reconstruct a **ClientModel**.
- In-process, the **client can consume the server’s TickResult directly** (no encode/decode required). Over the network, adapters translate between `TickResult` and wire messages.

---

## Core Contract (what everything revolves around)

### TickResult = canonical output of one server tick
`ServerEngine.TickOnce(ctx)` returns a `TickResult`:

- `Tick` (authoritative tick index)
- `ServerTimeMs` (timing info for presentation / metrics)
- `StateHash` (hash of the authoritative world **after** the tick)
- `Ops` (`RepOpBatch`: ordered replication ops for the tick)
- optional `Snapshot` (`ServerModelSnapshot`, typically for baseline / debug / tick 1)

This is the “handoff object” between server simulation and all downstream consumers:
- **ClientEngine** (in-process)
- **NetworkServer** (wire encoding + reliability)
- tests / harness programs

---

## High-level control flow (verified against the code)

### Server control flow (proactive)
**ServerEngine** runs simulation proactively at a fixed tick rate:

1. **Commands are buffered** by tick (players, bots, scripted server input)
2. `TickOnce()` executes a deterministic pipeline:
   - `CommandSystem.Execute(tick, world, ops)`  
     - dispatches buffered commands into sink inboxes  
     - executes sinks in **stable order** (determinism)
     - handlers emit `RepOp` via `OpWriter` (emit + apply)
   - replication: ops are recorded as the authoritative log
   - `StateHash = ServerModelHash.Compute(world)`
3. `TickOnce()` returns `TickResult` (ops + hash + optional snapshot)

**Matches your description:**  
`ServerEngine -> TickOnce -> Commands (buffer/consume) -> RepOps -> ServerModel (mutate)`

### Client control flow (reactive)
**ClientEngine** is reactive: it only changes state when it receives server output.

1. Apply a baseline (snapshot) once, then
2. Apply ordered tick updates (`TickResult`) as they arrive
   - out-of-order ticks are buffered (`SortedDictionary<int, TickResult>`)
   - ticks apply strictly in order (tick N+1 after N)
3. For each tick:
   - mutate `ClientModel` by applying `RepOp`s
   - publish events (e.g. `GameFlowStateTransitionEvent`) to notify presentation

**Matches your description:**  
`ClientEngine -> TickResult (consume) -> ClientModel (mutate) + Events -> Presentation`

### Player control flow (intent-only)
1. Presentation/input produces `EngineCommand<EngineCommandType>`
2. Client assigns:
   - `requestedClientTick` (client’s best guess of server tick to apply on)
   - `clientCmdSeq` (monotonic sequence)
3. Commands are enqueued into the server (`ServerEngine.EnqueueCommands(...)`) and validated/scheduled by `EngineCommandBuffer`.

**Matches your description:**  
`Commands (enqueue) -> ServerEngine (buffer commands)`

---

## Repository layout (ownership boundaries)

### `netlogic.core` (the engine and adapters)
**Simulation core**
- `Sim/ServerEngine/*`
  - `ServerEngine` (authoritative tick execution)
  - `TickResult` (server → client contract)
  - replication primitives: `RepOp`, `RepOpBatch`, recorder/replicator
- `Sim/ClientEngine/*`
  - `ClientEngine` (client-side reconstruction)
  - `ClientModel` + event publication for presentation hooks

**Command system**
- `Command/*`
  - `EngineCommandBuffer` (tick scheduling + validation)
  - `CommandSystem` (dispatch routes + sink execution order)
  - sinks implement `ICommandSink<T>` (e.g. `GameFlowSystem`, `MovementSystem`)

**Networking adapters (optional)**
- `Sim/NetworkServer/*`
  - owns transport, handshake, reliability (ack/replay), baseline policy
  - converts inbound client ops → commands → feeds `ServerEngine`
  - converts `TickResult` → wire messages (MustHave + NiceHave lanes)
- `Sim/NetworkClient/*`
  - owns client transport endpoint
  - decodes wire → baseline/ops → feeds `ClientEngine`
  - encodes commands → wire

**Timing**
- `Sim/ServerEngine/Timing/*`
  - `ServerTickRunner` schedules ticks in realtime and calls a tick callback

### `netlogic.app` (entry point / harness)
- `Main.cs` selects a program to run
- Current default is `LocalClientEngineProgram` (in-process end-to-end without transport):
  - runs server ticks
  - pipes `TickResult` directly to `ClientEngine`
  - exercises determinism, replication, and client reconstruction

---

## Replication model (how the client stays in sync)

Replication is expressed as a stream of fixed-width `RepOp` records.

- **MustHave ops**: things that must not be dropped (flow state, entity lifecycle)
- **NiceHave snapshots**: presentation-only “latest wins” data (e.g. positions)

`ClientEngine` currently applies both lanes by filtering over the same ops:
- MustHave pass: apply only “MustHave” types
- NiceHave pass: apply only `PositionSnapshot`

This keeps simulation truth on the server, while allowing “NiceHave” updates to be drop-safe.

---

## Determinism guardrails (what the code is doing right)

- Stable execution order is explicit (`CommandSystem` executes sinks in fixed order)
- Commands are applied in a tick domain via `EngineCommandBuffer` (late/future handling is centralized)
- Replication ops are recorded during the tick and flushed as an ordered batch
- The server computes a post-tick `StateHash` for verification/debugging

---

## Suggested improvements (practical, high leverage)

### 1) Make “contract types” explicit and minimal
Right now `TickResult` is already close to the ideal contract. To tighten it further:
- Consider a `ServerTickPacket` that **always** contains:
  - `tick`, `hash`, `opsMustHave`, `opsNiceHave`, optional `baselineSnapshot`
- This makes lane separation explicit and removes the client’s double-pass filtering.

### 2) Separate MustHave vs NiceHave at recording time
Today the client filters by `RepOpType`. Instead:
- record into two buffers (or one buffer with two spans) on the server
- encode/decode lanes independently
Benefits:
- less work on client (no second pass)
- clearer semantics (“NiceHave must never affect truth” becomes structural)

### 3) Add a deterministic test harness (hash lockstep)
You already compute `StateHash`. Use it to enforce determinism:
- run the same scripted command stream twice and assert identical hashes per tick
- (optional) run server and client reconstruction and assert they converge on expected observable state

### 4) Formalize “command → system ownership” documentation
Command routing is strict (one sink owns one command type). Add a small doc section (or code comments) explaining:
- how to add a new command type
- which system owns it
- what replication ops it should emit

### 5) Reduce lifetime hazards from pooled buffers
`RepOpBatch` can be pooled. You already have `TickResult.WithOwnedOps()` for buffering.
Two further safety options:
- enforce “owned ops only” at the public boundary (especially across threads)
- or introduce a `readonly struct TickResultOwned` for long-lived storage

### 6) Make simulation time deterministic by construction
`ServerTimeMs` is for presentation/metrics. Ensure no simulation code reads wall-clock time.
If any gameplay logic needs time, prefer tick counts or fixed-step accumulators derived from tick.

### 7) Add a short “how to run” and “how to extend” section
People will copy your engine. A tight README addition helps:
- run the in-process harness
- add a new system/sink
- add a new RepOp + client application

---

## TL;DR
- **ServerEngine** is the only place authoritative state mutates.
- Players send **commands** (intent), buffered per tick.
- The server outputs **TickResult** (hash + ordered RepOps).
- **ClientEngine** consumes TickResult reactively to rebuild ClientModel and emit presentation events.
