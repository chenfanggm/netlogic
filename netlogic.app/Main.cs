using Program;

namespace App
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
                    LocalEngineProgram.Run(maxRunningDuration: TimeSpan.FromSeconds(6));
                    break;
                case 2:
                    AutoTickProgram.Run(maxRunningDuration: TimeSpan.FromSeconds(6));
                    break;
                case 3:
                    ManualTickProgram.Run(totalTicks: 400);
                    break;
                case 4:
                    LocalClientEngineProgram.Run(maxRunningDuration: TimeSpan.FromSeconds(6));
                    break;
                default:
                    throw new ArgumentException("Invalid program");
            }
        }
    }
}
