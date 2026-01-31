You’re right — the only acceptable answer here is one that matches what’s actually in your latest netlogic.zip.

I unpacked and read through the project structure and the key control-flow files (ServerEngine/ClientEngine, CommandSystem/Buffer/Sinks/Handlers, RulesReducer, RepOp/OpWriter/Applier, LocalClientEngineProgram, NetworkServer/NetworkClient, codecs). Below is a file-based control-flow diagram set that matches the current full-ops architecture.

All diagrams are copy/paste Mermaid.

⸻

Diagram A — Whole system at a glance (in-process harness + networked mode)

flowchart TD
%% =========================
%% In-process harness (no net)
%% =========================
subgraph HARNESS["In-Process Harness (no transport / no codec)"]
LCEP["netlogic.core/Program/LocalClientEngineProgram/LocalClientEngineProgram.cs\nRun()"] --> TICKRUN["netlogic.core/Sim/Timing/ServerTickRunner.cs\nRun(onTick)"]
LCEP --> DI["netlogic.core/Program/... (DI container)\nMessagePipe + handlers"]

    LCEP --> BASE["netlogic.core/Sim/ServerEngine/ServerEngine.cs\nBuildSnapshot()"]
    BASE --> BASEMSG["netlogic.core/Sim/ClientEngine/BaselineResult.cs\nBaselineResult(snapshot)"]
    BASEMSG --> C_APPLY_BASE["netlogic.core/Sim/ClientEngine/ClientEngine.cs\nApply(in BaselineResult)"]

    TICKRUN --> S_TICK["netlogic.core/Sim/ServerEngine/ServerEngine.cs\nTickOnce(ctx)"]
    S_TICK --> TR["netlogic.core/Sim/ServerEngine/TickResult.cs\nTickResult(tick,time,hash,ops,snapshot:null)"]
    TR --> C_APPLY_TICK["netlogic.core/Sim/ClientEngine/ClientEngine.cs\nApply(in TickResult)"]

    %% scripted player intent
    C_APPLY_TICK --> FLOWSTATE["netlogic.core/Sim/ClientEngine/ClientModel.cs\nModel.Flow (view snapshot)\n+ Model.FlowState (authoritative)"]
    FLOWSTATE --> SCRIPT["netlogic.core/Program/FlowScript/PlayerFlowScript.cs\nStep() publishes CommandEvent"]
    SCRIPT --> BUS_CMD["netlogic.core/Sim/ClientEngine/Events/CommandEvent.cs\n+ netlogic.core/EventBus/IEventBus.Publish"]
    BUS_CMD --> CMD_HANDLER["netlogic.core/Sim/ClientEngine/Events/CommandEventHandler.cs\nHandle(CommandEvent)"]
    CMD_HANDLER --> DISPATCH["netlogic.core/Sim/ClientEngine/Command/ClientCommandToServerEngineDispatcher.cs\nDispatch -> ServerEngine.EnqueueCommands(...)"]
    DISPATCH --> S_ENQ["netlogic.core/Sim/ServerEngine/ServerEngine.cs\nEnqueueCommands(...)"]
    S_ENQ --> CMD_BUF["netlogic.core/Command/Buffer/EngineCommandBuffer.cs\nstore per tick/connId\nvalidate/clamp/snap/drop"]

end

%% =========================
%% Networked mode (transport + codec)
%% =========================
subgraph NET["Networked Mode (transport + codec adapters)"]
NS["netlogic.core/Sim/NetworkServer/NetworkServer.cs\nPoll() + TickOnce(ctx)"] --> ST["netlogic.core/Sim/ServerEngine/ServerEngine.cs\nTickOnce(ctx)"]
ST --> NS_PART["netlogic.core/Sim/ServerEngine/Replication/RepOpPartitioner.cs\nPartition(ops) => reliable/unreliable"]
NS_PART --> NS_REL["netlogic.core/Sim/NetworkServer/Reliability/ServerReliableStream.cs\nack/replay window + seq"]
NS_REL --> NS_ENC["netlogic.core/Sim/NetworkServer/ServerMessageEncoder.cs\nencode Baseline/ServerOps"]
NS_ENC --> NS_SEND["netlogic.core/Net/Transport/IServerTransport.cs\nSend(NetPacket lane/payload)"]

    NC["netlogic.core/Sim/NetworkClient/NetworkClient.cs\nPoll()"] --> NC_RECV["netlogic.core/Net/Transport/IClientTransport.cs\nTryReceive(NetPacket)"]
    NC_RECV --> MSGDEC["netlogic.core/Sim/ClientEngine/Protocol/ClientMessageDecoder.cs\nDecodeBaselineToResult\nDecodeServerOpsToUpdate"]
    MSGDEC --> CENG["netlogic.core/Sim/ClientEngine/ClientEngine.cs\nApply(BaselineResult)\nApplyReplicationUpdate(update)"]
    CENG --> ACK["netlogic.core/Sim/NetworkClient/NetworkClient.cs\nSend ClientAckMsg(reliable seq)"]

end

⸻

Diagram B — Server tick (full-ops authoritative core)

This reflects your current ServerEngine.TickOnce exactly:

flowchart TD
SE["netlogic.core/Sim/ServerEngine/ServerEngine.cs\nTickOnce(ctx)"] --> T["tick = ++_currentTick\nServerTimeMs = ctx.NowMs"]
SE --> REC0["netlogic.core/Sim/Replication/ReplicationRecorder.cs\nBeginTick(tick)"]
SE --> OPSW["netlogic.core/Sim/Replication/OpWriter.cs\nnew OpWriter(world, recorder)"]

%% 1) intent -> ops
SE --> CS["netlogic.core/Command/CommandSystem.cs\nExecute(tick, world, ops)"]
CS --> DISP["netlogic.core/Command/CommandSystem.cs\nDispatch(tick) -> sink.InboxCommand(...)"]
DISP --> BUF["netlogic.core/Command/Buffer/EngineCommandBuffer.cs\nTryDequeueForTick(tick,connId)"]
CS --> SINKS["netlogic.core/Command/Sink/CommandSinkBase.cs\nExecute(world, ops)\n(stable order)"]

%% handlers (examples)
SINKS --> REG["netlogic.core/Command/Sink/EngineCommandHandlerRegistry.cs\nsystem -> handlers[]"]
REG --> H_FLOW["netlogic.core/Sim/Systems/GameFlowSystem/Handlers/FlowFireHandler.cs\nFlowIntentHandler.Handle(world,ops,cmd)"]
REG --> H_MOVE["netlogic.core/Sim/Systems/MovementSystem/Handlers/MoveByHandler.cs\nHandle(world,ops,cmd)"]
H_FLOW --> FLOWRED["netlogic.core/Sim/Game/Flow/FlowReducer.cs\nApplyPlayerIntent(world, ops, intent,param)"]
H_MOVE --> EMAPPLY1["netlogic.core/Sim/Replication/OpWriter.cs\nEmitAndApply(RepOp.PositionSnapshot(...))"]
FLOWRED --> EMAPPLY2["netlogic.core/Sim/Replication/OpWriter.cs\nEmitAndApply(RepOp.* runtime/flow/round/run ops)"]

%% 2) time rules -> ops
SE --> RULES["netlogic.core/Sim/Game/Rules/RulesReducer.cs\nApplyTick(tick, world, ops)"]
RULES --> CD["CooldownRules.ApplyTick -> EmitAndApply(EntityCooldownSet)"]
RULES --> BF["BuffRules.ApplyTick -> EmitAndApply(EntityBuffSet)"]

%% 3) finalize
SE --> REC1["netlogic.core/Sim/Replication/ReplicationRecorder.cs\nEndTickAndFlush() -> RepOpBatch"]
SE --> HASH["netlogic.core/Sim/Game/ServerModelHash.cs\nCompute(world) -> StateHash"]
REC1 --> OUT["netlogic.core/Sim/ServerEngine/TickResult.cs\nnew TickResult(tick,time,hash,ops,snapshot:null)"]
HASH --> OUT

Important reality captured here:
• All authoritative mutation happens through OpWriter.EmitAndApply → RepOpApplier.ApplyAuthoritative (server target).
• RulesReducer is the replacement for \_game.Advance(1) (no direct mutation allowed).
• FlowSnapshot is used only for baselines/debug snapshots (not RepOps).

⸻

Diagram C — RepOp interpretation (single source of truth)

flowchart TD
OP["netlogic.core/Sim/Replication/RepOp.cs\nsemantic fields (EntityId,X,Y,Hp,...)"] --> APPLY["netlogic.core/Sim/Replication/RepOpApplier.cs\nApplyAuthoritative"]

subgraph SERVER_SIDE["Server side (authoritative state)"]
OW["netlogic.core/Sim/Replication/OpWriter.cs\nEmitAndApply(op)"] --> REC["netlogic.core/Sim/Replication/ReplicationRecorder.cs\nRecord(op)"]
OW --> APPA["netlogic.core/Sim/Replication/RepOpApplier.cs\nApplyAuthoritative(ServerModel, op)"]
APPA --> SM["netlogic.core/Sim/Game/ServerModel.cs\nIAuthoritativeOpTarget\n+ runtime targets"]
end

subgraph CLIENT_SIDE["Client side (reconstruction + presentation)"]
CE["netlogic.core/Sim/ClientEngine/ClientEngine.cs\nApplyOps(serverTick,hash,ops)"] --> APPA2["netlogic.core/Sim/Replication/RepOpApplier.cs\nApplyAuthoritative(ClientModel, op)"]
APPA2 --> CM["netlogic.core/Sim/ClientEngine/ClientModel.cs\nIAuthoritativeOpTarget\n(IRuntimeOpTarget fields)"]
end

⸻

Diagram D — Client apply (baseline + ordered ticks + event emission)

flowchart TD
CE["netlogic.core/Sim/ClientEngine/ClientEngine.cs"] --> BASE["Apply(in BaselineResult)\nApplyBaselineSnapshot(snapshot)"]
BASE --> RESET["netlogic.core/Sim/ClientEngine/ClientModel.cs\nResetFromSnapshot(snapshot)\n_hasBaseline=true\nFlowState default -> Boot"]

CE --> APPLYT["Apply(in TickResult)"]
APPLYT --> BUFCHK{"\_hasBaseline?"}
BUFCHK -->|No| PEND["buffer TickResult.WithOwnedOps()\nSortedDictionary pending"]
BUFCHK -->|Yes| ORD{"tick == lastApplied+1?"}
ORD -->|No| PEND2["buffer result.WithOwnedOps()\nFlushPendingTicks()"]
ORD -->|Yes| APPLYOPS["ApplyReplicationResult(result)\nApplyOps(tick,hash,ops)"]

APPLYOPS --> LOOP["for each RepOp:\nApplyAuthoritative(Model,op)"]
LOOP --> UPD["Model.LastServerTick/LastStateHash"]
UPD --> EVT{"FlowState changed?"}
EVT -->|Yes| PUBLISH["netlogic.core/EventBus/IEventBus.Publish\nGameFlowStateTransitionEvent"]

⸻

Diagram E — NetworkServer tick (codec + reliability, engine stays “typed”)

flowchart TD
NS["netlogic.core/Sim/NetworkServer/NetworkServer.cs\nTickOnce(ctx)"] --> ENG["netlogic.core/Sim/ServerEngine/ServerEngine.cs\nTickOnce(ctx) -> TickResult"]
ENG --> PART["netlogic.core/Sim/ServerEngine/Replication/RepOpPartitioner.cs\nPartition(ops) => reliable/unreliable"]

%% reliable lane
PART --> REL["Reliable ops span"]
REL --> STREAM["netlogic.core/Sim/NetworkServer/Reliability/ServerReliableStream.cs\nQueue ops -> packets\nseq + replay window"]
STREAM --> ENCODE_REL["netlogic.core/Sim/NetworkServer/ServerMessageEncoder.cs\nEncode ServerOpsMsg (Reliable)"]
ENCODE_REL --> SEND_REL["netlogic.core/Net/Transport/IServerTransport.cs\nSend(Lane.Reliable)"]

%% unreliable lane
PART --> UNREL["Unreliable ops span (latest-wins)"]
UNREL --> ENCODE_UNREL["netlogic.core/Sim/NetworkServer/ServerMessageEncoder.cs\nEncode ServerOpsMsg (Unreliable)"]
ENCODE_UNREL --> SEND_UNREL["netlogic.core/Net/Transport/IServerTransport.cs\nSend(Lane.Unreliable)"]

%% baseline
NS --> BASEPOL["Baseline policy\n(netlogic.core/Sim/NetworkServer/BaselineBuilder.cs)"]
BASEPOL --> ENCODE_BASE["netlogic.core/Sim/NetworkServer/ServerMessageEncoder.cs\nEncode BaselineMsg"]
ENCODE_BASE --> SEND_BASE["IServerTransport.Send(Lane.Reliable)"]

⸻

Diagram F — NetworkClient receive/apply (codec to typed, then ClientEngine)

flowchart TD
NC["netlogic.core/Sim/NetworkClient/NetworkClient.cs\nPoll()"] --> RECV["netlogic.core/Net/Transport/IClientTransport.cs\nTryReceive(NetPacket)"]

RECV --> DECBASE["netlogic.core/Sim/ClientEngine/Protocol/ClientMessageDecoder.cs\nDecodeBaselineToResult"]
DECBASE --> APPLYBASE["netlogic.core/Sim/ClientEngine/ClientEngine.cs\nApply(BaselineResult)"]

RECV --> DECREL["ClientMessageDecoder.cs\nDecodeServerOpsToUpdate(isReliable=true)"]
DECREL --> APPLYREL["ClientEngine.ApplyReplicationUpdate(update)"]

RECV --> DECUNREL["ClientMessageDecoder.cs\nDecodeServerOpsToUpdate(isReliable=false)"]
DECUNREL --> APPLYUNREL["ClientEngine.ApplyReplicationUpdate(update)"]

APPLYREL --> ACK["NetworkClient.cs\nSend ClientAckMsg(lastAckedReliableSeq)\nMsgCodec.EncodeClientAck"]
ACK --> SENDACK["IClientTransport.Send(Lane.Reliable)"]

⸻

Quick sanity checklist (so you can spot diagram drift)

This diagram set matches your current code if all these are true (they are, in this zip):
• ServerEngine.TickOnce uses OpWriter and calls RulesReducer.ApplyTick ✅
• Mutation on server happens via OpWriter.EmitAndApply + RepOpApplier.ApplyAuthoritative(ServerModel, op) ✅
• Client applies ops via RepOpApplier.ApplyAuthoritative(ClientModel, op) AND ApplyView(ClientModel, op) ✅
• Baseline is explicit (BuildSnapshot → BaselineResult) in LocalClientEngineProgram ✅
• Network layer does not leak Packet/Frame into Engine: it translates typed ↔ bytes at edges ✅

⸻

If you want, next I can generate a “Current vs Target” diff diagram where:
• current: FlowManager is still referenced in FlowReducer (“transitional bridge”)
• target: FlowState is stored directly and FlowManager is removed

That diagram usually makes the remaining refactor steps obvious in 30 seconds.
