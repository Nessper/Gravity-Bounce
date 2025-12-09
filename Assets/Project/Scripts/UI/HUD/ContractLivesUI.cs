using UnityEngine;
using UnityEngine.UI;

public class ContractLivesUI : MonoBehaviour
{
    [Header("LEDs")]
    [SerializeField] private Image led1;
    [SerializeField] private Image led2;
    [SerializeField] private Image led3;

    [Header("Sprites")]
    [SerializeField] private Sprite ledGreen;
    [SerializeField] private Sprite ledRed;

    /// <summary>
    /// Met à jour les LEDs en fonction des vies de contrat restantes.
    /// 3 -> G G G
    /// 2 -> R G G
    /// 1 -> R R G
    /// 0 -> R R R
    /// </summary>
    public void SetContractLives(int contractLives)
    {
        contractLives = Mathf.Clamp(contractLives, 0, 3);

        switch (contractLives)
        {
            case 3:
                SetLeds(ledGreen, ledGreen, ledGreen);
                break;
            case 2:
                SetLeds(ledRed, ledGreen, ledGreen);
                break;
            case 1:
                SetLeds(ledRed, ledRed, ledGreen);
                break;
            case 0:
                SetLeds(ledRed, ledRed, ledRed);
                break;
        }
    }

    private void SetLeds(Sprite s1, Sprite s2, Sprite s3)
    {
        if (led1 != null) led1.sprite = s1;
        if (led2 != null) led2.sprite = s2;
        if (led3 != null) led3.sprite = s3;
    }
}
