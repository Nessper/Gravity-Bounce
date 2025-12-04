using UnityEngine;

/// <summary>
/// Définition d'un personnage de l'équipage ou d'un interlocuteur (ex: opérateur).
/// Les dialogues font référence à ce personnage via speakerId.
/// Ce ScriptableObject contient les données visuelles et sonores de base.
/// </summary>
[CreateAssetMenu(menuName = "Narration/Crew Character", fileName = "NewCrewCharacter")]
public class CrewCharacter : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Identifiant logique utilisé dans le JSON (ex: 'captain', 'mike', 'leigh', 'rye', 'operator').")]
    public string id;

    [Tooltip("Nom affiché dans l'UI (ex: 'Cal Rydell', 'Mike', 'Leigh', 'Rye', 'Operator').")]
    public string displayName;

    [Header("Visuel")]
    [Tooltip("Portrait du personnage affiché dans la fenêtre de dialogue.")]
    public Sprite portrait;

    [Tooltip("Couleur associée au personnage dans l'UI (fond, contour, etc.).")]
    public Color uiColor = Color.white;

    [Header("Audio")]
    [Tooltip("Clip audio joué au début d'une réplique (bip, bruit radio, scroll, etc.).")]
    public AudioClip dialogClip;

    [Tooltip("Pitch appliqué au son du dialogue (1 = normal).")]
    [Range(0.5f, 2.0f)]
    public float pitch = 1.0f;

    [Tooltip("Volume relatif du son du dialogue (1 = normal).")]
    [Range(0.0f, 1.0f)]
    public float volume = 1.0f;
}
