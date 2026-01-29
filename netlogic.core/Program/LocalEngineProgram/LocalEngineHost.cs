
using com.aqua.netlogic.sim.serverengine;
using com.aqua.netlogic.sim.timing;

namespace com.aqua.netlogic.program
{
    /// <summary>
    /// Orchestrates the three loops:
    /// - Engine tick loop (authoritative)
    /// - Input pump (inject commands)
    /// - Output pump (render latest snapshot)
    ///
    /// Boundary rule:
    /// - Only the engine thread calls engine.TickOnce().
    /// - Input/output threads may read engine.CurrentServerTick / snapshots via LatestValue.
    /// </summary>
    public sealed class LocalEngineHost(
        IServerEngine engine,
        TickRunner runner,
        IInputPump input,
        IOutputPump output,
        LatestValue<TickSnapshot> latest)
    {
        private readonly IServerEngine _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        private readonly TickRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        private readonly IInputPump _input = input ?? throw new ArgumentNullException(nameof(input));
        private readonly IOutputPump _output = output ?? throw new ArgumentNullException(nameof(output));
        private readonly LatestValue<TickSnapshot> _latest = latest ?? throw new ArgumentNullException(nameof(latest));

        public void Run(CancellationToken token)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            CancellationToken ct = linkedCts.Token;

            Thread engineThread = new Thread(() => _runner.Run(
                onTick: ctx =>
                {
                    // The engine owns authoritative tick advancement.
                    using TickFrame frame = _engine.TickOnce(ctx);
                    TickFrame owned = frame.Clone();
                    com.aqua.netlogic.sim.game.snapshot.GameSnapshot snap = _engine.BuildSnapshot();
                    _latest.Publish(new TickSnapshot(owned, snap));
                },
                token: token))
            {
                IsBackground = true,
                Name = "Harness.EngineTick"
            };

            Thread inputThread = new Thread(() => _input.Run(_engine, ct))
            {
                IsBackground = true,
                Name = "Harness.Input"
            };

            Thread outputThread = new Thread(() => _output.Run(_latest, ct))
            {
                IsBackground = true,
                Name = "Harness.Output"
            };

            engineThread.Start();
            inputThread.Start();
            outputThread.Start();

            try
            {
                while (!ct.IsCancellationRequested)
                    Thread.Sleep(50);
            }
            finally
            {
                linkedCts.Cancel();

                engineThread.Join();
                inputThread.Join();
                outputThread.Join();
            }
        }
    }
}
