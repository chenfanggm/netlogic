using com.aqua.netlogic.sim.game.rules;
using com.aqua.netlogic.sim.game.runtime;

namespace com.aqua.netlogic.sim.replication
{
    /// <summary>
    /// Authoritative state mutations only.
    /// Must be safe to apply on BOTH server and client for lockstep reconstruction.
    /// </summary>
    internal interface IAuthoritativeOpTarget : IRuntimeOpTarget
    {
        // Entities
        void ApplyEntitySpawned(int entityId, int x, int y, int hp);
        void ApplyEntityDestroyed(int entityId);
        void ApplyPositionSnapshot(int entityId, int x, int y);

        // Runtime-ish but entity scoped
        void ApplyEntityBuffSet(int entityId, BuffType buff, int remainingTicks);
        void ApplyEntityCooldownSet(int entityId, CooldownType cd, int remainingTicks);
    }
}
