using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace RoguesVRaiders.Objective
{
    internal enum ObjectiveState { None, Travel, Patrol, Lockdown, Hunt }

    internal class SquadBlackboard
    {
        public readonly BotsGroup Group;
        public BotOwner Leader;
        public ObjectiveState State = ObjectiveState.None;

        // TRAVEL: a single destination the leader walks to, then transitions.
        public Vector3 TravelTarget;

        // PATROL: an ordered ring of validated points; leader walks them in order with dwell.
        public List<Vector3> Ring = new List<Vector3>();
        public Vector3 RingCenter;
        public int RingIndex;
        public float DwellUntil;
        public float PatrolSince;
        public Vector3 LockdownAnchor;
        public readonly Dictionary<BotOwner, Vector3> Posts = new Dictionary<BotOwner, Vector3>();

        // HUNT: the enemy squad we were sent to eliminate, where they were last seen, and whether our
        // squad actually fought them (gates the takeover - a third-party wipe grants no POI).
        public BotsGroup HuntTarget;
        public Vector3 HuntTargetPos;
        public bool FoughtTarget;

        // Movement throttling: re-issue orders only when the leader has moved this far since the last order.
        public Vector3 LastLeaderOrderPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

        public SquadBlackboard(BotsGroup group) { Group = group; }
    }
}
