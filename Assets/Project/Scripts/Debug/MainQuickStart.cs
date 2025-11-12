using UnityEngine;

public class MainQuickStart : MonoBehaviour
{
    [Header("QuickStart (si ce composant est actif)")]
    [Tooltip("ID du vaisseau à utiliser. Laisse vide pour garder la valeur courante de RunConfig.")]
    public string forcedShipId = "CORE_SCOUT";

    [Tooltip("Vies à forcer (>0). Mets 0 pour laisser la valeur du vaisseau.")]
    public int forcedLives = 0;

    [Tooltip("Timer à forcer en secondes (>0). Mets 0 pour laisser la valeur du vaisseau.")]
    public float forcedTimerSec = 0f;

    [Header("Sécurité build")]
    [SerializeField, Tooltip("Désactive automatiquement le QuickStart en build (hors éditeur).")]
    private bool disableInBuild = true;

    private void Awake()
    {
        // Si on n'est pas dans l'éditeur et que l'option est cochée, on désactive ce composant.
        if (disableInBuild && !Application.isEditor)
            enabled = false;
    }
}