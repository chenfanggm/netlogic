using System;
using System.Collections.Generic;
using Game;
using Net;

namespace Sim
{
    /// <summary>
    /// Authoritative server simulation that processes commands, maintains world state, and publishes snapshots.
    /// </summary>
    public sealed class ServerSim
    {
        private readonly TickClock _clock;
        private readonly ITransportEndpoint _net;
        private readonly CommandBuffer _cmdBuffer;
        private readonly World _world;

        private int _tick;
        private int _nextPlayerId;

        private readonly Dictionary<int, SeqDedupe> _dedupeByPlayer;

        public ServerSim(TickClock clock, ITransportEndpoint net)
        {
            _clock = clock;
            _net = net;

            _cmdBuffer = new CommandBuffer();
            _world = new World();

            _tick = 0;
            _nextPlayerId = 1;

            _dedupeByPlayer = new Dictionary<int, SeqDedupe>(16);

            _world.Spawn(0, 0);
        }

        public void RunTicks(int tickCount)
        {
            for (int i = 0; i < tickCount; i++)
            {
                _clock.WaitForNextTick();
                PumpNetwork();
                Step();
                PublishSnapshot();
            }
        }

        private void PumpNetwork()
        {
            IMessage msg;
            while (_net.TryReceive(out msg))
            {
                switch (msg)
                {
                    case HelloMsg hello:
                        {
                            int pid = _nextPlayerId++;
                            _dedupeByPlayer[pid] = new SeqDedupe(windowKeep: 2048);

                            _net.Send(new WelcomeMsg(pid, _tick, _clock.TickRateHz));
                            Console.WriteLine("[Server] Welcome " + hello.PlayerName + " -> PlayerId=" + pid);
                            break;
                        }

                    case CommandBatchMsg batch:
                        {
                            SeqDedupe? dedupe;
                            if (!_dedupeByPlayer.TryGetValue(batch.PlayerId, out dedupe) || dedupe == null)
                            {
                                dedupe = new SeqDedupe(windowKeep: 2048);
                                _dedupeByPlayer[batch.PlayerId] = dedupe;
                            }

                            // Always ACK receipt (even if duplicate) so client can stop resending.
                            _net.Send(new AckMsg(batch.ClientSeq));

                            bool firstTime = dedupe.TryMarkFirstTime(batch.ClientSeq);
                            if (!firstTime)
                                break; // duplicate

                            List<Command> commands = batch.Commands;
                            for (int iCmd = 0; iCmd < commands.Count; iCmd++)
                            {
                                Command cmd = commands[iCmd];
                                if (cmd.TargetTick >= _tick)
                                    _cmdBuffer.Add(cmd);
                            }

                            break;
                        }
                }
            }
        }

        private void Step()
        {
            List<Command> cmds = _cmdBuffer.Drain(_tick);
            for (int i = 0; i < cmds.Count; i++)
            {
                Apply(cmds[i]);
            }

            _world.StepFixed();
            _tick++;
        }

        private void Apply(Command cmd)
        {
            switch (cmd.Type)
            {
                case CommandType.Move:
                    {
                        Game.Entity e;
                        if (_world.TryGet(cmd.A, out e))
                        {
                            int dx = (short)((cmd.B >> 16) & 0xFFFF);
                            int dy = (short)(cmd.B & 0xFFFF);
                            e.MoveBy(dx, dy);
                        }
                        break;
                    }

                case CommandType.Spawn:
                    {
                        _world.Spawn(cmd.A, cmd.B);
                        break;
                    }

                case CommandType.Damage:
                    {
                        Game.Entity e2;
                        if (_world.TryGet(cmd.A, out e2))
                            e2.Damage(cmd.B);
                        break;
                    }
            }
        }

        private void PublishSnapshot()
        {
            SnapshotMsg snap = new SnapshotMsg(_tick, _world.ToSnapshot());
            _net.Send(snap);
        }

        /// <summary>
        /// Sequence number deduplication tracker that prevents processing duplicate command batches.
        /// </summary>
        private sealed class SeqDedupe
        {
            private readonly int _windowKeep;
            private readonly HashSet<uint> _seen;
            private uint _maxSeen;

            public SeqDedupe(int windowKeep)
            {
                _windowKeep = windowKeep;
                _seen = new HashSet<uint>();
                _maxSeen = 0;
            }

            public bool TryMarkFirstTime(uint seq)
            {
                if (_seen.Contains(seq))
                    return false;

                _seen.Add(seq);

                if (seq > _maxSeen)
                {
                    _maxSeen = seq;
                    PruneOld();
                }

                return true;
            }

            private void PruneOld()
            {
                // Keep only the last _windowKeep sequence numbers (simple pruning)
                // Remove anything < (_maxSeen - _windowKeep)
                uint minKeep = 0;
                if (_maxSeen > (uint)_windowKeep)
                    minKeep = _maxSeen - (uint)_windowKeep;

                List<uint> toRemove = new List<uint>(64);

                foreach (uint s in _seen)
                {
                    if (s < minKeep)
                        toRemove.Add(s);
                }

                for (int i = 0; i < toRemove.Count; i++)
                {
                    _seen.Remove(toRemove[i]);
                }
            }
        }
    }
}
