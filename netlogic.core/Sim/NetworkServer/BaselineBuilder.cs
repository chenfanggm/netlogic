using Net;
using Net.WireState;
using Sim.Engine;
using Sim.Snapshot;

namespace Sim.Server
{
    internal static class BaselineBuilder
    {
        public static BaselineMsg Build(GameSnapshot snap, int serverTick, uint stateHash)
        {
            FlowSnapshot flow = snap.Flow;
            WireFlowState wireFlow = new WireFlowState(
                (int)flow.FlowState,
                flow.LevelIndex,
                flow.RoundIndex,
                flow.SelectedChefHatId,
                flow.TargetScore,
                flow.CumulativeScore,
                flow.CookAttemptsUsed,
                (int)flow.RoundState,
                flow.CookResultSeq,
                flow.LastCookScoreDelta,
                flow.LastCookMetTarget);

            SampleEntityPos[] ents = snap.Entities;
            WireEntityState[] wireEnts = new WireEntityState[ents.Length];
            for (int i = 0; i < ents.Length; i++)
            {
                SampleEntityPos e = ents[i];
                wireEnts[i] = new WireEntityState(e.EntityId, e.X, e.Y, e.Hp);
            }

            return new BaselineMsg(
                ProtocolVersion.Current,
                HashContract.ScopeId,
                (byte)HashContract.Phase,
                StateSchema.BaselineSchemaId,
                serverTick,
                stateHash,
                wireFlow,
                wireEnts);
        }
    }
}
