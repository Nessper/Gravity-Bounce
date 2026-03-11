using TMPro;
using UnityEngine;
using VoidScrappers.Briefing;

/// <summary>
/// Chemin recommandé : Scripts/UI/Briefing/LevelBriefingPanelUI.cs
///
/// Renderer factorisé du "briefing" (utilisé par IntroLevelUI + PauseOverlayUI).
/// Responsabilités :
/// - Header (LevelID, WorldName, Title)
/// - Phases via LevelBriefingFormatter (T0-T3)
/// - Main objective + optional directives
/// - Score targets (Bronze/Silver/Gold)
///
/// Notes :
/// - Ce composant ne connaît PAS le Ship block (Intro) ni le Run Status (Pause).
/// - Le tier (T0-T3) est fourni par l'appelant (Intro: via RunSessionState / Pause idem).
/// </summary>
public class LevelBriefingPanelUI : MonoBehaviour
{
    // ------------------------------------------------------------
    // HEADER
    // ------------------------------------------------------------

    [Header("Header")]
    [SerializeField] private TMP_Text levelIdText;
    [SerializeField] private TMP_Text worldLevelText;
    [SerializeField] private TMP_Text titleText;

    // ------------------------------------------------------------
    // PHASES (T0-T3)
    // ------------------------------------------------------------

    [Header("Level Briefing - Phases")]
    [SerializeField] private TMP_Text[] phaseNameTexts;
    [SerializeField] private TMP_Text[] phaseDurationTexts;     // Line1
    [SerializeField] private TMP_Text[] phaseNodesTexts;        // Line2
    [SerializeField] private TMP_Text[] phaseMixTexts;          // Line3
    [SerializeField] private TMP_Text[] phaseSpawnSpeedTexts;   // Line4

    // ------------------------------------------------------------
    // OBJECTIFS / SCORE
    // ------------------------------------------------------------

    [Header("Main Objective")]
    [SerializeField] private TMP_Text mainObjectiveText;

    [Header("Optional Directives")]
    [SerializeField] private TMP_Text[] optionalDirectiveTexts;

    [Header("Score Targets")]
    [SerializeField] private TMP_Text bronzeGoalText;
    [SerializeField] private TMP_Text silverGoalText;
    [SerializeField] private TMP_Text goldGoalText;

    // ------------------------------------------------------------
    // SPRITES TMP
    // ------------------------------------------------------------

    [Header("Inline Sprites - Offset")]
    [SerializeField] private int spriteYOffset = -6;

    [Header("Briefing Icons (TMP Sprite Names)")]
    [SerializeField] private string spriteTimeName = "icon_time";
    [SerializeField] private string spriteIntervalName = "icon_interval";
    [SerializeField] private string spriteBallWhiteName = "ball_white";
    [SerializeField] private string spriteBallBlueName = "ball_blue";
    [SerializeField] private string spriteBallRedName = "ball_red";
    [SerializeField] private string spriteBallBlackName = "ball_black";

    // ------------------------------------------------------------
    // CACHE (pour Refresh rapide)
    // ------------------------------------------------------------

    private LevelData cachedLevelData;
    private PhasePlanInfo[] cachedPhasePlans;
    private string cachedWorldName;
    private string cachedTitle;
    private BriefingTier cachedTier;

    private const char Nbsp = '\u00A0';

    // ------------------------------------------------------------
    // API
    // ------------------------------------------------------------

    /// <summary>
    /// Remplit tout le bloc briefing et met en cache les paramètres.
    /// </summary>
    public void Render(
        LevelData data,
        PhasePlanInfo[] phasePlans,
        string worldName,
        string title,
        BriefingTier tier)
    {
        if (data == null)
            return;

        // Cache pour refresh "à chaud" (tier SCAN, etc.)
        cachedLevelData = data;
        cachedPhasePlans = phasePlans;
        cachedWorldName = worldName;
        cachedTitle = title;
        cachedTier = tier;

        RenderInternal(data, phasePlans, worldName, title, tier);
    }

    /// <summary>
    /// Re-render à partir du cache. Utile quand le tier change (modules SCAN)
    /// ou quand tu veux rafraîchir l'affichage sans rappeler l'orchestrateur.
    /// </summary>
    public void Refresh()
    {
        if (cachedLevelData == null)
            return;

        RenderInternal(cachedLevelData, cachedPhasePlans, cachedWorldName, cachedTitle, cachedTier);
    }

    /// <summary>
    /// Re-render depuis cache mais avec un nouveau tier (SCAN).
    /// </summary>
    public void RefreshWithTier(BriefingTier newTier)
    {
        cachedTier = newTier;
        Refresh();
    }

    // ------------------------------------------------------------
    // INTERNAL RENDER
    // ------------------------------------------------------------

    private void RenderInternal(
        LevelData data,
        PhasePlanInfo[] phasePlans,
        string worldName,
        string title,
        BriefingTier tier)
    {
        // HEADER
        if (levelIdText != null)
            levelIdText.text = data.LevelID;

        if (worldLevelText != null)
        {
            string w = string.IsNullOrEmpty(worldName) ? "World ?" : worldName;
            worldLevelText.text = w;
        }

        if (titleText != null)
            titleText.text = title ?? "";

        // PHASES
        ResetPhaseBriefingPlaceholders();
        FillPhasesBriefing(data, phasePlans, tier);

        // OBJECTIF PRINCIPAL
        if (mainObjectiveText != null)
        {
            if (data.MainObjective != null && !string.IsNullOrEmpty(data.MainObjective.Text))
                mainObjectiveText.text = data.MainObjective.Text;
            else
                mainObjectiveText.text = "-";
        }

        // OBJECTIFS SECONDAIRES
        FillSecondaryObjectives(data);

        // SCORE TARGETS
        FillScoreTargets(data);
    }

    // ------------------------------------------------------------
    // PHASES (formatter T0-T3)
    // ------------------------------------------------------------

    private void FillPhasesBriefing(LevelData data, PhasePlanInfo[] phasePlans, BriefingTier tier)
    {
        if (data.Phases == null || data.Phases.Length <= 0)
            return;

        if (phasePlans == null || phasePlans.Length <= 0)
            return;

        int phaseCount = Mathf.Min(
            data.Phases.Length,
            phasePlans.Length,
            phaseNameTexts != null ? phaseNameTexts.Length : int.MaxValue,
            phaseDurationTexts != null ? phaseDurationTexts.Length : int.MaxValue,
            phaseNodesTexts != null ? phaseNodesTexts.Length : int.MaxValue,
            phaseSpawnSpeedTexts != null ? phaseSpawnSpeedTexts.Length : int.MaxValue,
            phaseMixTexts != null ? phaseMixTexts.Length : int.MaxValue
        );

        // Total durée runtime (source fiable)
        float levelTotalDurationSec = 0f;
        for (int i = 0; i < phaseCount; i++)
            levelTotalDurationSec += phasePlans[i].DurationSec;

        for (int i = 0; i < phaseCount; i++)
        {
            PhasePlanInfo plan = phasePlans[i];

            if (phaseNameTexts != null && phaseNameTexts[i] != null)
                phaseNameTexts[i].text = plan.Name;

            // IMPORTANT :
            // On n'utilise PLUS le JSON pour recalculer un mix théorique.
            // Le briefing doit afficher le mix FINAL REEL préparé par le BallSpawner.
            int w = plan.WhiteCount;
            int b = plan.BlueCount;
            int r = plan.RedCount;
            int k = plan.BlackCount;

            PhaseBriefingInput input = new PhaseBriefingInput
            {
                PhaseName = plan.Name,
                PhaseDurationSec = plan.DurationSec,
                LevelTotalDurationSec = levelTotalDurationSec,
                SpawnIntervalSec = plan.IntervalSec,
                DropCount = plan.Quota,
                WhiteCount = w,
                BlueCount = b,
                RedCount = r,
                BlackCount = k
            };

            PhaseBriefingOutput output = LevelBriefingFormatter.Format(input, tier);

            // Remplacement tokens -> <sprite ...> avec offset via <voffset>
            string l1 = ReplaceBriefingTokensWithSprites(output.Line1);
            string l2 = ReplaceBriefingTokensWithSprites(output.Line2);
            string l3 = ReplaceBriefingTokensWithSprites(output.Line3);
            string l4 = ReplaceBriefingTokensWithSprites(output.Line4);

            if (phaseDurationTexts != null && phaseDurationTexts[i] != null)
                phaseDurationTexts[i].text = l1;

            if (phaseNodesTexts != null && phaseNodesTexts[i] != null)
                phaseNodesTexts[i].text = l2;

            if (phaseMixTexts != null && phaseMixTexts[i] != null)
                phaseMixTexts[i].text = l3;

            if (phaseSpawnSpeedTexts != null && phaseSpawnSpeedTexts[i] != null)
                phaseSpawnSpeedTexts[i].text = l4;
        }
    }

    // ------------------------------------------------------------
    // SPRITES TMP
    // ------------------------------------------------------------

    private string SpriteNameTag(string name, int yOffset)
    {
        if (string.IsNullOrEmpty(name))
            return "";

        string spriteTag = "<sprite name=\"" + name + "\">";

        if (yOffset == 0)
            return spriteTag;

        // TMP: pas de "y=" dans <sprite> sur ta version. On utilise <voffset>.
        return "<voffset=" + yOffset + ">" + spriteTag + "</voffset>";
    }

    private string ReplaceBriefingTokensWithSprites(string line)
    {
        if (string.IsNullOrEmpty(line))
            return line;

        // IMPORTANT: "Black" avant "B"
        line = line.Replace("Black", SpriteNameTag(spriteBallBlackName, spriteYOffset));

        // Labels
        line = line.Replace("Time", SpriteNameTag(spriteTimeName, spriteYOffset));
        line = line.Replace("Int", SpriteNameTag(spriteIntervalName, spriteYOffset));

        // Mix (lettres seules, sorties par le formatter)
        line = line.Replace("W", SpriteNameTag(spriteBallWhiteName, spriteYOffset));
        line = line.Replace("B", SpriteNameTag(spriteBallBlueName, spriteYOffset));
        line = line.Replace("R", SpriteNameTag(spriteBallRedName, spriteYOffset));

        // Fix: colle l’icône au token suivant (évite sprite en fin de ligne + nombre à la ligne)
        line = line.Replace("</voffset> ", "</voffset>" + Nbsp);

        return line;
    }

    // ------------------------------------------------------------
    // OBJECTIFS SECONDAIRES / SCORE
    // ------------------------------------------------------------

    private void FillSecondaryObjectives(LevelData data)
    {
        if (optionalDirectiveTexts == null)
            return;

        for (int i = 0; i < optionalDirectiveTexts.Length; i++)
        {
            if (optionalDirectiveTexts[i] != null)
                optionalDirectiveTexts[i].gameObject.SetActive(false);
        }

        if (data.SecondaryObjectives == null || data.SecondaryObjectives.Length <= 0)
            return;

        int count = Mathf.Min(data.SecondaryObjectives.Length, optionalDirectiveTexts.Length);
        for (int i = 0; i < count; i++)
        {
            var so = data.SecondaryObjectives[i];
            if (so == null || optionalDirectiveTexts[i] == null)
                continue;

            optionalDirectiveTexts[i].gameObject.SetActive(true);
            optionalDirectiveTexts[i].text = so.UiText;
        }
    }

    private void FillScoreTargets(LevelData data)
    {
        if (data.ScoreGoals != null && data.ScoreGoals.Length >= 3)
        {
            if (bronzeGoalText != null)
                bronzeGoalText.text = data.ScoreGoals[0].Points.ToString();
            if (silverGoalText != null)
                silverGoalText.text = data.ScoreGoals[1].Points.ToString();
            if (goldGoalText != null)
                goldGoalText.text = data.ScoreGoals[2].Points.ToString();
        }
        else
        {
            if (bronzeGoalText != null) bronzeGoalText.text = "-";
            if (silverGoalText != null) silverGoalText.text = "-";
            if (goldGoalText != null) goldGoalText.text = "-";
        }
    }

    

    // ------------------------------------------------------------
    // PLACEHOLDERS
    // ------------------------------------------------------------

    private void ResetPhaseBriefingPlaceholders()
    {
        if (phaseNameTexts != null)
            for (int i = 0; i < phaseNameTexts.Length; i++)
                if (phaseNameTexts[i] != null) phaseNameTexts[i].text = "-";

        if (phaseDurationTexts != null)
            for (int i = 0; i < phaseDurationTexts.Length; i++)
                if (phaseDurationTexts[i] != null) phaseDurationTexts[i].text = "-";

        if (phaseNodesTexts != null)
            for (int i = 0; i < phaseNodesTexts.Length; i++)
                if (phaseNodesTexts[i] != null) phaseNodesTexts[i].text = "-";

        if (phaseMixTexts != null)
            for (int i = 0; i < phaseMixTexts.Length; i++)
                if (phaseMixTexts[i] != null) phaseMixTexts[i].text = "-";

        if (phaseSpawnSpeedTexts != null)
            for (int i = 0; i < phaseSpawnSpeedTexts.Length; i++)
                if (phaseSpawnSpeedTexts[i] != null) phaseSpawnSpeedTexts[i].text = "-";
    }
}
