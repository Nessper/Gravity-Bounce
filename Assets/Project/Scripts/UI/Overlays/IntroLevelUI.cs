using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Ecran d intro de niveau (briefing).
/// - Affiche les informations du niveau (id, titre, monde).
/// - Affiche le detail des phases (duree, nodes, mix, vitesse de spawn).
/// - Affiche l objectif principal et les objectifs secondaires.
/// - Affiche les objectifs de score (bronze, argent, or).
/// - Affiche les infos du vaisseau (image, nom, hull, shield).
/// - Expose deux callbacks:
///   - Start (bouton "Start") -> demarrage du niveau.
///   - Menu (bouton "Menu") -> retour vers le menu titre (gere par un autre script).
/// Ce script ne fait aucun changement de scene, il ne fait que l affichage.
/// </summary>
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
    [SerializeField] private Button startButton;  // bouton "Start"
    [SerializeField] private Button menuButton;   // bouton "Menu" (retour vers Title)

    /// <summary>
    /// Callback appele quand le joueur clique sur Start.
    /// </summary>
    private System.Action onStartCallback;

    /// <summary>
    /// Callback appele quand le joueur clique sur Menu.
    /// </summary>
    private System.Action onMenuCallback;

    /// <summary>
    /// Valeur runtime du hull (injectee par l orchestrateur du niveau).
    /// Permet d afficher la valeur courante dans le briefing.
    /// </summary>
    private int runtimeHull = -1;

    /// <summary>
    /// Valeur runtime du hull max (injectee par l orchestrateur du niveau).
    /// </summary>
    private int runtimeMaxHull = -1;

    private void Awake()
    {
        // L overlay d intro est cache par defaut.
        if (overlayIntro != null)
            overlayIntro.SetActive(false);

        // Les boutons sont cables dans l inspector vers OnStartClicked et OnMenuClicked.
        // Ce script ne fait pas de AddListener en code.
    }

    /// <summary>
    /// Met a jour les valeurs runtime de hull, utilisees pour afficher "hull courant / hull max"
    /// dans l UI du vaisseau.
    /// </summary>
    public void SetShipRuntimeHull(int currentHull, int maxHull)
    {
        runtimeHull = Mathf.Max(-1, currentHull);
        runtimeMaxHull = Mathf.Max(-1, maxHull);

        // Si l overlay est deja visible, on met a jour le texte immediatement.
        if (shipHullText != null && overlayIntro != null && overlayIntro.activeInHierarchy)
        {
            if (runtimeHull >= 0 && runtimeMaxHull > 0)
            {
                shipHullText.text = runtimeHull.ToString() + "/" + runtimeMaxHull.ToString();
            }
            else
            {
                shipHullText.text = "x" + runtimeMaxHull.ToString();
            }
        }
    }

    /// <summary>
    /// Affiche le briefing avec les donnees de niveau, les phases et les callbacks.
    /// - onStart est appele quand le joueur clique sur le bouton Start.
    /// - onMenu est appele quand le joueur clique sur le bouton Menu.
    /// </summary>
    public void Show(LevelData data, PhasePlanInfo[] phasePlans, System.Action onStart, System.Action onMenu)
    {
        if (data == null)
            return;

        onStartCallback = onStart;
        onMenuCallback = onMenu;

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

        // OBJECTIF PRINCIPAL
        if (mainObjectiveText != null)
        {
            if (data.MainObjective != null && !string.IsNullOrEmpty(data.MainObjective.Text))
                mainObjectiveText.text = data.MainObjective.Text;
            else
                mainObjectiveText.text = "-";
        }

        // OBJECTIFS SECONDAIRES
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

        // INFOS VAISSEAU
        FillShipInfo();

        if (overlayIntro != null)
            overlayIntro.SetActive(true);
    }

    /// <summary>
    /// Remet les zones de texte des phases avec des placeholders "-".
    /// </summary>
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

    /// <summary>
    /// Cache l overlay de briefing.
    /// </summary>
    public void Hide()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);
    }

    /// <summary>
    /// Callback appele par le bouton Start (via l inspector).
    /// </summary>
    public void OnStartClicked()
    {
        Debug.Log("[IntroLevelUI] OnStartClicked");
        onStartCallback?.Invoke();
    }

    /// <summary>
    /// Callback appele par le bouton Menu (via l inspector).
    /// </summary>
    public void OnMenuClicked()
    {
        Debug.Log("[IntroLevelUI] OnMenuClicked");
        onMenuCallback?.Invoke();
    }

    /// <summary>
    /// Remplit les infos du vaisseau (image, nom, hull, shield) a partir de RunConfig et du ShipCatalog.
    /// </summary>
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

    /// <summary>
    /// Charge une texture depuis StreamingAssets et la convertit en Sprite pour l UI.
    /// </summary>
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
