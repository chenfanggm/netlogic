# Deterministic Tick-Based, Server-Authoritative Game Engine (Pure C#)

This repo is a **server-authoritative**, **deterministic**, **fixed-tick** simulation core written in **pure C#**.  
Networking, realtime scheduling, and presentation are **adapters** around that core.

Core principles:

- **ServerEngine is the single source of truth** and the only place authoritative state mutates.
- Players send **intent** (commands), never state.
- The server produces a compact, ordered stream of **replication ops** (`RepOp`) that clients apply to reconstruct `ClientModel`.
- In-process, the client can consume server output **directly** (no encode/decode). Over the network, a codec maps between the sim contract types and transport messages.

---

## Architecture rule: Sim contract types vs transport types

**ServerEngine / ClientEngine must not use transport terms** like *Frame* or *Packet*.  
They only produce/consume **well-typed simulation contract types** for clarity and determinism.

- **Simulation contract (sim layer):**
  - baseline snapshot
  - authoritative tick output
  - replication delta

- **Transport (network layer):**
  - packets / frames / messages
  - reliability (ack/replay)
  - batching / MTU splitting
  - encoding / decoding

---

## Core contract (what everything revolves around)

### BaselineResult = explicit baseline snapshot
A baseline snapshot is applied explicitly (typically once on connect or recovery).

- `Snapshot` (`ServerModelSnapshot`)

Baselines are **simulation concepts**, not transport concerns.

### TickResult = canonical output of one authoritative server tick
`ServerEngine.TickOnce(ctx)` returns a `TickResult`:

- `Tick` – authoritative tick index
- `ServerTimeMs` – timing info for presentation / metrics only
- `StateHash` – hash of authoritative world **after** the tick
- `Ops` – ordered replication ops (`RepOpBatch`) for this tick

`TickResult` is the handoff object between server simulation and all downstream consumers:
- `ClientEngine` (in-process)
- network adapters (encode/decode + reliability)
- tests / replay / harness programs

**Recommendation:** keep `TickResult` minimal (delta only). Baselines remain explicit via `BaselineResult`.

---

## High-level control flow (verified against the code)

### Server control flow (proactive)
**ServerEngine** runs the simulation proactively at a fixed tick rate:

1. Commands are buffered by tick (`EngineCommandBuffer`)
2. `TickOnce()` executes a deterministic pipeline:
   - `CommandSystem.Execute(tick, world)`
     - dispatch buffered commands into sinks
     - execute sinks in **stable order**
   - `ServerModel.Advance(1)` fixed-step progression
   - systems emit `RepOp` via a transient replicator hook
   - `StateHash = ServerModelHash.Compute(world)`
3. `TickOnce()` returns `TickResult`

**Flow:**  
`ServerEngine → TickOnce → Commands → RepOps → ServerModel`

### Client control flow (reactive)
**ClientEngine** changes state only when it receives server output:

1. Apply a baseline (`BaselineResult`) once
2. Apply `TickResult` updates as they arrive
   - out-of-order ticks are buffered
   - ticks apply strictly in order
3. For each tick:
   - mutate `ClientModel` by applying `RepOp`
   - publish events for presentation

**Flow:**  
`ClientEngine → TickResult → ClientModel + Events → Presentation`

### Player control flow (intent-only)
1. Input produces `EngineCommand<EngineCommandType>`
2. Client assigns:
   - `requestedClientTick`
   - `clientCmdSeq`
3. Commands are enqueued on the server and scheduled centrally

**Flow:**  
`Commands → ServerEngine`

---

## Repository layout (ownership boundaries)

### `netlogic.core`

**Simulation core**
- `Sim/ServerEngine/*`
  - `ServerEngine`
  - `TickResult`
  - replication primitives (`RepOp`, `RepOpBatch`)
- `Sim/ClientEngine/*`
  - `ClientEngine`
  - `ClientModel`
  - event publication

**Command system**
- `Command/*`
  - `EngineCommandBuffer`
  - `CommandSystem`
  - command sinks (`ICommandSink<T>`)

**Networking adapters (optional)**
- `Sim/NetworkServer/*`
  - transport, reliability, baseline policy
  - sim contract → transport
- `Sim/NetworkClient/*`
  - transport endpoint
  - transport → sim contract

**Timing**
- `Sim/Timing/*`
  - realtime tick scheduling

### `netlogic.app`
- in-process harness
- deterministic tests and debugging

---

## Replication model

Replication is expressed as a stream of fixed-width `RepOp` records.

- **Must-apply ops**: entity lifecycle, flow transitions
- **Best-effort ops**: presentation snapshots (e.g. positions)

Current implementation filters by op type on the client.

**Recommended improvement:** make lanes explicit in the sim contract
(e.g. `ReplicationDelta { MustApply, BestEffort }`) so reliability policy
does not live inside `ClientEngine`.

---

## Determinism guardrails

- Stable execution order is explicit
- Commands are scheduled in tick space
- Replication ops are recorded during the tick and flushed in order
- Post-tick `StateHash` enables verification

---

## Suggested improvements

1. Keep sim contract types minimal and explicit
2. Separate must-apply vs best-effort replication at record time
3. Add hash lockstep determinism tests
4. Document how to add commands, systems, and RepOps
5. Harden pooled buffer lifetime rules
6. Enforce deterministic time usage (ticks only)

---

## TL;DR

- **ServerEngine** is authoritative.
- Players send **commands**, never state.
- Server outputs **TickResult**.
- **ClientEngine** consumes it reactively.
- Transport wraps the contract without leaking into simulation.
