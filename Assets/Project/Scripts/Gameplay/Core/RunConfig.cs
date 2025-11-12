using UnityEngine;

public class RunConfig : MonoBehaviour
{
    public static RunConfig Instance { get; private set; }

    // Valeur par défaut si le joueur n’a pas encore choisi
    public string SelectedShipId = "CORE_SCOUT";
    public bool SkipTitleIntroOnce = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
