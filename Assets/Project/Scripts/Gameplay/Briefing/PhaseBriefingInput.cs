namespace VoidScrappers.Briefing
{
    public struct PhaseBriefingInput
    {
        public string PhaseName;

        public float PhaseDurationSec;
        public float LevelTotalDurationSec;

        public float SpawnIntervalSec;

        // Quota = nombre de drops prevus dans la phase (ton plan.Quota actuel)
        public int DropCount;

        // Mix exact en counts (T3) si disponible.
        // Si tu ne l'as pas, on pourra le calculer depuis le mix du JSON.
        public int WhiteCount;
        public int BlueCount;
        public int RedCount;
        public int BlackCount;
    }
}
