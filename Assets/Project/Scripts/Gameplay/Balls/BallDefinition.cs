using UnityEngine;

[CreateAssetMenu(menuName = "404/Balls/Ball Definition")]
public class BallDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id;
    [SerializeField] private string displayNameLocKey;

    [Header("Gameplay")]
    [SerializeField] private int basePoints;
    [SerializeField] private bool isDanger;
    [SerializeField] private bool countsForProgress = true;

    [Header("Visuals")]
    [SerializeField] private Material material;
    [SerializeField] private Color scoreColor = Color.white;

    public string Id => id;
    public string DisplayNameLocKey => displayNameLocKey;

    public int BasePoints => basePoints;
    public bool IsDanger => isDanger;
    public bool CountsForProgress => countsForProgress;

    public Material Material => material;
    public Color ScoreColor => scoreColor;
}