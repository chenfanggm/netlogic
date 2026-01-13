using System;
using System.Collections.Generic;
using Net;

namespace Sim
{
    /// <summary>
    /// Client-side simulation that handles network communication, command sending, snapshot buffering, and rendering interpolation.
    /// </summary>
    public sealed class ClientSim
    {
        private readonly ITransportEndpoint _net;

        public int PlayerId { get; private set; }
        public int TickRateHz { get; private set; }

        private uint _clientSeq;

        private ClientTimeSync _timeSync;

        private readonly SnapshotRingBuffer _snapshots;
        private readonly RenderInterpolator _interpolator;

        private readonly PendingCommandBatches _pending;
        private readonly ClientAuthoritativeState _auth;

        public int InputDelayTicks { get; set; } = 3;
        public int RenderDelayTicks { get; set; } = 3;

        // Reliability knobs
        public long ResendIntervalMs { get; set; } = 120;
        public int MaxResendsPerPump { get; set; } = 8;

        public ClientSim(ITransportEndpoint net)
        {
            _net = net;

            TickRateHz = 20;
            _timeSync = new ClientTimeSync(TickRateHz);

            _snapshots = new SnapshotRingBuffer(capacity: 128);
            _interpolator = new RenderInterpolator();

            _pending = new PendingCommandBatches();
            _auth = new ClientAuthoritativeState();
        }

        public void Connect(string name)
        {
            _net.Send(new HelloMsg(name));
        }

        public void PumpNetworkAndResends()
        {
            PumpNetwork();
            ResendPendingIfNeeded();
        }

        private void PumpNetwork()
        {
            while (_net.TryReceive(out IMessage msg))
            {
                switch (msg)
                {
                    case WelcomeMsg welcomeMsg:
                        {
                            PlayerId = welcomeMsg.PlayerId;
                            TickRateHz = welcomeMsg.TickRateHz;

                            _timeSync.SetTickRate(TickRateHz);
                            _timeSync.OnSnapshotReceived(welcomeMsg.ServerTick);

                            Console.WriteLine("[Client] Welcome: PlayerId=" + PlayerId + " ServerTick=" + welcomeMsg.ServerTick + " Rate=" + TickRateHz + "Hz");
                            break;
                        }

                    case AckMsg ackMsg:
                        {
                            _pending.Ack(ackMsg.AckClientSeq);
                            break;
                        }

                    case SnapshotMsg snapshotMsg:
                        {
                            _auth.ApplyFullSnapshot(snapshotMsg);

                            SnapshotMsg rebuilt = new SnapshotMsg(snapshotMsg.Tick, _auth.ToEntityArrayUnordered());
                            _snapshots.Add(rebuilt);

                            _timeSync.OnSnapshotReceived(snapshotMsg.Tick);
                            break;
                        }

                    case DeltaMsg deltaMsg:
                        {
                            _auth.ApplyDelta(deltaMsg);

                            SnapshotMsg rebuilt = new SnapshotMsg(deltaMsg.Tick, _auth.ToEntityArrayUnordered());
                            _snapshots.Add(rebuilt);

                            _timeSync.OnSnapshotReceived(deltaMsg.Tick);
                            break;
                        }
                }
            }
        }

        private void ResendPendingIfNeeded()
        {
            List<CommandBatchMsg> resends = _pending.CollectResends(ResendIntervalMs, MaxResendsPerPump);
            for (int i = 0; i < resends.Count; i++)
            {
                CommandBatchMsg msg = resends[i];
                _net.Send(msg);
                _pending.MarkSent(msg.ClientSeq);
            }
        }

        public int GetEstimatedServerTickFloor()
        {
            return _timeSync.GetEstimatedServerTickFloor();
        }

        public EntityState[] GetRenderEntities()
        {
            double estServerTick = _timeSync.GetEstimatedServerTickDouble();
            double renderTick = estServerTick - RenderDelayTicks;

            SnapshotMsg a;
            SnapshotMsg b;
            double t;

            bool ok = _snapshots.TryGetPairForTickDouble(renderTick, out a, out b, out t);
            if (!ok)
            {
                int est = (int)Math.Floor(estServerTick);
                SnapshotMsg latest;
                if (_snapshots.TryGet(est, out latest))
                    return latest.Entities;

                return Array.Empty<EntityState>();
            }

            EntityState[] renderStates = _interpolator.Interpolate(a, b, t);
            return renderStates;
        }

        public void SendMoveCommand(int entityId, short dx, short dy)
        {
            if (PlayerId == 0)
                return;

            int estimatedServerTick = _timeSync.GetEstimatedServerTickFloor();
            int targetTick = estimatedServerTick + InputDelayTicks;

            int packed = (dx << 16) | (ushort)dy;

            List<Command> cmds = [new Command(PlayerId, targetTick, CommandType.Move, entityId, packed)];

            uint seq = ++_clientSeq;
            CommandBatchMsg msg = new CommandBatchMsg(seq, PlayerId, cmds);

            _pending.Add(msg);

            _net.Send(msg);
            _pending.MarkSent(seq);
        }
    }
}
