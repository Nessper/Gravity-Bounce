using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; 

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
    [SerializeField] private TMP_Text Lives_Txt;
    [SerializeField] private TMP_Text Shield_Txt;
    [SerializeField] private TMP_Text PaddleWidth_Txt;

    [Header("Config")]
    [SerializeField] private string mainSceneName = "Main";

    private int index = 0;

    private void Awake()
    {
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

        // Wiring
        if (Start_Button) Start_Button.onClick.AddListener(OnStart);
        if (Back_Button) Back_Button.onClick.AddListener(OnBack);
        if (Previous_Button) Previous_Button.onClick.AddListener(OnPrev);
        if (Next_Button) Next_Button.onClick.AddListener(OnNext);

        // Affiche le premier vaisseau
        index = 0;
        RefreshUI();
    }

    private void OnPrev()
    {
        int count = ShipCatalogService.Catalog.ships.Count;
        index = (index - 1 + count) % count;
        RefreshUI();
    }

    private void OnNext()
    {
        int count = ShipCatalogService.Catalog.ships.Count;
        index = (index + 1) % count;
        RefreshUI();
    }

    private void OnBack()
    {
        if (RunConfig.Instance != null)
            RunConfig.Instance.SkipTitleIntroOnce = true;
        SceneManager.LoadScene("Title");
    }

    private void OnStart()
    {
        var ship = ShipCatalogService.Catalog.ships[index];
        if (RunConfig.Instance != null)
            RunConfig.Instance.SelectedShipId = ship.id;

        StartCoroutine(StartAfterMusicFade());
    }

    private IEnumerator StartAfterMusicFade()
    {
        if (TitleMusicPlayer.Instance != null)
            yield return TitleMusicPlayer.Instance.FadeOut();

        SceneManager.LoadScene(mainSceneName);
    }

    private void RefreshUI()
    {
        var ship = ShipCatalogService.Catalog.ships[index];

        if (ShipName_Txt) ShipName_Txt.text = ship.displayName;
        if (Description_Txt) Description_Txt.text = ship.description;
        if (Lives_Txt) Lives_Txt.text = "x"+ship.lives;
        if (Shield_Txt) Shield_Txt.text = ship.shieldSecondsPerLevel.ToString();
        if (PaddleWidth_Txt) PaddleWidth_Txt.text = ship.paddleWidthMult.ToString();

        // Charge l'image depuis StreamingAssets
        if (!string.IsNullOrEmpty(ship.imageFile) && Ship_Img != null)
            StartCoroutine(LoadSpriteFromStreamingAssets(ship.imageFile, Ship_Img));
    }

    private IEnumerator LoadSpriteFromStreamingAssets(string fileName, Image target)
    {
        string url = System.IO.Path.Combine(Application.streamingAssetsPath, "Ships/Images", fileName);

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

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            target.sprite = sprite;
            target.preserveAspect = true;
        }
    }

}
