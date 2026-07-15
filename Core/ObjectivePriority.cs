namespace RoguesVRaiders.Core
{
    public static class ObjectivePriority
    {
        public static int Compute(int combatSolo, int combatSquad, int extract, int gap, int floor)
        {
            var min = combatSolo;
            if (combatSquad < min) min = combatSquad;
            if (extract < min) min = extract;

            var p = min - gap;
            if (p < floor) p = floor;
            if (p >= min) p = min - 1;
            return p;
        }
    }
}
