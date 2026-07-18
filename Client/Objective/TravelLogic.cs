using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace RoguesVRaiders.Objective
{
    internal class TravelLogic : CustomLogic
    {
        const float ReissueDist = 2f;
        const float ReachDist = 5f;
        const float SprintBeyond = 40f;
        const float ResumeSettle = 2f;

        float _startedAt;

        public TravelLogic(BotOwner botOwner) : base(botOwner) { }

        public override void Start()
        {
            var bb = RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);
            if (bb != null) bb.LastOrderTarget.Remove(BotOwner);
            _startedAt = Time.time;
        }

        public override void Update(CustomLayer.ActionData data)
        {
            var bb = RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);
            if (bb == null || SainInterop.SainOwns(BotOwner)) return;
            if (Time.time - _startedAt < ResumeSettle) return;

            var isLeader = ReferenceEquals(BotOwner, bb.Leader);
            var target = isLeader
                ? bb.TravelTarget
                : Follower.SlotTarget(BotOwner, bb);

            Movement.DriveTo(BotOwner, bb, target, ReachDist, SprintBeyond, ReissueDist, slowAtTheEnd: true);
        }
    }
}
