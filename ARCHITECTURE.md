# ARCHITECTURE.md

## Overview

This repo implements a **server-authoritative, deterministic, fixed-tick, command-based** multiplayer architecture in **pure C#**, with Unity used only for **rendering/presentation**.

Core promise:

* **No game state mutation during network polling**
* **All simulation runs only inside `ServerEngine.TickOnce()`**
* Clients send **intent only** (commands), never state
* Engine is **transport-agnostic**
* Determinism preserved by construction (tick-driven, buffered commands, controlled IO)

Mental model:

> **ServerEngine is king.**
> **Network is just a pipe.**
> **Commands drive everything.**
> **Ticks are sacred.**

---

## Layering and dependencies

### Layers

| Layer                | Purpose                                             | Allowed to depend on                      |
| -------------------- | --------------------------------------------------- | ----------------------------------------- |
| Unity (presentation) | Input + render only                                 | NetworkClient API (presentation view models) |
| Client App           | Client networking + interpolation + command sending | Protocol + Transport                      |
| Server Adapter       | Network polling + decode/encode + feed engine       | Protocol + Transport + Engine public API  |
| Pure Engine          | Tick + deterministic simulation + outbound queue    | Game domain only (no Transport)           |
| Protocol             | Messages, serialization, lanes                      | (none)                                    |
| Transport            | UDP or in-process plumbing                          | (none)                                    |

### Non-negotiable rules

1. **Transport polling must not mutate game state.**
2. **Only `ServerEngine.TickOnce()` mutates `World`.**
3. **Clients never send state. Only `ClientCommand[]`.**
4. **Network layer converts wire messages → engine commands once.**
5. **Outbound packets are produced by engine and only transmitted by adapter.**

---

## Directory map (conceptual)

> Names below match the intent; actual filenames may vary slightly, but the ownership boundaries are strict.

### `netlogic.core/Sim` (application + engine glue)

* `ServerEngine` — pure authoritative simulation + tick
* `NetworkServer` — server network adapter
* `NetworkClient` — client network adapter + interpolation/presentation hooks
* `ClientCommandBuffer2` — scheduling + validation buffer
* `TickTicker` — fixed tick source
* `ServerReliableStream` — reliable op stream per client
* `ClientCommandCodec` — engine command <-> wire ops translation (client-side)
* `ClientOpsMsgToClientCommandConverter` — wire ops -> engine commands (server-side)
* `SnapshotRingBuffer`, `RenderInterpolator` — sample lane interpolation on client
* `RttEstimator`, `InputDelayController` — client delay tuning

### `netlogic.core/Net` (protocol + transports)

* `Protocol` / `MsgCodec` — message framing + serialize/deserialize
* Message types: `Hello`, `Welcome`, `Ping`, `Pong`, `Ack`, `Baseline`, `ClientOps`, `ServerOps`
* `Lane` — `Reliable` vs `Unreliable`
* `IClientTransport`, `IServerTransport`
* Transports:

  * `LiteNetLib` UDP
  * `InProcess` loopback for fast iteration/tests

### `Game/*` (domain state + deterministic logic)

* `World` and domain systems (entities, movement, containers, etc.)
* Must remain **pure**: no timing sources, no IO, no random without seeded RNG

---

## Subsystems deep dive

## 1) Tick System (`TickTicker`)

### Purpose

Provides a **single authoritative timeline** for simulation.

### Responsibilities

* Hold current tick
* Advance by exactly 1 per `TickOnce()` call (or per fixed step if batched)
* Provide tick rate metadata if needed

### Invariants

* Tick must be **monotonic**
* Tick increments only during simulation (`TickOnce()`), not during network polling
* All time-based gameplay derives from tick, not wall-clock

### Extension points

* Variable tick rate (not recommended; breaks determinism assumptions)
* Tick scaling for replay (fast-forward) by calling `TickOnce()` multiple times

---

## 2) Command Model (`ClientCommand`)

### Purpose

Defines **engine-level player intent**, independent of network protocol.

### Responsibilities

* Represent input as deterministic data
* Avoid embedding transport concerns (packet ids, byte arrays, etc.)

### Invariants

* Commands must be:

  * deterministic
  * serializable (via codec)
  * replayable
  * validated/scheduled using tick rules

### Extension points

Add new command types (recommended workflow):

1. Add `ClientCommandType`
2. Update client input creation
3. Update codec encode/decode
4. Update server conversion (ops->command)
5. Update engine application logic

---

## 3) Command Buffer (`ClientCommandBuffer2`)

### Purpose

Buffers commands in a deterministic structure indexed by:

* **scheduledTick**
* **connId**

This isolates network jitter from simulation and guarantees tick-stable command application.

### Responsibilities

* Accept batches `(connId, clientTick, cmdSeq, commands[])`
* Validate client tick vs server tick window
* Schedule commands into the correct server tick slot
* Provide “commands for tick T” during `TickOnce()`

### Validation rules (typical defaults)

* `MaxFutureTicks = 2`
* `MaxPastTicks = 2`
* Late commands → shift to current tick
* Too old → drop
* Too far future → clamp or drop

### Invariants

* The command buffer must be **purely data**:

  * it cannot mutate World
  * it cannot depend on transport
* Same input stream results in same scheduled batches → determinism

### Extension points

* Add per-connection dedup: `LastClientCmdSeq` to reject duplicates
* Add per-command validation (anti-cheat): move speed checks, etc.
* Add command merging/coalescing (e.g., continuous input accumulation)

---

## 4) Authoritative Engine (`ServerEngine`)

### Purpose

The authoritative simulation core.

### Responsibilities

* Own `World`
* Own `TickTicker`
* Consume commands (from buffer) and apply to World
* Produce outbound messages as **data** (never call transport)
* Manage per-client reliable streams + baseline cadence

### Public input surface (conceptual)

* `OnClientConnected(connId)`
* `OnClientHello(connId, ...)`
* `OnClientAck(connId, ...)`
* `OnClientPing(connId, ...)`
* `EnqueueClientCommands(connId, clientTick, cmdSeq, commands[])`
* `TickOnce()`
* `TryDequeueOutbound(out OutboundPacket pkt)`

### What happens in `TickOnce()`

At tick `T`:

1. Advance tick (`TickTicker`)
2. Dequeue command batches scheduled for `T`
3. Apply commands deterministically to World
4. Emit discrete ops into per-client `ServerReliableStream` as needed
5. Emit continuous/sample updates (latest wins) for interpolation
6. Emit periodic baseline snapshots (every N ticks)
7. Queue outbound packets (reliable + sample) for the adapter to send

### Invariants

* **Only place where World is mutated**
* No transport references
* Outbound is queued, not directly sent
* Deterministic outcomes given same:

  * initial baseline
  * command stream
  * tick count

### Extension points

* Client-side prediction: run `ServerEngine` locally in parallel on client
* Replay: feed recorded command stream into engine and compare hashes
* Bots: implement bot decision → `ClientCommand[]` injection
* Interest management: decide per-client visibility before queuing outbound

---

## 5) Reliable Ops Stream (`ServerReliableStream`)

### Purpose

Reliable lane is for **discrete state changes** that must be:

* ordered
* acknowledged
* replayable

Examples:

* container ops (move card, discard item)
* entity spawn/despawn
* authoritative events

### Responsibilities

* Maintain per-client reliable sequence
* Coalesce multiple ops into packets
* Track last-acked sequence
* Replay unacked ops when needed (reconnect/packet loss)

### Invariants

* Client must ack reliable sequence
* Reliable op application on client should be idempotent when possible

### Extension points

* Compact op encoding / delta compress
* Partial reliable stream for interest-managed subsets

---

## 6) Unreliable Lane + Interpolation (Client)

### Purpose

The sample lane provides **continuous state** updates (position, HP, etc.) where:

* newest update supersedes older ones
* client interpolates for smooth rendering

### Responsibilities (client side)

* Maintain a `SnapshotRingBuffer` of recent samples
* Render time is a delayed cursor behind latest sample tick
* Interpolate between two snapshots around render tick
* Snap only when error exceeds threshold

### Invariants

* Unreliable lane must not block gameplay (it’s “nice to have”)
* Render is allowed to be nondeterministic (visual only)
* Simulation remains authoritative on server

### Extension points

* Dead reckoning / extrapolation for brief loss
* Per-entity smoothing curves
* Priority sampling (nearby entities more frequent)

---

## Client core (`ClientEngine`)

`ClientEngine` is **pure state reconstruction** for rendering/UI.

Important boundary:

- `ClientEngine` **does not** decode wire bytes.
- It consumes only **client-facing replication primitives**:
  - `GameSnapshot` (baseline snapshot)
  - `ReplicationUpdate` (envelope containing `RepOp[]`, plus tick/hash/seq)

Wire decoding lives in `ClientMessageDecoder`.

---

## 7) Client Networking (`NetworkClient`)

### Purpose

Client-side adapter between transport/protocol and presentation.

### Responsibilities

* Handshake lifecycle (Hello/Welcome)
* Ping/Pong for RTT estimation
* Command sending (Reliable)
* Reliable lane application (snapshot + discrete ops)
* Unreliable lane buffering for interpolation

### Outgoing path

`ClientCommand` → `ClientCommandCodec` → `ClientOpsMsg` → `IClientTransport.Send(Reliable)`

### Incoming path

* Reliable: apply immediately, ack sequences
* Unreliable: push into interpolation buffer

### Invariants

* Client never “decides” game truth
* Client should tolerate reorder/loss on sample lane
* Client must not mutate server-owned state outside of applying server ops

### Extension points

* Input prediction layer (local engine mirror)
* Reconciliation: compare predicted vs authoritative, rewind/replay
* Input delay tuning per RTT (already scaffolded)

---

## 8) Server Adapter (`NetworkServer`)

### Purpose

A thin adapter that connects transport to engine.

### Responsibilities

* Poll transport
* Decode packets into protocol messages
* Convert ClientOps → ClientCommand[] exactly once
* Feed engine entrypoints (`OnClientHello`, `EnqueueClientCommands`, etc.)
* Dequeue outbound from engine and send via transport

### Critical invariant

**NetworkServer must never mutate World and must never run simulation.**
It can call `ServerEngine.TickOnce()` when hosting, but it must not apply gameplay itself.

### Extension points

* ServerHost loop for fixed tick scheduling
* Rate limiting / spam control before enqueue
* Metrics instrumentation (per-lane bytes, queue sizes)

---

## 9) Protocol (`MsgCodec`, message types, lanes)

### Purpose

Define wire contract:

* message framing
* lane semantics
* payload encoding/decoding

### Typical message set

* `Hello` / `Welcome`
* `Ping` / `Pong`
* `ClientAck`
* `Baseline`
* `ClientOps` (commands)
* `ServerOps` (reliable/sample ops)

### Invariants

* Protocol must be versionable
* Message decoding must be safe against malformed inputs
* Wire format should remain independent from engine model types

### Extension points

* Protocol version negotiation
* Compression
* Interest-managed op subsets
* Security: signature or session tokens

---

## Protocol boundary: why messages are decoded into RepOps before ClientEngine

The wire protocol (`BaselineMsg`, `ServerOpsMsg`) exists to support:
- schema/versioning
- hash contract guards
- packet lanes (reliable/unreliable)
- payload framing `[opType][opLen][payload]`

However, the **client simulation core** should not depend on wire details.

Therefore:

- `NetworkClient` receives messages via transport
- `ClientMessageDecoder` validates + decodes:
  - `BaselineMsg` → `GameSnapshot`
  - `ServerOpsMsg` → `ReplicationUpdate(RepOp[])`
- `ClientEngine` consumes only `GameSnapshot` + `ReplicationUpdate`

This keeps `ClientEngine` reusable for:
- replays (feed updates directly)
- client prediction (same input type)
- deterministic tests without any network codec

---

## Folder layout and naming rules (refactor checklist)

### Recommended folder layout

```
netlogic.core/
  Net/                       <-- pure wire protocol: message structs + byte codecs
    Messages/
      BaselineMsg.cs
      ServerOpsMsg.cs
      ClientOpsMsg.cs
      WelcomeMsg.cs
      PingMsg.cs
      PongMsg.cs
      ClientAckMsg.cs
    Codec/
      MsgCodec.cs            <-- bytes <-> message structs (framing, envelopes)
      OpsWriter.cs           <-- writes op payload bytes into writer
      OpsReader.cs           <-- reads op payload bytes from reader
    WireState/
      WireEntityState.cs
      WireFlowState.cs
    Transport/
      IClientTransport.cs
      IServerTransport.cs
      LiteNetLib/
        ...

  Sim/                       <-- simulation + replication (transport-agnostic)
    Game/
      Game.cs
      Entity/
      Snapshot/
        GameSnapshot.cs
        SampleEntityPos.cs
      Flow/
        FlowSnapshot.cs
        ...
    ServerEngine/
      ServerEngine.cs
      TickFrame.cs
      CommandBuffer.cs
      ...

    Replication/             <-- shared replication primitives (client + server)
      RepOp.cs
      RepOpType.cs
      ReplicationUpdate.cs
      StateHash.cs
      HashContract.cs

    NetworkServer/           <-- server endpoint; owns transport + reliability streams
      NetworkServer.cs
      ReliableStream/
        ServerReliableStream.cs
      Protocol/
        ServerMessageEncoder.cs   <-- ServerEngine outputs -> BaselineMsg/ServerOpsMsg

    NetworkClient/           <-- client endpoint; owns transport + acks
      NetworkClient.cs
      Protocol/
        ClientMessageDecoder.cs   <-- BaselineMsg/ServerOpsMsg -> snapshot + RepUpdate

    ClientEngine/            <-- client core; consumes snapshot + RepUpdate only
      ClientEngine.cs
      ClientModel.cs
```

### Naming rules

1) Codec = converts between bytes and a structured message type.
   - Pure wire framing layer.
   - Examples:
     - MsgCodec: bytes <-> BaselineMsg / ServerOpsMsg / etc
     - ClientCommandCodec: ClientCommand <-> ops payload bytes (if needed)

2) Encoder = converts from higher-level domain data -> message struct or payload bytes.
   - Does not do transport.
   - Does not do reliability/ordering.
   - Examples:
     - ServerMessageEncoder: (GameSnapshot + RepOp[]) -> BaselineMsg/ServerOpsMsg payload

3) Decoder = converts from message struct or payload bytes -> higher-level domain data.
   - Validates protocol/schema/hash contract.
   - Examples:
     - ClientMessageDecoder: BaselineMsg -> GameSnapshot, ServerOpsMsg -> ReplicationUpdate

4) Transport classes should not parse op payload bytes.
   - NetworkClient/NetworkServer may call MsgCodec, but op payload parsing belongs in Decoder/Encoder.

5) Engine cores should never touch NetDataReader/Writer.
   - ServerEngine produces RepOps, ClientEngine consumes RepOps, no wire details.

6) Reliable sequencing belongs to the endpoint (NetworkServer/NetworkClient) or stream module.
   - Encoder/Decoder may read or forward seq fields but should not own retransmit windows.

### Optional refinement

- Move RepOp out of Sim/ServerEngine into Sim/Replication so it is clearly shared
  by both server and client layers.

---

## 10) Transport (LiteNetLib / InProcess)

### Purpose

Provide a unified interface:

* send/receive packets
* connection lifecycle
* lanes

### Requirements

* Transport contains **no game knowledge**
* Must not introduce nondeterminism into simulation (it only affects arrival time)

### Invariants

* InProcess transport should mirror UDP behavior enough to test logic
* Adapter must treat transports identically

### Extension points

* WebSocket transport for debugging
* Relay/NAT punch-through improvements
* Packet simulation for tests (loss/jitter)

---

## Runtime flows

## A) Server flow (Poll vs Tick boundary)

**Poll phase (no simulation):**

1. `NetworkServer.Poll()`
2. transport events + packet receives
3. decode → convert → `ServerEngine.EnqueueClientCommands()`
4. drain `ServerEngine.TryDequeueOutbound()` and send any queued packets

**Tick phase (simulation only):**

1. `ServerEngine.TickOnce()`
2. apply commands scheduled for current tick
3. emit reliable + sample + baseline outbound packets
4. adapter sends them after tick

---

## B) Client flow (commands out, state in)

1. Unity input produces `ClientCommand`
2. `NetworkClient` applies delay strategy and sends `ClientOps` on Reliable lane
3. Client polls transport:

   * Reliable: apply baseline + discrete ops, send ack
   * Unreliable: push snapshots into buffer
4. Unity render reads interpolated states from client

---

## Determinism policy

Simulation determinism requires:

* fixed tick
* deterministic world mutation order
* stable command ordering per tick
* no dependency on wall-clock
* controlled randomness (seeded RNG if used)

**Rendering does not need to be deterministic**.

Recommended future: add periodic `WorldHash` computation and compare server vs replay runs.

---

## Testing strategy (recommended)

### 1) Pure engine tests

* Create `ServerEngine` + `World`
* Feed a deterministic command script
* Run `TickOnce()` for N ticks
* Assert:

  * key state fields
  * optional world hash
  * outbound messages count/shape

### 2) InProcess end-to-end tests

* Connect `NetworkClient` ↔ `NetworkServer` using InProcess transport
* Send commands under simulated jitter/loss
* Assert:

  * client sees correct baseline
  * reliable ops applied exactly once
  * sample interpolation buffer behaves

### 3) Regression replay tests

* Record command stream + baseline seed
* Re-run engine and assert identical world hash at checkpoints

---

## Extension roadmap (how to add big features without breaking layering)

### Client-side prediction

* Run `ServerEngine` locally on client as `PredictedEngine`
* Send commands normally
* When authoritative ops arrive:

  * reconcile (rewind to last confirmed tick, replay commands)

### Interest management

* In `ServerEngine`, decide per-conn what entities/ops to send
* Keep reliable stream per-client
* Unreliable lane can be throttled per interest radius

### Bandwidth reduction

* Unreliable: quantize + delta + frequency control
* Reliable: op packing + dictionary ids + compression

### Anti-cheat basics

* Validate commands on server:

  * movement bounds
  * cooldown gating
  * server-side “truth checks”
* Keep rules deterministic and tick-based

---

## Design checklist (use this for code review)

Before merging changes, verify:

* [ ] No gameplay state mutation occurs during network polling
* [ ] World is mutated only in `ServerEngine.TickOnce()`
* [ ] New inputs are represented as `ClientCommand` (not state)
* [ ] Wire format changes stay inside protocol/codec layer
* [ ] Transport does not reference game types
* [ ] Unreliable lane is treated as latest-wins (not authoritative)
* [ ] Reliable ops are acked and replayable
* [ ] Feature adds an extension point rather than cross-layer coupling

---

## Glossary

* **Tick**: fixed-step simulation index (authoritative on server)
* **ConnId**: server-side connection identifier
* **Reliable lane**: ordered, acked, replayable ops
* **Unreliable lane**: newest-wins snapshots for smooth rendering
* **Baseline**: authoritative reset snapshot for join/reconnect/recovery
* **Command**: engine-level player intent (MoveBy, CastSpell, etc.)
* **Op**: server-to-client action describing a state transition or snapshot