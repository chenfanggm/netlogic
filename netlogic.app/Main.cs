using Program;

namespace App
{
    public static class Program
    {
        // Set to true to run StepTickProgram, false to run LoopTickProgram
        private static readonly int program = 1;

        public static void Main()
        {
            switch (program)
            {
                case 1:
                    AutoEngineTickProgram.Run(maxRunningDuration: TimeSpan.FromSeconds(6));
                    break;
                case 2:
                    AutoTickProgram.Run(maxRunningDuration: TimeSpan.FromSeconds(6));
                    break;
                case 3:
                    ManualTickProgram.Run(totalTicks: 400);
                    break;
                default:
                    throw new ArgumentException("Invalid program");
            }
        }
    }
}
