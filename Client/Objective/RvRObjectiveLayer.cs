using DrakiaXYZ.BigBrain.Brains;
using EFT;

namespace RoguesVRaiders.Objective
{
    internal class RvRObjectiveLayer : CustomLayer
    {
        ObjectiveState _pickedState = ObjectiveState.None;

        public RvRObjectiveLayer(BotOwner botOwner, int priority) : base(botOwner, priority) { }

        public override string GetName() => "RvRObjective";

        SquadBlackboard Blackboard => RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);

        public override bool IsActive()
        {
            if (!RvRPlugin.MasterEnable.Value || !RvRPlugin.ObjectiveEnable.Value) return false;
            if (!SquadRegistry.IsRvRSquadMember(BotOwner)) return false;

            var bb = Blackboard;
            if (bb == null || bb.State == ObjectiveState.None) return false;

            if (SainInterop.SainOwns(BotOwner)) return false;
            return true;
        }

        public override Action GetNextAction()
        {
            var bb = Blackboard;
            _pickedState = bb?.State ?? ObjectiveState.None;
            switch (_pickedState)
            {
                case ObjectiveState.Travel: return new Action(typeof(TravelLogic), "travel");
                case ObjectiveState.Lockdown: return new Action(typeof(LockdownLogic), "lockdown");
                case ObjectiveState.Hunt: return new Action(typeof(HuntLogic), "hunt");
                default: return new Action(typeof(PatrolLogic), "patrol");
            }
        }

        public override bool IsCurrentActionEnding()
        {
            var bb = Blackboard;
            return bb == null || bb.State != _pickedState;
        }
    }
}
