using Program;

namespace App
{
    public static class Program
    {
        // Set to true to run StepTickProgram, false to run LoopTickProgram
        private static readonly bool ManualTick = false;

        public static void Main()
        {
            if (ManualTick)
            {
                ManualTickProgram.Run(totalTicks: 400);
            }
            else
            {
                AutoTickProgram.Run(maxRunningDuration: TimeSpan.FromSeconds(6));
            }
        }
    }
}
