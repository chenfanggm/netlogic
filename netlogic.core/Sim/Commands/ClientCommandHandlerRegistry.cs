using System;
using System.Collections.Generic;
using Game;

namespace Sim.Commands
{
    /// <summary>
    /// Deterministic command dispatcher.
    /// O(1) per command dispatch, no big switch/if chains.
    /// </summary>
    public sealed class ClientCommandHandlerRegistry
    {
        private readonly Dictionary<ClientCommandType, IClientCommandHandler> _handlers =
            new Dictionary<ClientCommandType, IClientCommandHandler>(64);

        public void Register(IClientCommandHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_handlers.ContainsKey(handler.Type))
                throw new InvalidOperationException($"Duplicate handler for {handler.Type}");

            _handlers.Add(handler.Type, handler);
        }

        public void RegisterMany(params IClientCommandHandler[] handlers)
        {
            for (int i = 0; i < handlers.Length; i++)
                Register(handlers[i]);
        }

        public void ApplyAll(World world, List<ClientCommand> commands)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                ClientCommand cmd = commands[i];

                if (_handlers.TryGetValue(cmd.Type, out IClientCommandHandler? handler) && handler != null)
                {
                    handler.Apply(world, in cmd);
                    continue;
                }
            }
        }
    }
}
