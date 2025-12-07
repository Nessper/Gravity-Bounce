using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class IntroLevelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject overlayIntro;

    [Header("Header")]
    [SerializeField] private TMP_Text levelIdText;
    [SerializeField] private TMP_Text worldLevelText;
    [SerializeField] private TMP_Text titleText;

    [Header("Level Briefing - Phases")]
    [SerializeField] private TMP_Text[] phaseNameTexts;
    [SerializeField] private TMP_Text[] phaseDurationTexts;
    [SerializeField] private TMP_Text[] phaseNodesTexts;
    [SerializeField] private TMP_Text[] phaseMixTexts;
    [SerializeField] private TMP_Text[] phaseSpawnSpeedTexts;

    [Header("Main Objective")]
    [SerializeField] private TMP_Text mainObjectiveText;

    [Header("Optional Directives")]
    [SerializeField] private TMP_Text[] optionalDirectiveTexts;

    [Header("Score Targets")]
    [SerializeField] private TMP_Text bronzeGoalText;
    [SerializeField] private TMP_Text silverGoalText;
    [SerializeField] private TMP_Text goldGoalText;

    [Header("Ship Info")]
    [SerializeField] private Image shipImage;
    [SerializeField] private TMP_Text shipNameText;
    [SerializeField] private TMP_Text shipHullText;
    [SerializeField] private TMP_Text shipShieldText;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;

    private System.Action onPlayCallback;
    private System.Action onBackCallback;

    // Valeurs runtime de hull (injectées par le briefing)
    private int runtimeHull = -1;
    private int runtimeMaxHull = -1;

    private void Awake()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    public void SetShipRuntimeHull(int currentHull, int maxHull)
    {
        runtimeHull = Mathf.Max(-1, currentHull);
        runtimeMaxHull = Mathf.Max(-1, maxHull);

        // Si l'overlay est déjà visible, on met à jour le texte tout de suite
        if (shipHullText != null && overlayIntro != null && overlayIntro.activeInHierarchy)
        {
            if (runtimeHull >= 0 && runtimeMaxHull > 0)
            {
                shipHullText.text = runtimeHull.ToString() + "/" + runtimeMaxHull.ToString();
            }
            else
            {
                // Fallback au cas où, mais normalement on ne devrait pas passer ici
                shipHullText.text = "x" + runtimeMaxHull.ToString();
            }
        }
    }


    public void Show(LevelData data, PhasePlanInfo[] phasePlans, System.Action onPlay, System.Action onBack)
    {
        if (data == null)
            return;

        onPlayCallback = onPlay;
        onBackCallback = onBack;

        // HEADER
        if (levelIdText != null)
            levelIdText.text = data.LevelID;

        if (worldLevelText != null)
        {
            string world = string.IsNullOrEmpty(data.World) ? "World ?" : data.World;
            worldLevelText.text = world;
        }

        if (titleText != null)
            titleText.text = data.Title;

        // PHASES
        ResetPhaseBriefingPlaceholders();

        if (data.Phases != null && data.Phases.Length > 0 &&
            phasePlans != null && phasePlans.Length > 0)
        {
            int phaseCount = Mathf.Min(
                data.Phases.Length,
                phasePlans.Length,
                phaseNameTexts != null ? phaseNameTexts.Length : int.MaxValue,
                phaseDurationTexts != null ? phaseDurationTexts.Length : int.MaxValue,
                phaseNodesTexts != null ? phaseNodesTexts.Length : int.MaxValue,
                phaseSpawnSpeedTexts != null ? phaseSpawnSpeedTexts.Length : int.MaxValue,
                phaseMixTexts != null ? phaseMixTexts.Length : int.MaxValue
            );

            var fr = new CultureInfo("fr-FR");

            for (int i = 0; i < phaseCount; i++)
            {
                var plan = phasePlans[i];

                if (phaseNameTexts != null && phaseNameTexts[i] != null)
                    phaseNameTexts[i].text = plan.Name;

                if (phaseDurationTexts != null && phaseDurationTexts[i] != null)
                {
                    int rounded = Mathf.RoundToInt(plan.DurationSec);
                    phaseDurationTexts[i].text = "Duration : " + rounded.ToString() + "s";
                }

                if (phaseNodesTexts != null && phaseNodesTexts[i] != null)
                    phaseNodesTexts[i].text = "Nodes : " + plan.Quota.ToString();

                if (phaseSpawnSpeedTexts != null && phaseSpawnSpeedTexts[i] != null)
                {
                    string intervalText = "Spawn interval : " + plan.IntervalSec.ToString("0.0", fr) + "s";
                    phaseSpawnSpeedTexts[i].text = intervalText;
                }

                if (phaseMixTexts != null && phaseMixTexts[i] != null)
                {
                    var phase = data.Phases[i];
                    var mixEntries = phase.Mix;

                    if (mixEntries == null || mixEntries.Length == 0)
                    {
                        phaseMixTexts[i].text = "-";
                    }
                    else
                    {
                        float totalPoids = 0f;
                        for (int k = 0; k < mixEntries.Length; k++)
                        {
                            var m = mixEntries[k];
                            if (m == null || m.Poids <= 0f)
                                continue;

                            totalPoids += m.Poids;
                        }

                        if (totalPoids <= 0f || plan.Quota <= 0)
                        {
                            phaseMixTexts[i].text = "-";
                        }
                        else
                        {
                            var parts = new List<string>();

                            for (int k = 0; k < mixEntries.Length; k++)
                            {
                                var m = mixEntries[k];
                                if (m == null || m.Poids <= 0f)
                                    continue;

                                string type = m.Type ?? string.Empty;
                                string letter = null;

                                switch (type)
                                {
                                    case "White": letter = "W"; break;
                                    case "Blue": letter = "B"; break;
                                    case "Red": letter = "R"; break;
                                    case "Black": letter = "V"; break;
                                }

                                if (letter != null)
                                {
                                    float ratio = m.Poids / totalPoids;
                                    int count = Mathf.RoundToInt(ratio * plan.Quota);

                                    if (count > 0)
                                        parts.Add(letter + count);
                                }
                            }

                            phaseMixTexts[i].text = (parts.Count > 0)
                                ? "Mix : " + string.Join(" / ", parts)
                                : "-";
                        }
                    }
                }
            }
        }

        // MAIN OBJECTIVE
        if (mainObjectiveText != null)
        {
            if (data.MainObjective != null && !string.IsNullOrEmpty(data.MainObjective.Text))
                mainObjectiveText.text = data.MainObjective.Text;
            else
                mainObjectiveText.text = "-";
        }

        // SECONDARY OBJECTIVES
        if (optionalDirectiveTexts != null)
        {
            for (int i = 0; i < optionalDirectiveTexts.Length; i++)
            {
                if (optionalDirectiveTexts[i] != null)
                    optionalDirectiveTexts[i].gameObject.SetActive(false);
            }

            if (data.SecondaryObjectives != null && data.SecondaryObjectives.Length > 0)
            {
                int count = Mathf.Min(data.SecondaryObjectives.Length, optionalDirectiveTexts.Length);

                for (int i = 0; i < count; i++)
                {
                    var so = data.SecondaryObjectives[i];

                    if (so != null && optionalDirectiveTexts[i] != null)
                    {
                        optionalDirectiveTexts[i].gameObject.SetActive(true);
                        optionalDirectiveTexts[i].text = so.UiText;
                    }
                }
            }
        }

        // SCORE TARGETS
        if (data.ScoreGoals != null && data.ScoreGoals.Length >= 3)
        {
            var fr = new CultureInfo("fr-FR");

            if (bronzeGoalText != null)
                bronzeGoalText.text = data.ScoreGoals[0].Points.ToString("N0", fr);
            if (silverGoalText != null)
                silverGoalText.text = data.ScoreGoals[1].Points.ToString("N0", fr);
            if (goldGoalText != null)
                goldGoalText.text = data.ScoreGoals[2].Points.ToString("N0", fr);
        }
        else
        {
            if (bronzeGoalText != null) bronzeGoalText.text = "-";
            if (silverGoalText != null) silverGoalText.text = "-";
            if (goldGoalText != null) goldGoalText.text = "-";
        }

        // SHIP INFO
        FillShipInfo();

        if (overlayIntro != null)
            overlayIntro.SetActive(true);
    }

    private void ResetPhaseBriefingPlaceholders()
    {
        if (phaseNameTexts != null)
        {
            for (int i = 0; i < phaseNameTexts.Length; i++)
                if (phaseNameTexts[i] != null) phaseNameTexts[i].text = "-";
        }

        if (phaseDurationTexts != null)
        {
            for (int i = 0; i < phaseDurationTexts.Length; i++)
                if (phaseDurationTexts[i] != null) phaseDurationTexts[i].text = "-";
        }

        if (phaseNodesTexts != null)
        {
            for (int i = 0; i < phaseNodesTexts.Length; i++)
                if (phaseNodesTexts[i] != null) phaseNodesTexts[i].text = "-";
        }

        if (phaseMixTexts != null)
        {
            for (int i = 0; i < phaseMixTexts.Length; i++)
                if (phaseMixTexts[i] != null) phaseMixTexts[i].text = "-";
        }

        if (phaseSpawnSpeedTexts != null)
        {
            for (int i = 0; i < phaseSpawnSpeedTexts.Length; i++)
                if (phaseSpawnSpeedTexts[i] != null) phaseSpawnSpeedTexts[i].text = "-";
        }
    }

    public void Hide()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);
    }

    private void OnPlayClicked()
    {
        onPlayCallback?.Invoke();
    }

    private void OnBackClicked()
    {
        onBackCallback?.Invoke();
    }

    private void FillShipInfo()
    {
        if (RunConfig.Instance == null || ShipCatalogService.Catalog == null)
            return;

        var catalog = ShipCatalogService.Catalog;
        string selectedId = RunConfig.Instance.SelectedShipId;
        var ship = catalog.ships.Find(s => s.id == selectedId);

        if (ship == null)
        {
            Debug.LogWarning("[IntroLevelUI] Ship not found: " + selectedId);
            return;
        }

        // IMAGE
        if (shipImage != null && !string.IsNullOrEmpty(ship.imageFile))
        {
            StartCoroutine(LoadSpriteFromStreamingAssets(ship.imageFile, shipImage));
        }

        // NOM
        if (shipNameText != null)
            shipNameText.text = ship.displayName;

        // HULL
        if (shipHullText != null)
        {
            if (runtimeHull >= 0 && runtimeMaxHull > 0)
            {
                shipHullText.text = runtimeHull.ToString() + " / " + runtimeMaxHull.ToString();
            }
            else
            {
                shipHullText.text = "x" + ship.maxHull;
            }
        }

        // SHIELD
        if (shipShieldText != null)
            shipShieldText.text = ship.shieldSecondsPerLevel.ToString("0") + "s";
    }

    private IEnumerator LoadSpriteFromStreamingAssets(string fileName, Image target)
    {
        if (target == null || string.IsNullOrEmpty(fileName))
            yield break;

        string url = Path.Combine(Application.streamingAssetsPath, "Ships/Images", fileName);

        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[IntroLevelUI] Failed to load texture: " + req.error + " (" + url + ")");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null)
                yield break;

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);

            target.sprite = sprite;
            target.preserveAspect = true;
        }
    }
}
