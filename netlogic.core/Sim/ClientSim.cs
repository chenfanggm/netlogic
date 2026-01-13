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
            while (_net.TryReceive(out IMessage msg))
            {
                switch (msg)
                {
                    case WelcomeMsg welcome:
                        {
                            PlayerId = welcome.PlayerId;
                            TickRateHz = welcome.TickRateHz;

                            _timeSync.SetTickRate(TickRateHz);
                            _timeSync.OnSnapshotReceived(welcome.ServerTick);

                            Console.WriteLine("[Client] Welcome: PlayerId=" + PlayerId + " ServerTick=" + welcome.ServerTick + " Rate=" + TickRateHz + "Hz");
                            break;
                        }

                    case AckMsg ack:
                        {
                            if (ack.AckClientSeq > _lastAckedSeq)
                                _lastAckedSeq = ack.AckClientSeq;
                            break;
                        }

                    case SnapshotMsg snapshot:
                        {
                            _snapshots.Add(snapshot);
                            _timeSync.OnSnapshotReceived(snapshot.Tick);
                            break;
                        }
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

            EntityState[] renderStates;
            if (!_snapshots.TryGetPairForTickDouble(renderTick, out SnapshotMsg a, out SnapshotMsg b, out double t))
            {
                // Fallback: return most recent snapshot if any
                int est = (int)Math.Floor(estServerTick);
                if (_snapshots.TryGet(est, out SnapshotMsg latest))
                    renderStates = latest.Entities;

                return []; // empty array
            }

            renderStates = _interpolator.Interpolate(a, b, t);
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
