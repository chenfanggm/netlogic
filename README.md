# 🧠 Server-Authoritative Game Engine (Pure C# Core)

This repository implements a **server-authoritative, deterministic, fixed-tick, command-based** multiplayer architecture in **pure C#**, with Unity used only for **rendering/presentation**.

Core promise:

- **No game state mutation during network polling**
- **All simulation runs only inside `ServerEngine.TickOnce()`**
- Clients send **intent only** (commands), never state
- Engine is **transport-agnostic**
- Determinism preserved by construction (tick-driven, buffered commands, controlled IO)

> **ServerEngine is king.**  
> **Network is just a pipe.**  
> **Commands drive everything.**  
> **Ticks are sacred.**

---

## 🧱 High-Level Architecture (Current Ownership Boundaries)
### Server Control Flow
ServerEngine (proactive) -> TickOnce (generate TickResult, optional Baseline + replicated RepOps) -> Commands (consume, as intent, buffer per tick) -> RepOps (data action) -> ServerModel (mutate)
### Client Control Flow
ClientEngine (reactive) -> TickResult (consume) -> ClientModel (mutate) + Event (notify presentation layer) -> Presentation
### Player Control Flow
Commands (enqueue) -> ServerEngine (buffer commands)
                                                       
