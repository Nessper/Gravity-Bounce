namespace VoidScrappers.Briefing
{
    public static class LevelBriefingFormatter
    {
        public static PhaseBriefingOutput Format(PhaseBriefingInput input, BriefingTier tier)
        {
            switch (tier)
            {
                case BriefingTier.T0:
                    return FormatT0();

                case BriefingTier.T1:
                    return FormatT1(input);

                case BriefingTier.T2:
                    return FormatT2(input);

                case BriefingTier.T3:
                default:
                    return FormatT3(input);
            }
        }

        private static PhaseBriefingOutput FormatT0()
        {
            // T0 : structure visible, information totalement masquée
            return new PhaseBriefingOutput
            {
                Line1 = "Time ?    Int ?",
                Line2 = "Drops planned",
                Line3 = "W ?  B ?  R ?",
                Line4 = "Black ?"
            };
        }

        private static PhaseBriefingOutput FormatT1(PhaseBriefingInput input)
        {
            // T1 : bands qualitatifs (aucun chiffre exploitable).
            // - Duree : Short/Medium/Long/Extended (ratio sur total level)
            // - Tempo : Calm/Steady/Fast/Chaotic (interval)
            // - Drops : Light/Moderate/Heavy/Overload (sur nombre de drops)
            // - Mix : W/B/R en bands (Few/Some/High/Dominant)
            // - Black : None/Low/Medium/High/Trap

            string durBand = ResolveDurationBand(input);
            string tempoBand = ResolveTempoBand_W1(input.SpawnIntervalSec);

            string dropsBand = ResolveDropsBand(input.DropCount);
            string dropsPlanned = dropsBand + " planned";

            string mixBand = ResolveRewardMixBand(input);
            string blackRisk = ResolveBlackRiskBand(input);

            return new PhaseBriefingOutput
            {
                Line1 = "Time " + durBand + "    Int " + tempoBand,
                Line2 = dropsPlanned,
                Line3 = mixBand,
                Line4 = blackRisk
            };
        }

        private static PhaseBriefingOutput FormatT3(PhaseBriefingInput input)
        {
            // T3 : verite brute (Tmax).
            int durSec = RoundToInt(input.PhaseDurationSec);
            string interval = input.SpawnIntervalSec.ToString("0.0");

            return new PhaseBriefingOutput
            {
                Line1 = "Time " + durSec + "s    Int " + interval + "s",
                Line2 = input.DropCount + " drops planned",
                Line3 = "W " + input.WhiteCount + "  B " + input.BlueCount + "  R " + input.RedCount,
                Line4 = "Black " + input.BlackCount
            };
        }

        private static PhaseBriefingOutput FormatT2(PhaseBriefingInput input)
        {
            // T2 : semi-quantitatif.
            // - Time : secondes arrondies
            // - Int  : arrondi 0.1s avec ~
            // - Drops: exact (pas besoin de le flouter ici)
            // - Mix : ranges courts (W 10-12 etc.)
            // - Black : label (bands)

            int durSec = RoundToInt(input.PhaseDurationSec);
            string interval = input.SpawnIntervalSec.ToString("0.0");

            string mixRanges = BuildMixRangesT2(input);
            string blackBand = ResolveBlackRiskBand(input);

            return new PhaseBriefingOutput
            {
                Line1 = "Time " + durSec + "s    Int ~" + interval + "s",
                Line2 = input.DropCount + " drops planned",
                Line3 = mixRanges,
                Line4 = blackBand
            };
        }

        private static int RoundToInt(float value)
        {
            // Equivalent Mathf.RoundToInt, sans dependance UnityEngine.
            return (int)(value >= 0f ? value + 0.5f : value - 0.5f);
        }

        // ------------------------------------------------------------
        // T1 BANDS
        // ------------------------------------------------------------

        private static string ResolveDurationBand(PhaseBriefingInput input)
        {
            // Duree en % du total niveau.
            if (input.LevelTotalDurationSec <= 0.001f)
                return "Time ?";

            float ratio = input.PhaseDurationSec / input.LevelTotalDurationSec;

            if (ratio < 0.15f) return "Short";
            if (ratio < 0.30f) return "Medium";
            if (ratio < 0.50f) return "Long";
            return "Extended";
        }

        private static string ResolveTempoBand_W1(float intervalSec)
        {
            // Thresholds calibres pour W1 (a ajuster plus tard).
            if (intervalSec >= 1.7f) return "Calm";
            if (intervalSec >= 1.45f) return "Steady";
            if (intervalSec >= 1.15f) return "Fast";
            return "Chaotic";
        }

        private static string ResolveDropsBand(int drops)
        {
            // Bands simples, lies au nombre de drops prevus dans la phase.
            // A ajuster avec ton equilibrage.
            if (drops <= 0) return "Drops ?";
            if (drops <= 8) return "Light drops";
            if (drops <= 14) return "Moderate drops";
            if (drops <= 22) return "Heavy drops";
            return "Overload drops";
        }

        private static string ResolveRewardMixBand(PhaseBriefingInput input)
        {
            // Mix W/B/R (hors billes noires).
            int total = input.WhiteCount + input.BlueCount + input.RedCount;
            if (total <= 0)
                return "Mix ?";

            float w = (float)input.WhiteCount / total;
            float b = (float)input.BlueCount / total;
            float r = (float)input.RedCount / total;

            return "W " + BandFromRatio(w) + "  B " + BandFromRatio(b) + "  R " + BandFromRatio(r);
        }

        private static string BandFromRatio(float ratio)
        {
            if (ratio <= 0f) return "None";
            if (ratio < 0.16f) return "Few";
            if (ratio < 0.41f) return "Some";
            if (ratio < 0.71f) return "High";
            return "Dominant";
        }

        private static string ResolveBlackRiskBand(PhaseBriefingInput input)
        {
            // Proportion de noires sur total drops, avec garde-fou pour Trap.
            if (input.DropCount <= 0)
                return "Black ?";

            if (input.BlackCount <= 0)
                return "Black None";

            float p = (float)input.BlackCount / input.DropCount;

            if (p < 0.10f) return "Black Low";
            if (p < 0.25f) return "Black Medium";
            if (p < 0.45f) return "Black High";

            bool canTrap = input.DropCount >= 12;
            return canTrap ? "Black Trap" : "Black High";
        }

        // ------------------------------------------------------------
        // T2 MIX RANGES (semi-quantitatif)
        // ------------------------------------------------------------

        private static string BuildMixRangesT2(PhaseBriefingInput input)
        {
            int totalDrops = input.DropCount;
            if (totalDrops <= 0)
                return "Mix ?";

            string w = RangeAround(input.WhiteCount, totalDrops);
            string b = RangeAround(input.BlueCount, totalDrops);
            string r = RangeAround(input.RedCount, totalDrops);

            return "W " + w + "  B " + b + "  R " + r;
        }

        private static string RangeAround(int exact, int max)
        {
            if (exact <= 0)
                return "0";

            int min = exact - 1;
            int maxV = exact + 1;

            if (min < 0) min = 0;
            if (maxV > max) maxV = max;

            if (min == maxV)
                return exact.ToString();

            if (exact == 1 && min == 0)
                min = 1;

            if (exact == max && maxV == max)
                min = exact - 1;

            if (min < 0) min = 0;

            return min.ToString() + "-" + maxV.ToString();
        }
    }
}
