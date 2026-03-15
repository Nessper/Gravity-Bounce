using UnityEngine;

/// <summary>
/// Source d'input clavier pour la fermeture des bacs.
/// Utilise Space en mode maintien.
/// Ne tourne pas sur mobile.
/// </summary>
public class CloseBinInputKeyboard : MonoBehaviour
{
    [SerializeField] private CloseBinController closeBin;

    [Header("Options")]
    [SerializeField] private bool inputEnabled = true;

    private void Update()
    {
        // On évite de traiter le clavier sur mobile
        if (Application.isMobilePlatform)
            return;

        if (!inputEnabled || closeBin == null)
            return;

        bool closePressed = Input.GetKey(KeyCode.Space);

        closeBin.SetClosedFromInput(closePressed);
    }

    /// <summary>
    /// Permet de désactiver la prise en compte de l'input clavier (pause, fin de niveau...).
    /// </summary>
    public void SetInputEnabled(bool state)
    {
        inputEnabled = state;
    }
}