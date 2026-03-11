using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ajoute un SFX a un bouton UI via BootRoot.Audio.
/// Usage: mettre ce script sur le GameObject qui porte le Button.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSfx : MonoBehaviour
{
    [SerializeField] private SfxId sfx = SfxId.UiClick;

    private void Awake()
    {
        Button btn = GetComponent<Button>();
        btn.onClick.AddListener(Play);
    }

    private void Play()
    {
        if (BootRoot.Audio == null)
        {
            Debug.LogWarning("[UIButtonSfx] BootRoot.Audio est null. AudioManager pas initialise ?");
            return;
        }

        BootRoot.Audio.PlayUi(sfx);
    }
}
