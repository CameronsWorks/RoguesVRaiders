namespace RoguesVRaiders.Core
{
    public enum Warband { Rogue, Raider }

    public static class Hostility
    {
        // WildSpawnType role ints (decompile-confirmed).
        const int ExUsec = 24;
        const int PmcBot = 9;
        const int BossKnight = 26;
        const int FollowerBigPipe = 27;
        const int FollowerBirdEye = 28;

        // Warbands fight every AI role except their own faction; rogues additionally spare the Goons
        // (raiders fight them). Sparing the own faction also covers vanilla Lighthouse rogues / Reserve
        // raiders and other same-role RvR squads. The player relationship is handled elsewhere (Friendliness).
        public static bool IsHostileRole(int targetRole, Warband self)
        {
            if (self == Warband.Rogue)
            {
                if (targetRole == ExUsec) return false;
                if (targetRole == BossKnight || targetRole == FollowerBigPipe || targetRole == FollowerBirdEye)
                    return false;
                return true;
            }
            if (targetRole == PmcBot) return false;
            return true;
        }
    }
}
