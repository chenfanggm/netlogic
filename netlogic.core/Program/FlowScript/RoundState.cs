namespace com.aqua.netlogic.program.flowscript
{
    internal sealed class RoundState
    {
        public double EnteredAtMs = -1;
        public double LastMoveAtMs = -1;
        public int CookCyclesCompleted;
        public bool WaitingForContinue;

        public void Reset()
        {
            EnteredAtMs = -1;
            LastMoveAtMs = -1;
            CookCyclesCompleted = 0;
            WaitingForContinue = false;
        }
    }
}
