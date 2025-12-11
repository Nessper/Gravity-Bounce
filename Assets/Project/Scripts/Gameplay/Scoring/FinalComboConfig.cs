using UnityEngine;

[CreateAssetMenu(
    fileName = "FinalComboConfig",
    menuName = "VoidScrappers/Combos/FinalComboConfig")]
public class FinalComboConfig : ScriptableObject
{
    [Header("Noir / Perfect run")]
    public int ptsNoBlackCollected = 400;
    public int ptsPerfectRun = 700;

    [Header("Volume / Diversite / Chains")]
    public int ptsCombosCollector = 200;
    public int ptsMaxChainBonus = 150;
    public int ptsComboDiversity = 250;
    public int ptsColorTrinity = 200;
    public int ptsChainDuo = 400;

    // --------------------------------------------------------------------
    // TIMING OBJECTIF PRINCIPAL (FAST / CLUTCH FINISHER)
    // --------------------------------------------------------------------
    [Header("Timing objectif principal")]
    [Tooltip("Marge minimale avant la fin du timer (en secondes) pour FastFinisher.")]
    public float fastFinisherMarginSec = 10f;
    [Tooltip("Points accordes pour FastFinisher.")]
    public int fastFinisherPoints = 250;
    [Tooltip("Marge maximale (en secondes) avant la fin du timer pour ClutchFinisher.")]
    public float clutchFinisherMarginSec = 3f;
    [Tooltip("Points accordes pour ClutchFinisher.")]
    public int clutchFinisherPoints = 100;

}
