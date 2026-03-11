using UnityEngine;

[CreateAssetMenu(fileName = "EconomyConfig", menuName = "Game/Economy Config")]
public class EconomyConfig : ScriptableObject
{
    [Header("Money reward per medal")]
    public int bronzeReward = 1;
    public int silverReward = 2;
    public int goldReward = 3;

    public int GetMoneyReward(EndMedal medal)
    {
        switch (medal)
        {
            case EndMedal.Bronze:
                return Mathf.Max(0, bronzeReward);

            case EndMedal.Silver:
                return Mathf.Max(0, silverReward);

            case EndMedal.Gold:
                return Mathf.Max(0, goldReward);

            default:
                return 0;
        }
    }
}
