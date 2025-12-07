using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;


/// <summary>
/// UI d’intro du niveau (nouvelle version).
/// STEP 1 : Câblage du Header (ID + World + titre)
///          + Score Targets (Bronze / Silver / Gold)
/// STEP 2 (partiel) : Préparation de la structure du Level Briefing (Phases),
///                    avec remplissage minimal de Name + SpawnSpeed.
///                    Duration / Nodes / Mix seront câblés plus tard.
/// </summary>
public class IntroLevelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject overlayIntro;

    // --------------------------------------------------------------------
    // HEADER
    // --------------------------------------------------------------------

    [Header("Header")]
    [SerializeField] private TMP_Text levelIdText;       // ID brut du niveau (ex: W1-L1) en haut à gauche
    [SerializeField] private TMP_Text worldLevelText;    // World (ex: "World 1")
    [SerializeField] private TMP_Text titleText;         // Titre du niveau

    // --------------------------------------------------------------------
    // LEVEL BRIEFING - PHASES (3 phases x 5 champs)
    // --------------------------------------------------------------------
    // Index convention:
    // 0 = Phase 1, 1 = Phase 2, 2 = Phase 3
    // On se contente pour l’instant de remplir:
    // - phaseNameTexts[i]     <- data.Phases[i].Name
    // - phaseSpawnSpeedTexts[i] <- data.Phases[i].Intervalle
    // Les autres champs seront câblés dans une étape ultérieure.

    [Header("Level Briefing - Phases")]
    [SerializeField] private TMP_Text[] phaseNameTexts;        // Phase_name
    [SerializeField] private TMP_Text[] phaseDurationTexts;    // Duration (à calculer plus tard)
    [SerializeField] private TMP_Text[] phaseNodesTexts;       // Nodes (à calculer plus tard)
    [SerializeField] private TMP_Text[] phaseMixTexts;         // Mix (à calculer plus tard)
    [SerializeField] private TMP_Text[] phaseSpawnSpeedTexts;  // SpawnSpeed (Intervalle du JSON)

    // --------------------------------------------------------------------
    // MAIN OBJECTIVE (Mission Priority)
    // --------------------------------------------------------------------
    [Header("Main Objective")]
    [SerializeField] private TMP_Text mainObjectiveText;

    // --------------------------------------------------------------------
    // OPTIONAL DIRECTIVES (Secondary Objectives)
    // --------------------------------------------------------------------
    [Header("Optional Directives")]
    [SerializeField] private TMP_Text[] optionalDirectiveTexts;   // size = 3 max

    // --------------------------------------------------------------------
    // SCORE TARGETS (Bronze / Silver / Gold)
    // --------------------------------------------------------------------

    [Header("Score Targets")]
    [SerializeField] private TMP_Text bronzeGoalText;
    [SerializeField] private TMP_Text silverGoalText;
    [SerializeField] private TMP_Text goldGoalText;

    // --------------------------------------------------------------------
    // SPACESHIP (Selected Ship)
    // --------------------------------------------------------------------
    [Header("Ship Info")]
    [SerializeField] private Image shipImage;
    [SerializeField] private TMP_Text shipNameText;
    [SerializeField] private TMP_Text shipLivesText;
    [SerializeField] private TMP_Text shipShieldText;

    // --------------------------------------------------------------------
    // BOUTONS
    // --------------------------------------------------------------------

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;

    private System.Action onPlayCallback;
    private System.Action onBackCallback;

    private void Awake()
    {
        if (overlayIntro != null)
            overlayIntro.SetActive(false);

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    /// <summary>
    /// Affiche l’intro du niveau.
    /// - data : LevelData brut (titre, objectifs, etc.)
    /// - phasePlans : plan de phases calculé par le BallSpawner (durée, quota, interval, nom)
    /// </summary>
    public void Show(LevelData data, PhasePlanInfo[] phasePlans, System.Action onPlay, System.Action onBack)
    {
        if (data == null)
            return;

        onPlayCallback = onPlay;
        onBackCallback = onBack;

        // --------------------------------------------------------------------
        // HEADER : ID + World + Titre du niveau
        // --------------------------------------------------------------------

        if (levelIdText != null)
            levelIdText.text = data.LevelID;

        if (worldLevelText != null)
        {
            string world = string.IsNullOrEmpty(data.World) ? "World ?" : data.World;
            worldLevelText.text = world;
        }

        if (titleText != null)
            titleText.text = data.Title;

        // --------------------------------------------------------------------
        // LEVEL BRIEFING - PHASES (remplissage à partir du plan du BallSpawner)
        // --------------------------------------------------------------------
        // On part d'abord sur des placeholders "-", puis on remplit ce qu'on peut
        // en fonction du nombre réel de phases et du plan.
        ResetPhaseBriefingPlaceholders();

        if (data.Phases != null && data.Phases.Length > 0 &&
            phasePlans != null && phasePlans.Length > 0)
        {
            // Nombre maximum de lignes qu'on peut afficher proprement :
            // limité par le JSON, le plan, et les tableaux de TMP.
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

                // Nom de phase
                if (phaseNameTexts != null && phaseNameTexts[i] != null)
                {
                    phaseNameTexts[i].text = plan.Name;
                }

                // Durée (arrondie en secondes)
                if (phaseDurationTexts != null && phaseDurationTexts[i] != null)
                {
                    int rounded = Mathf.RoundToInt(plan.DurationSec);
                    phaseDurationTexts[i].text = "Duration : " + rounded.ToString() + "s";
                }

                // Nodes (quota de billes prévues)
                if (phaseNodesTexts != null && phaseNodesTexts[i] != null)
                {
                    phaseNodesTexts[i].text = "Nodes : " + plan.Quota.ToString();
                }

                // Spawn Interval (vitesse d’apparition)
                if (phaseSpawnSpeedTexts != null && phaseSpawnSpeedTexts[i] != null)
                {
                    string intervalText = "Spawn interval : " + plan.IntervalSec.ToString("0.0", fr) + "s";
                    phaseSpawnSpeedTexts[i].text = intervalText;
                }

                // -------------------------------
                // MIX : W27 / B3 / R2 / V5 (counts, pas pourcentages)
                // -------------------------------
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
                        // 1) Somme des poids valides (même logique que pour les pourcentages)
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
                            List<string> parts = new List<string>();

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
                                    case "Black": letter = "V"; break; // Void
                                }

                                if (letter != null)
                                {
                                    // Ratio de ce type dans le mix
                                    float ratio = m.Poids / totalPoids;

                                    // Nombre de billes de ce type prévu dans la phase
                                    int count = Mathf.RoundToInt(ratio * plan.Quota);

                                    // On n’affiche que les types réellement présents
                                    if (count > 0)
                                    {
                                        parts.Add(letter + count);
                                    }
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
        else
        {
            // Si pas de phases ou pas de plan, on garde juste les "-" mis par ResetPhaseBriefingPlaceholders.
        }



        // --------------------------------------------------------------------
        // MAIN OBJECTIVE : texte issu du JSON (MainObjective.Text)
        // --------------------------------------------------------------------
        if (mainObjectiveText != null)
        {
            if (data.MainObjective != null && !string.IsNullOrEmpty(data.MainObjective.Text))
            {
                // Affiche le texte de l'objectif principal tel quel
                mainObjectiveText.text = data.MainObjective.Text;
            }
            else
            {
                // Fallback propre si pas d'objectif défini dans le JSON
                mainObjectiveText.text = "-";
            }
        }

        //----------------------------------------------
        // OPTIONAL DIRECTIVES (Secondary Objectives)
        //----------------------------------------------
        if (optionalDirectiveTexts != null)
        {
            // Tout désactiver au début
            for (int i = 0; i < optionalDirectiveTexts.Length; i++)
            {
                if (optionalDirectiveTexts[i] != null)
                    optionalDirectiveTexts[i].gameObject.SetActive(false);
            }

            // Si pas d'objectifs secondaires -> rien à afficher
            if (data.SecondaryObjectives == null || data.SecondaryObjectives.Length == 0)
                return;

            int count = Mathf.Min(data.SecondaryObjectives.Length, optionalDirectiveTexts.Length);

            for (int i = 0; i < count; i++)
            {
                var so = data.SecondaryObjectives[i];

                if (so != null && optionalDirectiveTexts[i] != null)
                {
                    // On active la ligne correspondante
                    optionalDirectiveTexts[i].gameObject.SetActive(true);

                    // On affiche UiText (la version "joueur")
                    optionalDirectiveTexts[i].text = so.UiText;
                }
            }
        }

        // --------------------------------------------------------------------
        // SCORE TARGETS : Bronze / Silver / Gold
        // --------------------------------------------------------------------

        if (data.ScoreGoals != null && data.ScoreGoals.Length >= 3)
        {
            var fr = new CultureInfo("fr-FR");

            // Bronze
            if (bronzeGoalText != null)
                bronzeGoalText.text = data.ScoreGoals[0].Points.ToString("N0", fr);

            // Silver
            if (silverGoalText != null)
                silverGoalText.text = data.ScoreGoals[1].Points.ToString("N0", fr);

            // Gold
            if (goldGoalText != null)
                goldGoalText.text = data.ScoreGoals[2].Points.ToString("N0", fr);
        }
        else
        {
            // JSON incomplet -> fallback pour éviter des cases vides
            if (bronzeGoalText != null) bronzeGoalText.text = "-";
            if (silverGoalText != null) silverGoalText.text = "-";
            if (goldGoalText != null) goldGoalText.text = "-";
        }

        // --------------------------------------------------------------------
        // SPACESHIP INFO (depuis RunConfig -> ShipCatalog)
        // --------------------------------------------------------------------
        FillShipInfo();

        // --------------------------------------------------------------------
        // FINAL : Active l’overlay
        // --------------------------------------------------------------------

        if (overlayIntro != null)
            overlayIntro.SetActive(true);
    }

    /// <summary>
    /// Remet des placeholders sur les lignes de phases
    /// si le JSON ne définit aucune phase.
    /// </summary>
    private void ResetPhaseBriefingPlaceholders()
    {
        if (phaseNameTexts != null)
        {
            for (int i = 0; i < phaseNameTexts.Length; i++)
            {
                if (phaseNameTexts[i] != null)
                    phaseNameTexts[i].text = "-";
            }
        }

        if (phaseDurationTexts != null)
        {
            for (int i = 0; i < phaseDurationTexts.Length; i++)
            {
                if (phaseDurationTexts[i] != null)
                    phaseDurationTexts[i].text = "-";
            }
        }

        if (phaseNodesTexts != null)
        {
            for (int i = 0; i < phaseNodesTexts.Length; i++)
            {
                if (phaseNodesTexts[i] != null)
                    phaseNodesTexts[i].text = "-";
            }
        }

        if (phaseMixTexts != null)
        {
            for (int i = 0; i < phaseMixTexts.Length; i++)
            {
                if (phaseMixTexts[i] != null)
                    phaseMixTexts[i].text = "-";
            }
        }

        if (phaseSpawnSpeedTexts != null)
        {
            for (int i = 0; i < phaseSpawnSpeedTexts.Length; i++)
            {
                if (phaseSpawnSpeedTexts[i] != null)
                    phaseSpawnSpeedTexts[i].text = "-";
            }
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
        // Sécurité
        if (RunConfig.Instance == null || ShipCatalogService.Catalog == null)
            return;

        var catalog = ShipCatalogService.Catalog;

        // ID du vaisseau choisi
        string selectedId = RunConfig.Instance.SelectedShipId;

        // On récupère le ShipDefinition correspondant
        var ship = catalog.ships.Find(s => s.id == selectedId);

        if (ship == null)
        {
            Debug.LogWarning("[IntroLevelUI] Ship not found: " + selectedId);
            return;
        }
        // -------------------------------------------------------------
        // IMAGE (chargée depuis StreamingAssets, même logique que ShipSelectController)
        // -------------------------------------------------------------
        if (shipImage != null && !string.IsNullOrEmpty(ship.imageFile))
        {
            // On réutilise la même approche que dans ShipSelectController :
            // le fichier est attendu dans StreamingAssets/Ships/Images/<fileName>
            StartCoroutine(LoadSpriteFromStreamingAssets(ship.imageFile, shipImage));
        }


        // -------------------------------------------------------------
        // NOM
        // -------------------------------------------------------------
        if (shipNameText != null)
            shipNameText.text = ship.displayName;

        // -------------------------------------------------------------
        // LIVES
        // -------------------------------------------------------------
        if (shipLivesText != null)
            shipLivesText.text = "x" + ship.maxHull;

        // -------------------------------------------------------------
        // SHIELD (durée de protection par niveau)
        // -------------------------------------------------------------
        if (shipShieldText != null)
        {
            // Format simple : "60 s"
            shipShieldText.text = ship.shieldSecondsPerLevel.ToString("0") + "s";
        }
    }

    /// <summary>
    /// Charge une texture depuis StreamingAssets/Ships/Images et la convertit en Sprite pour l'UI.
    /// Même logique que dans ShipSelectController, pour rester cohérent.
    /// </summary>
    private IEnumerator LoadSpriteFromStreamingAssets(string fileName, Image target)
    {
        if (target == null || string.IsNullOrEmpty(fileName))
            yield break;

        // Construction de l'URL, identique au ShipSelectController
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
