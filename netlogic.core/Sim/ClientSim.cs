using System;
using System.Collections.Generic;
using Net;

namespace Sim
{
    public sealed class ClientSim
    {
        private readonly ITransportEndpoint _net;

        public int PlayerId { get; private set; }
        public int TickRateHz { get; private set; }

        private uint _clientSeq;

        private ClientTimeSync _timeSync;

        private readonly SnapshotRingBuffer _snapshots;
        private readonly RenderInterpolator _interpolator;

        private readonly PendingBatches _pending;

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

            _pending = new PendingBatches();
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
            IMessage msg;
            while (_net.TryReceive(out msg))
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
                            _snapshots.Add(snapshotMsg);
                            _timeSync.OnSnapshotReceived(snapshotMsg.Tick);
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

            List<Command> cmds = new List<Command>(1);
            cmds.Add(new Command(PlayerId, targetTick, CommandType.Move, entityId, packed));

            uint seq = ++_clientSeq;
            CommandBatchMsg msg = new CommandBatchMsg(seq, PlayerId, cmds);

            _pending.Add(msg);

            _net.Send(msg);
            _pending.MarkSent(seq);
        }
    }
}
