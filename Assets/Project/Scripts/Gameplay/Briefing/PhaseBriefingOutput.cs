namespace VoidScrappers.Briefing
{
    public struct PhaseBriefingOutput
    {
        // 4 lignes max (hors titre "PHASE X" qui est deja dans ton UI)
        public string Line1; // temps + interval
        public string Line2; // drops + densite
        public string Line3; // mix W/B/R
        public string Line4; // hull + noirs
    }
}
