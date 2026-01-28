using com.aqua.netlogic.program;

namespace com.aqua.netlogic.app
{
    public static class Program
    {
        // Set to true to run StepTickProgram, false to run LoopTickProgram
        private static readonly int program = 4;

        public static void Main()
        {
            switch (program)
            {
                case 1:
                    new LocalEngineProgram().Run(
                        new ProgramConfig(tickRateHz: 20, maxRunDuration: TimeSpan.FromSeconds(15)));
                    break;
                case 4:
                    new LocalClientEngineProgram().Run(
                        new ProgramConfig(tickRateHz: 10, maxRunDuration: TimeSpan.FromSeconds(15)));
                    break;
                default:
                    throw new ArgumentException("Invalid program");
            }
        }
    }
}
