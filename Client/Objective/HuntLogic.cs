using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace RoguesVRaiders.Objective
{
    internal class HuntLogic : CustomLogic
    {
        const float ReissueDist = 2f;
        const float ReachDist = 6f;
        const float SprintBeyond = 30f;
        const float ResumeSettle = 2f;

        float _startedAt;

        public HuntLogic(BotOwner botOwner) : base(botOwner) { }

        public override void Start()
        {
            var bb = RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);
            if (bb != null) bb.LastLeaderOrderPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _startedAt = Time.time;
        }

        public override void Update(CustomLayer.ActionData data)
        {
            var bb = RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);
            if (bb == null || SainInterop.SainOwns(BotOwner)) return;
            if (Time.time - _startedAt < ResumeSettle) return;

            var isLeader = ReferenceEquals(BotOwner, bb.Leader);
            var target = isLeader ? bb.HuntTargetPos : Follower.SlotTarget(BotOwner, bb);
            Movement.DriveTo(BotOwner, bb, target, isLeader, ReachDist, SprintBeyond, ReissueDist, slowAtTheEnd: true);
        }
    }
}
