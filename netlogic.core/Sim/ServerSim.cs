using Game;
using Net;

namespace Sim
{
    public sealed class ServerSim
    {
        private readonly TickClock _clock;
        private readonly ITransportEndpoint _net;
        private readonly CommandBuffer _cmdBuffer = new();
        private readonly World _world = new();

        private int _tick;
        private int _nextPlayerId = 1;

        // Minimal reliability: ack last received seq per player
        private readonly Dictionary<int, uint> _lastClientSeq = new();

        public ServerSim(TickClock clock, ITransportEndpoint net)
        {
            _clock = clock;
            _net = net;

            // Example: spawn one entity at start
            _world.Spawn(0, 0);
        }

        public void RunTicks(int tickCount)
        {
            for (int i = 0; i < tickCount; i++)
            {
                _clock.WaitForNextTick();
                PumpNetwork();
                Step();
                PublishSnapshot(); // Step 1: full snapshots
            }
        }

        private void PumpNetwork()
        {
            while (_net.TryReceive(out IMessage msg))
            {
                switch (msg)
                {
                    case HelloMsg hello:
                        {
                            int pid = _nextPlayerId++;
                            _lastClientSeq[pid] = 0;
                            _net.Send(new WelcomeMsg(pid, _tick, _clock.TickRateHz));
                            Console.WriteLine($"[Server] Welcome {hello.PlayerName} -> PlayerId={pid}");
                            break;
                        }
                    case CommandBatchMsg batch:
                        {
                            // basic ordering (not full reliability yet)
                            if (_lastClientSeq.TryGetValue(batch.PlayerId, out uint last))
                            {
                                if (batch.ClientSeq <= last)
                                    break; // old/duplicate
                                _lastClientSeq[batch.PlayerId] = batch.ClientSeq;
                            }
                            else
                            {
                                _lastClientSeq[batch.PlayerId] = batch.ClientSeq;
                            }

                            // Ack immediately
                            _net.Send(new AckMsg(batch.ClientSeq));

                            foreach (Command cmd in batch.Commands)
                            {
                                // Server authoritative: accept only future/present ticks
                                if (cmd.TargetTick >= _tick)
                                    _cmdBuffer.Add(cmd);
                                // else late: drop (log later in step 3+)
                            }
                            break;
                        }
                }
            }
        }

        private void Step()
        {
            // 1) Apply commands scheduled for this tick
            List<Command> cmds = _cmdBuffer.Drain(_tick);
            for (int i = 0; i < cmds.Count; i++)
                Apply(cmds[i]);

            // 2) Advance world
            _world.StepFixed();

            _tick++;
        }

        private void Apply(Command cmd)
        {
            switch (cmd.Type)
            {
                case CommandType.Move:
                    {
                        // A=entityId, B=packed dx/dy: high16=dx low16=dy (signed)
                        if (_world.TryGet(cmd.A, out Entity e))
                            e.MoveBy((short)((cmd.B >> 16) & 0xFFFF), (short)(cmd.B & 0xFFFF));
                        break;
                    }
                case CommandType.Spawn:
                    {
                        // A=x, B=y
                        _world.Spawn(cmd.A, cmd.B);
                        break;
                    }
                case CommandType.Damage:
                    {
                        // A=entityId, B=damage
                        if (_world.TryGet(cmd.A, out Entity e))
                            e.Damage(cmd.B);
                        break;
                    }
            }
        }

        private void PublishSnapshot()
        {
            SnapshotMsg snap = new(_tick, _world.ToSnapshot());
            _net.Send(snap);
        }
    }
}
