using UnityEngine;
using UnityEngine.UI;

public class PauseButtonUI : MonoBehaviour
{
    [SerializeField] private PauseController pauseController; // référence au manager
    [SerializeField] private Image icon;                       // l’image du bouton
    [SerializeField] private Sprite pauseSprite;               // icône “pause”
    [SerializeField] private Sprite resumeSprite;              // icône “play”

    private void Awake()
    {
        if (pauseController == null)
            pauseController = FindFirstObjectByType<PauseController>();
    }

    private void Update()
    {
        if (icon == null || pauseController == null) return;

        // On choisit le sprite selon l’état actuel
        icon.sprite = pauseController.IsPaused ? resumeSprite : pauseSprite;
    }
}
