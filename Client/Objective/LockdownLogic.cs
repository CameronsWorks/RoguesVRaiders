using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace RoguesVRaiders.Objective
{
    internal class LockdownLogic : CustomLogic
    {
        const float ResumeSettle = 2f;
        const float ReachDist = 1.5f;
        const float ReissueDist = 1f;

        float _startedAt;

        public LockdownLogic(BotOwner botOwner) : base(botOwner) { }

        public override void Start()
        {
            // Drop any throttle carried in from the previous logic. A post never moves, so a bot arriving
            // with an entry already on it would wait for a destination change that can never come.
            var bb = RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);
            if (bb != null) bb.LastOrderTarget.Remove(BotOwner);
            _startedAt = Time.time;
        }

        public override void Update(CustomLayer.ActionData data)
        {
            var bb = RvRObjectiveController.GetBlackboard(BotOwner.BotsGroup);
            if (bb == null || SainInterop.SainOwns(BotOwner)) return;
            if (Time.time - _startedAt < ResumeSettle) return;

            var post = bb.Posts.TryGetValue(BotOwner, out var p) ? p : bb.LockdownAnchor;

            if ((BotOwner.Position - post).sqrMagnitude <= ReachDist * ReachDist)
            {
                BotOwner.StopMove();
                var outward = post - bb.LockdownAnchor;
                if (outward.sqrMagnitude > 0.01f) BotOwner.Steering.LookToDirection(outward);
                return;
            }

            if (!NavMesh.SamplePosition(post, out var hit, 3f, NavMesh.AllAreas)) return;
            BotOwner.SetTargetMoveSpeed(1f);

            if (Movement.ShouldReissue(bb, BotOwner, hit.position, ReissueDist) &&
                BotOwner.Mover.GoToPoint(hit.position, true, ReachDist, mustHaveWay: false) == NavMeshPathStatus.PathInvalid)
            {
                bb.LastOrderTarget.Remove(BotOwner);   // retry next tick
                return;
            }

            BotOwner.Steering.LookToPoint(hit.position);
        }
    }
}
