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
                    LocalEngineProgram.Run(maxRunningDuration: TimeSpan.FromSeconds(15));
                    break;
                case 4:
                    LocalClientEngineProgram.Run(maxRunningDuration: TimeSpan.FromSeconds(15));
                    break;
                default:
                    throw new ArgumentException("Invalid program");
            }
        }
    }
}
