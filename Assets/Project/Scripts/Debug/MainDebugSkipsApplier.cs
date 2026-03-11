using UnityEngine;

/// <summary>
/// Applique les options de debug "skip briefing / skip intro" aux controllers adequats,
/// sans polluer LevelManager.
/// </summary>
public class MainDebugSkipsApplier : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private LevelBriefingController briefingController;
    [SerializeField] private LevelIntroSequenceController introSequenceController;

    public void ApplySkips(bool skipBriefing, bool skipIntro)
    {
        if (briefingController != null)
            briefingController.SetDebugSkip(skipBriefing);

        if (introSequenceController != null)
            introSequenceController.SetDebugSkip(skipIntro);
    }
}
