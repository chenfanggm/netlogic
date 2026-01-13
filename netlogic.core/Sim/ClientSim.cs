using System;
using System.Collections.Generic;
using Net;

namespace Sim
{
    public sealed class ClientSim
    {
        private readonly ITransportEndpoint _net;

        public int PlayerId { get; private set; }
        public int ServerTick { get; private set; }
        public int TickRateHz { get; private set; }

        private uint _clientSeq;
        private uint _lastAckedSeq;

        // In real client: keep ring buffer of snapshots for interpolation
        public SnapshotMsg? LastSnapshot { get; private set; }

        // Practice: fixed input delay (ticks ahead of estimated server tick)
        public int InputDelayTicks { get; set; } = 3;

        public ClientSim(ITransportEndpoint net)
        {
            _net = net;
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
                        PlayerId = welcome.PlayerId;
                        ServerTick = welcome.ServerTick;
                        TickRateHz = welcome.TickRateHz;
                        Console.WriteLine($"[Client] Welcome: PlayerId={PlayerId}, ServerTick={ServerTick}, Rate={TickRateHz}Hz");
                        break;

                    case AckMsg ack:
                        if (ack.AckClientSeq > _lastAckedSeq)
                            _lastAckedSeq = ack.AckClientSeq;
                        break;

                    case SnapshotMsg snap:
                        LastSnapshot = snap;
                        ServerTick = snap.Tick; // crude sync for now
                        break;
                }
            }
        }

        public void SendMoveCommand(int entityId, short dx, short dy)
        {
            if (PlayerId == 0) return;

            int targetTick = ServerTick + InputDelayTicks;
            int packed = (dx << 16) | (ushort)dy;

            List<Command> cmds = new(1)
            {
                new Command(PlayerId, targetTick, CommandType.Move, entityId, packed)
            };

            CommandBatchMsg msg = new(++_clientSeq, PlayerId, cmds);
            _net.Send(msg);
        }
    }
}
