using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace RoguesVRaiders.Objective
{
    internal class PatrolLogic : CustomLogic
    {
        const float ReissueDist = 2f;
        const float ReachDist = 3f;
        const float DwellSeconds = 4f;
        const float PatrolSpeed = 0.7f;
        const float ResumeSettle = 2f;

        float _startedAt;

        public PatrolLogic(BotOwner botOwner) : base(botOwner) { }

        public override void Start()
        {
            var bb = RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);
            if (bb != null) bb.LastOrderTarget.Remove(BotOwner);
            _startedAt = Time.time;
        }

        public override void Update(CustomLayer.ActionData data)
        {
            var bb = RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);
            if (bb == null || bb.Ring.Count == 0 || SainInterop.SainOwns(BotOwner)) return;
            if (Time.time - _startedAt < ResumeSettle) return;

            var isLeader = ReferenceEquals(BotOwner, bb.Leader);
            if (!isLeader)
            {
                Movement.DriveTo(BotOwner, bb, Follower.SlotTarget(BotOwner, bb), ReachDist, 40f, ReissueDist, slowAtTheEnd: false);
                return;
            }

            BotOwner.SetTargetMoveSpeed(PatrolSpeed);

            if (Time.time < bb.DwellUntil)
            {
                BotOwner.StopMove();
                return;
            }

            var target = bb.Ring[bb.RingIndex];
            if ((BotOwner.Position - target).sqrMagnitude <= ReachDist * ReachDist)
            {
                bb.DwellUntil = Time.time + DwellSeconds;
                bb.RingIndex = (bb.RingIndex + 1) % bb.Ring.Count;
                bb.LastOrderTarget.Remove(BotOwner);
                BotOwner.Steering.LookToMovingDirection();
                return;
            }

            Movement.DriveTo(BotOwner, bb, target, ReachDist, 40f, ReissueDist, slowAtTheEnd: true);
        }
    }
}
