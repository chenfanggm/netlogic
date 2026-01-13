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
        private uint _lastAckedSeq;

        // Tick timing
        private ClientTimeSync _timeSync;

        // Snapshots for rendering
        private readonly SnapshotRingBuffer _snapshots;
        private readonly RenderInterpolator _interpolator;

        // Practice knobs
        public int InputDelayTicks { get; set; } = 3;
        public int RenderDelayTicks { get; set; } = 3;

        public ClientSim(ITransportEndpoint net)
        {
            _net = net;
            TickRateHz = 20;
            _timeSync = new ClientTimeSync(TickRateHz);

            _snapshots = new SnapshotRingBuffer(capacity: 128);
            _interpolator = new RenderInterpolator();
        }

        public void Connect(string name)
        {
            _net.Send(new HelloMsg(name));
        }

        public void PumpNetwork()
        {
            IMessage msg;
            while (_net.TryReceive(out msg))
            {
                WelcomeMsg welcomeMsg;
                AckMsg ackMsg;
                SnapshotMsg snapshotMsg;

                if (msg is WelcomeMsg)
                {
                    welcomeMsg = (WelcomeMsg)msg;
                    PlayerId = welcomeMsg.PlayerId;
                    TickRateHz = welcomeMsg.TickRateHz;

                    _timeSync.SetTickRate(TickRateHz);
                    _timeSync.OnSnapshotReceived(welcomeMsg.ServerTick);

                    Console.WriteLine("[Client] Welcome: PlayerId=" + PlayerId + " ServerTick=" + welcomeMsg.ServerTick + " Rate=" + TickRateHz + "Hz");
                    continue;
                }

                if (msg is AckMsg)
                {
                    ackMsg = (AckMsg)msg;
                    if (ackMsg.AckClientSeq > _lastAckedSeq)
                        _lastAckedSeq = ackMsg.AckClientSeq;
                    continue;
                }

                if (msg is SnapshotMsg)
                {
                    snapshotMsg = (SnapshotMsg)msg;
                    _snapshots.Add(snapshotMsg);
                    _timeSync.OnSnapshotReceived(snapshotMsg.Tick);
                    continue;
                }
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


            if (!_snapshots.TryGetPairForTickDouble(renderTick, out SnapshotMsg a, out SnapshotMsg b, out double t))
            {
                // Fallback: return most recent snapshot if any
                int est = (int)Math.Floor(estServerTick);
                if (_snapshots.TryGet(est, out SnapshotMsg latest))
                    return latest.Entities;

                return []; // empty array
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

            CommandBatchMsg msg = new CommandBatchMsg(++_clientSeq, PlayerId, cmds);
            _net.Send(msg);
        }
    }
}
