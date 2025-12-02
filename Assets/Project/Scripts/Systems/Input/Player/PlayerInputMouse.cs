using UnityEngine;

/// <summary>
/// Source d'input pour le paddle en mode PC (souris).
/// Convertit la position de la souris en position monde et
/// envoie la target X au PlayerController.
/// </summary>
public class PlayerInputMouse : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Camera mainCam;
    [SerializeField] private PlayerController player;

    [Header("Etat")]
    [SerializeField] private bool inputEnabled = true;


    private void Update()
    {
        if (Application.isMobilePlatform)
            return;

        if (!inputEnabled)
            return;

        if (player == null || mainCam == null)
            return;

        Vector3 screenPos = Input.mousePosition;

        float distance = Mathf.Abs(mainCam.transform.position.z - player.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, distance)
        );

        player.SetTargetXWorld(worldPos.x);
    }


    /// <summary>
    /// Active ou désactive la prise en compte de l'input souris.
    /// </summary>
    public void SetInputEnabled(bool state)
    {
        inputEnabled = state;
    }
}
