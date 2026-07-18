using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace RoguesVRaiders.Objective
{
    internal static class RvRObjectiveController
    {
        const float TravelThreshold = 30f;   // farther than this from the ring => TRAVEL first, else PATROL directly
        const float ArriveDist = 6f;
        const float PostRadius = 15f;        // cover points within this of the anchor are candidates
        const float MinPostSpacing = 2f;     // dedup near-identical posts
        const int PostSlack = 2;             // gather a couple extra beyond squad size
        static readonly float[] RadialRadii = { 4f, 8f, 12f };
        const int RadialHeadings = 8;
        const float EngageDist = 60f;        // a hunter member in SAIN combat within this of the target counts as "fought them"

        static readonly Dictionary<BotsGroup, SquadBlackboard> _blackboards = new Dictionary<BotsGroup, SquadBlackboard>();
        static readonly HashSet<Vector3> _takenRings = new HashSet<Vector3>();

        public static SquadBlackboard GetBlackboard(BotsGroup group)
        {
            if (group == null) return null;
            _blackboards.TryGetValue(group, out var bb);
            return bb;
        }

        public static void Reset()
        {
            _blackboards.Clear();
            _takenRings.Clear();
            SuppressionPatches.Active = false;
            SuppressionPatches.ReportUnmatched();
        }

        // Called from TriggerScheduler.Update (~5s), host-only.
        public static void Tick()
        {
            if (!RvRPlugin.MasterEnable.Value || !RvRPlugin.ObjectiveEnable.Value)
            {
                SuppressionPatches.Active = false;
                return;
            }

            var live = new HashSet<BotsGroup>();
            foreach (var squad in SquadRegistry.ActiveSquads)
            {
                live.Add(squad.Group);
                if (!_blackboards.TryGetValue(squad.Group, out var bb))
                {
                    bb = new SquadBlackboard(squad.Group);
                    _blackboards[squad.Group] = bb;
                }
                Advance(squad, bb);
            }

            AssignHunts();

            // Drop blackboards for squads that no longer exist (wiped/released), freeing their ring.
            if (_blackboards.Count > live.Count)
            {
                var stale = new List<BotsGroup>();
                foreach (var g in _blackboards.Keys) if (!live.Contains(g)) stale.Add(g);
                foreach (var g in stale)
                {
                    if (_blackboards.TryGetValue(g, out var sb)) _takenRings.Remove(sb.RingCenter);
                    _blackboards.Remove(g);
                }
            }

            SuppressionPatches.Active = _blackboards.Count > 0;
        }

        static void Advance(WarbandSquad squad, SquadBlackboard bb)
        {
            bb.Leader = SquadRegistry.LeaderOf(squad);
            if (bb.Leader == null)
            {
                if (bb.State != ObjectiveState.None)
                {
                    _takenRings.Remove(bb.RingCenter);
                    bb.RingCenter = Vector3.zero;
                    bb.State = ObjectiveState.None;
                    bb.HuntTarget = null;
                    bb.FoughtTarget = false;
                }
                return;
            }

            if (bb.State == ObjectiveState.None)
            {
                if (!PoiRegistry.HasRings) return;
                var ring = PoiRegistry.NearestRing(bb.Leader.Position, _takenRings, out var center);
                if (ring == null) return;

                bb.Ring = ring;
                bb.RingCenter = center;
                bb.RingIndex = 0;
                var start = ring[0];
                if ((start - bb.Leader.Position).magnitude > TravelThreshold)
                {
                    bb.TravelTarget = start;
                    bb.State = ObjectiveState.Travel;
                }
                else
                {
                    bb.State = ObjectiveState.Patrol;
                    bb.PatrolSince = Time.time;
                }
                if (RvRPlugin.DebugLogs.Value)
                    RvRPlugin.Log.LogInfo($"RvR objective: squad of {squad.Group.Members.Count} -> {bb.State} ({ring.Count}-pt ring)");
                return;
            }

            if (bb.State == ObjectiveState.Travel &&
                (bb.Leader.Position - bb.TravelTarget).magnitude <= ArriveDist)
            {
                bb.State = ObjectiveState.Patrol;
                bb.PatrolSince = Time.time;
                bb.RingIndex = 0;
                if (RvRPlugin.DebugLogs.Value) RvRPlugin.Log.LogInfo("RvR objective: squad arrived -> Patrol");
            }

            if (bb.State == ObjectiveState.Patrol && RvRPlugin.LockdownEnable.Value &&
                Time.time - bb.PatrolSince >= RvRPlugin.LockdownDelay.Value)
            {
                TryLockdown(squad, bb);
            }

            if (bb.State == ObjectiveState.Hunt)
            {
                if (TargetAlive(bb.HuntTarget))
                {
                    RefreshHuntTarget(bb);
                    if (!bb.FoughtTarget && FoughtNearTarget(squad, bb)) bb.FoughtTarget = true;
                }
                else
                {
                    ResolveHunt(squad, bb);
                }
            }
        }

        static void TryLockdown(WarbandSquad squad, SquadBlackboard bb)
        {
            if (!LockdownAt(squad, bb, bb.RingCenter))
                bb.PatrolSince = Time.time; // too few posts - wait another delay before retrying
        }

        // Returns false (and does NOT change state) when there are too few valid posts to hold this anchor.
        static bool LockdownAt(WarbandSquad squad, SquadBlackboard bb, Vector3 anchor)
        {
            var members = new List<BotOwner>();
            foreach (var m in squad.Group.Members)
                if (m != null && !m.IsDead) members.Add(m);
            if (members.Count == 0) return false;

            var posts = ComputePosts(anchor, members.Count);
            if (posts.Count < members.Count)
            {
                if (RvRPlugin.DebugLogs.Value)
                    RvRPlugin.Log.LogInfo($"RvR lockdown: only {posts.Count} valid post(s) for {members.Count} - not holding");
                return false;
            }

            bb.Posts.Clear();
            var remaining = new List<Vector3>(posts);
            foreach (var m in members)
            {
                var best = 0;
                var bestLen = float.MaxValue;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var len = m.Mover.ComputePathLengthToPoint(remaining[i]);
                    if (len < bestLen) { bestLen = len; best = i; }
                }
                bb.Posts[m] = remaining[best];
                remaining.RemoveAt(best);
            }

            bb.LockdownAnchor = anchor;
            bb.State = ObjectiveState.Lockdown;
            if (RvRPlugin.DebugLogs.Value)
                RvRPlugin.Log.LogInfo($"RvR lockdown: squad of {members.Count} holding {bb.Posts.Count} posts");
            return true;
        }

        static List<Vector3> ComputePosts(Vector3 anchor, int needed)
        {
            var candidates = new List<Vector3>();

            var covers = Singleton<IBotGame>.Instance?.BotsController?.CoversData;
            if (covers?.Points != null)
            {
                foreach (var gp in covers.Points)
                    if (gp != null && (gp.Position - anchor).sqrMagnitude <= PostRadius * PostRadius)
                        candidates.Add(gp.Position);
            }
            candidates.AddRange(Core.Lockdown.RadialPosts(anchor, RadialRadii, RadialHeadings));

            var valid = new List<Vector3>();
            var path = new NavMeshPath();
            foreach (var c in candidates)
            {
                if (!NavMesh.SamplePosition(c, out var hit, 3f, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(anchor, hit.position, NavMesh.AllAreas, path)) continue;
                if (path.status != NavMeshPathStatus.PathComplete) continue;

                var dup = false;
                foreach (var v in valid)
                    if ((v - hit.position).sqrMagnitude < MinPostSpacing * MinPostSpacing) { dup = true; break; }
                if (dup) continue;

                valid.Add(hit.position);
                if (valid.Count >= needed + PostSlack) break;
            }
            return valid;
        }

        static Core.Warband? FactionOf(SquadBlackboard bb)
        {
            var leader = bb.Leader;
            if (leader == null) return null;
            return leader.Profile.Info.Settings.Role == WildSpawnType.exUsec ? Core.Warband.Rogue : Core.Warband.Raider;
        }

        static void AssignHunts()
        {
            if (!RvRPlugin.HuntEnable.Value) return;

            foreach (var hb in _blackboards.Values)
            {
                if (hb.State != ObjectiveState.Patrol || hb.HuntTarget != null || hb.Leader == null) continue;
                var hunterFaction = FactionOf(hb);
                if (hunterFaction == null) continue;

                SquadBlackboard best = null;
                var bestDist = float.MaxValue;
                foreach (var tb in _blackboards.Values)
                {
                    if (tb.State != ObjectiveState.Lockdown || tb.Group == hb.Group) continue;
                    var tf = FactionOf(tb);
                    if (tf == null || tf == hunterFaction) continue;   // opposite faction only
                    if (IsHunted(tb.Group)) continue;                  // one hunter per target
                    var d = (tb.LockdownAnchor - hb.Leader.Position).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = tb; }
                }
                if (best == null) continue;

                hb.HuntTarget = best.Group;
                hb.HuntTargetPos = best.LockdownAnchor;
                hb.FoughtTarget = false;
                hb.State = ObjectiveState.Hunt;
                if (RvRPlugin.DebugLogs.Value)
                    RvRPlugin.Log.LogInfo($"RvR hunt: {hunterFaction} squad sent to hunt a {FactionOf(best)} lockdown");
            }
        }

        static bool IsHunted(BotsGroup target)
        {
            foreach (var bb in _blackboards.Values)
                if (bb.HuntTarget == target) return true;
            return false;
        }

        static bool TargetAlive(BotsGroup target)
        {
            if (target == null) return false;
            foreach (var m in target.Members)
                if (m != null && !m.IsDead) return true;
            return false;
        }

        static void RefreshHuntTarget(SquadBlackboard bb)
        {
            foreach (var m in bb.HuntTarget.Members)
                if (m != null && !m.IsDead) { bb.HuntTargetPos = m.Position; return; }
        }

        static bool FoughtNearTarget(WarbandSquad squad, SquadBlackboard bb)
        {
            foreach (var m in squad.Group.Members)
            {
                if (m == null || m.IsDead) continue;
                if (!SainInterop.SainInCombat(m)) continue;
                if ((m.Position - bb.HuntTargetPos).sqrMagnitude <= EngageDist * EngageDist) return true;
            }
            return false;
        }

        static void ResolveHunt(WarbandSquad squad, SquadBlackboard bb)
        {
            var anchor = bb.HuntTargetPos;   // where the target was holding = the POI to inherit
            var fought = bb.FoughtTarget;
            bb.HuntTarget = null;
            bb.FoughtTarget = false;

            if (fought && LockdownAt(squad, bb, anchor))
            {
                _takenRings.Add(anchor);
                if (RvRPlugin.DebugLogs.Value)
                    RvRPlugin.Log.LogInfo("RvR hunt: target eliminated - taking over the POI");
                return;
            }

            bb.State = ObjectiveState.Patrol;
            bb.PatrolSince = Time.time;
            if (RvRPlugin.DebugLogs.Value)
                RvRPlugin.Log.LogInfo(fought ? "RvR hunt: target dead but no valid posts - back to patrol"
                                             : "RvR hunt: target died to a third party - no takeover");
        }
    }

    internal static class Movement
    {
        // Every bot is throttled, not just the leader: a follower's slot moves with the leader, so an
        // unthrottled follower re-pathed on every frame. Steering and sprint stay per-frame - only the
        // path request is throttled.
        public static void DriveTo(BotOwner bot, SquadBlackboard bb, Vector3 target,
            float reachDist, float sprintBeyond, float reissueDist, bool slowAtTheEnd)
        {
            if (!NavMesh.SamplePosition(target, out var hit, 5f, NavMesh.AllAreas)) return;

            if (ShouldReissue(bb, bot, hit.position, reissueDist) &&
                bot.Mover.GoToPoint(hit.position, slowAtTheEnd, reachDist, mustHaveWay: false) == NavMeshPathStatus.PathInvalid)
            {
                bb.LastOrderTarget.Remove(bot);   // retry next tick
                return;
            }

            bot.Sprint(bot.Mover.DistDestination > sprintBeyond);
            bot.Steering.LookToMovingDirection();
        }

        // mustHaveWay is left off wherever we path: with it on, EFT answers a failed path by teleporting the
        // bot to its nearest cover node, and a formation slot lands off the cover graph often enough that it
        // shows. The returned PathInvalid is no defence - the snap happens inside the call.
        public static bool ShouldReissue(SquadBlackboard bb, BotOwner bot, Vector3 target, float reissueDist)
        {
            if (bb.LastOrderTarget.TryGetValue(bot, out var last) &&
                (last - target).sqrMagnitude < reissueDist * reissueDist) return false;

            bb.LastOrderTarget[bot] = target;
            return true;
        }
    }

    internal static class Follower
    {
        const float Spacing = 3f;
        const float Lateral = 2f;

        public static Vector3 SlotTarget(BotOwner bot, SquadBlackboard bb)
        {
            var leader = bb.Leader;
            if (leader == null) return bot.Position;
            var slot = SlotOf(bb.Group.Members, leader, bot);
            return Core.Formation.SlotOffset(leader.Position, leader.LookDirection, slot, Spacing, Lateral);
        }

        static int SlotOf(System.Collections.Generic.List<BotOwner> members, BotOwner leader, BotOwner bot)
        {
            var slot = 0;
            foreach (var m in members)
            {
                if (ReferenceEquals(m, leader)) continue;
                if (ReferenceEquals(m, bot)) return slot;
                slot++;
            }
            return slot;
        }
    }
}
