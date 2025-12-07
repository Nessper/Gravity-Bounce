using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Contrôle l'écran de sélection de vaisseau : navigation entre les vaisseaux,
/// affichage des stats, choix du vaisseau et lancement de la scène principale.
/// </summary>
public class ShipSelectController : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text ShipName_Txt;
    [SerializeField] private Image Ship_Img;
    [SerializeField] private Button Start_Button;
    [SerializeField] private Button Back_Button;
    [SerializeField] private Button Previous_Button;
    [SerializeField] private Button Next_Button;
    [SerializeField] private TMP_Text Description_Txt;
    [SerializeField] private TMP_Text Hull_Txt;
    [SerializeField] private TMP_Text Shield_Txt;
    [SerializeField] private TMP_Text PaddleWidth_Txt;

    [Header("Config")]
    [SerializeField] private string mainSceneName = "Main";

    /// <summary>
    /// Index du vaisseau actuellement sélectionné dans le catalogue.
    /// </summary>
    private int index = 0;

    private void Awake()
    {
        // Sécurité : vérifie que le catalogue de vaisseaux est bien chargé et non vide.
        if (ShipCatalogService.Catalog == null || ShipCatalogService.Catalog.ships == null || ShipCatalogService.Catalog.ships.Count == 0)
        {
            Debug.LogError("[ShipSelect] Ship catalog not loaded or empty.");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Assure un volume plein sur l'écran ShipSelect (pas de fade entre Title et ShipSelect)
        if (TitleMusicPlayer.Instance != null)
            TitleMusicPlayer.Instance.SnapToTargetVolume();

        // Wiring des boutons vers les callbacks
        if (Start_Button) Start_Button.onClick.AddListener(OnStart);
        if (Back_Button) Back_Button.onClick.AddListener(OnBack);
        if (Previous_Button) Previous_Button.onClick.AddListener(OnPrev);
        if (Next_Button) Next_Button.onClick.AddListener(OnNext);

        // Récupère le vaisseau sauvegardé (persistance via RunConfig / SaveManager)
        string savedId = RunConfig.Instance != null
            ? RunConfig.Instance.SelectedShipId
            : "CORE_SCOUT";

        // Trouve l'index correspondant dans le catalogue
        var ships = ShipCatalogService.Catalog.ships;
        int found = ships.FindIndex(s => s.id == savedId);

        // Si on trouve le vaisseau sauvegardé, on se positionne dessus, sinon on tombe sur le premier
        index = (found >= 0) ? found : 0;

        // Met à jour l'affichage pour le vaisseau courant
        RefreshUI();
    }

    /// <summary>
    /// Callback pour le bouton de sélection du vaisseau précédent.
    /// Tourne dans la liste avec wrap-around.
    /// </summary>
    private void OnPrev()
    {
        int count = ShipCatalogService.Catalog.ships.Count;
        index = (index - 1 + count) % count;
        RefreshUI();
    }

    /// <summary>
    /// Callback pour le bouton de sélection du vaisseau suivant.
    /// Tourne dans la liste avec wrap-around.
    /// </summary>
    private void OnNext()
    {
        int count = ShipCatalogService.Catalog.ships.Count;
        index = (index + 1) % count;
        RefreshUI();
    }

    /// <summary>
    /// Retour à l'écran Title.
    /// Marque SkipTitleIntroOnce pour ne pas rejouer l'intro immédiatement.
    /// </summary>
    private void OnBack()
    {
        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = true;

        SceneManager.LoadScene("Title");
    }

    /// <summary>
    /// Callback du bouton Start.
    /// 1) Mémorise le vaisseau choisi (RunConfig + sauvegarde persistante),
    /// 2) Initialise une nouvelle "run" dans GameSaveData.runState,
    /// 3) Lance la scène principale après le fade-out de la musique.
    /// </summary>
    private void OnStart()
    {
        // Vaisseau actuellement sélectionné dans le catalogue
        var ship = ShipCatalogService.Catalog.ships[index];

        // 1) Met à jour la config de run ET la sauvegarde persistante (selectedShipId + unlockedShips)
        if (RunConfig.Instance != null)
        {
            RunConfig.Instance.SetSelectedShip(ship.id);
        }

        // 2) Initialise l'état de la campagne (run) dans la sauvegarde persistante
        if (SaveManager.Instance != null && SaveManager.Instance.Current != null)
        {
            GameSaveData save = SaveManager.Instance.Current;

            // Sécurité : si runState est null, on en crée un
            if (save.runState == null)
            {
                save.runState = new RunStateData();
            }

            RunStateData run = save.runState;

            // Nouvelle run : on marque qu'il y a un run en cours
            run.hasOngoingRun = true;

            // Vaisseau utilisé pour cette run
            run.currentShipId = ship.id;

            // Pour l'instant : monde 1, niveau index 0 en dur
            run.currentWorld = 1;
            run.currentLevelIndex = 0;

            // Identifiant exact du niveau à jouer (LevelID du JSON)
            run.currentLevelId = "W1-L1";

            // Vies de la campagne = vies de base du vaisseau
            run.remainingHullInRun = Mathf.Max(0, ship.maxHull);

            // Score de run remis à zéro
            run.currentRunScore = 0;

            // Aucun niveau encore terminé dans ce run
            run.levelsClearedInRun = 0;

            // Au moment où on quitte ShipSelect, aucun level n'est encore en cours
            run.levelInProgress = false;

            // On sauvegarde immédiatement l'état de la run
            SaveManager.Instance.Save();
        }

        // 3) Lance la scène principale après un fade-out de la musique de titre
        StartCoroutine(StartAfterMusicFade());
    }


    /// <summary>
    /// Coroutine qui attend le fade-out de la musique de titre
    /// avant de charger la scène principale.
    /// </summary>
    private IEnumerator StartAfterMusicFade()
    {
        if (TitleMusicPlayer.Instance != null)
            yield return TitleMusicPlayer.Instance.FadeOut();

        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>
    /// Met à jour tous les éléments UI pour refléter le vaisseau actuellement sélectionné.
    /// </summary>
    private void RefreshUI()
    {
        var ship = ShipCatalogService.Catalog.ships[index];

        if (ShipName_Txt) ShipName_Txt.text = ship.displayName;
        if (Description_Txt) Description_Txt.text = ship.description;
        if (Hull_Txt) Hull_Txt.text = "x" + ship.maxHull;
        if (Shield_Txt) Shield_Txt.text = ship.shieldSecondsPerLevel.ToString();
        if (PaddleWidth_Txt) PaddleWidth_Txt.text = ship.paddleWidthMult.ToString();

        // Charge l'image depuis StreamingAssets
        if (!string.IsNullOrEmpty(ship.imageFile) && Ship_Img != null)
            StartCoroutine(LoadSpriteFromStreamingAssets(ship.imageFile, Ship_Img));
    }

    /// <summary>
    /// Charge une texture depuis StreamingAssets et la convertit en Sprite pour l'UI.
    /// </summary>
    private IEnumerator LoadSpriteFromStreamingAssets(string fileName, Image target)
    {
        string url = Path.Combine(Application.streamingAssetsPath, "Ships/Images", fileName);

        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[ShipSelect] Failed to load texture: " + req.error + " (" + url + ")");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null) yield break;

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
