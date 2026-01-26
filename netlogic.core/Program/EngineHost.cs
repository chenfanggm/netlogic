using System;
using System.Threading;
using Sim;

namespace Program
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
    public sealed class EngineHost
    {
        private readonly IGameEngine _engine;
        private readonly TickRunner _runner;
        private readonly IInputPump _input;
        private readonly IOutputPump _output;
        private readonly LatestValue<EngineTickResult> _latest;

        public EngineHost(
            IGameEngine engine,
            TickRunner runner,
            IInputPump input,
            IOutputPump output,
            LatestValue<EngineTickResult> latest)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _latest = latest ?? throw new ArgumentNullException(nameof(latest));
        }

        public void Run(CancellationToken token)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            CancellationToken ct = linkedCts.Token;

            Thread engineThread = new Thread(() => EngineLoop(ct))
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

        private void EngineLoop(CancellationToken token)
        {
            _runner.Run(
                onTick: ctx =>
                {
                    // The engine owns authoritative tick advancement.
                    EngineTickResult r = _engine.TickOnce(ctx);
                    _latest.Publish(r);
                },
                token: token);
        }
    }
}
