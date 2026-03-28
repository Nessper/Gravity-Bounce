using System.Collections;
using UnityEngine;

/// <summary>
/// Gère les bonus de modules appliqués
/// après la fin réelle du gameplay, mais avant la cérémonie de fin.
///
/// Rythme voulu :
/// 1. H module (toast + sfx module)
/// 2. H stat (feedback hull + sfx stat)
/// 3. le toast reste un peu pendant/juste après l'anim hull
/// 4. C module (toast + sfx module)
/// 5. C stat (feedback hull + sfx stat)
/// 6. le toast reste un peu pendant/juste après l'anim max hull
/// </summary>
public class LevelEndModuleBonusController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSessionState;
    [SerializeField] private HullSystem hullSystem;

    [Header("UI")]
    [SerializeField] private StatToastUI statToastUI;

    [Header("Timing (unscaled)")]
    [SerializeField] private float preBonusDelay = 0.25f;

    [Tooltip("Petit délai entre l'apparition du toast H et l'application du bonus Hull.")]
    [SerializeField] private float toastToHullDelay = 0.08f;

    [Tooltip("Temps laissé au feedback stat Hull (H).")]
    [SerializeField] private float hullStatStepDuration = 0.30f;

    [Tooltip("Petit délai entre l'apparition du toast C et l'application du bonus Max Hull.")]
    [SerializeField] private float toastToMaxHullDelay = 0.08f;

    [Tooltip("Temps laissé au feedback stat Max Hull (C).")]
    [SerializeField] private float maxHullStatStepDuration = 0.35f;

    [Tooltip("Fallback si pas de StatToastUI pour H.")]
    [SerializeField] private float fallbackHullToastDuration = 0.8f;

    [Tooltip("Fallback si pas de StatToastUI pour C.")]
    [SerializeField] private float fallbackMaxHullToastDuration = 0.8f;

    public IEnumerator PlayPreCeremonyBonuses(bool mainObjectiveAchieved)
    {
        if (!mainObjectiveAchieved)
            yield break;

        if (runSessionState == null)
            yield break;

        ModuleRuntimeStats stats = ModuleRuntimeStats.Instance;
        if (stats == null)
            yield break;

        if (preBonusDelay > 0f)
            yield return new WaitForSecondsRealtime(preBonusDelay);

        yield return StartCoroutine(ApplyHullSustainBonus(stats));
        yield return StartCoroutine(ApplyCoreGrowthBonus(stats));
    }

    private IEnumerator ApplyHullSustainBonus(ModuleRuntimeStats stats)
    {
        var sustain = stats.GetEndLevelSustainBonus();

        if (sustain.hullGain <= 0)
            yield break;

        ModuleDefinition mod = stats.GetEndLevelSustainModule();
        if (mod == null)
            yield break;

        int hullBefore = Mathf.Max(0, runSessionState.Hull);
        int hullMax = Mathf.Max(1, runSessionState.HullMax);

        if (hullBefore >= hullMax)
            yield break;

        int theoreticalGain = Mathf.Max(0, sustain.hullGain);
        int actualGain = Mathf.Min(theoreticalGain, hullMax - hullBefore);

        if (actualGain <= 0)
            yield break;

        // 1) H module
        if (statToastUI != null)
            statToastUI.ShowHullRepair(mod, actualGain);

        if (toastToHullDelay > 0f)
            yield return new WaitForSecondsRealtime(toastToHullDelay);

        // 2) H stat
        runSessionState.RepairHull(sustain.hullGain);

        if (hullStatStepDuration > 0f)
            yield return new WaitForSecondsRealtime(hullStatStepDuration);

        // 3) on laisse ensuite le toast finir
        if (statToastUI != null)
        {
            yield return statToastUI.WaitUntilIdle();
        }
        else if (fallbackHullToastDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(fallbackHullToastDuration);
        }
    }

    private IEnumerator ApplyCoreGrowthBonus(ModuleRuntimeStats stats)
    {
        ModuleDefinition mod = stats.GetEndLevelCoreGrowthModule();
        if (mod == null)
            yield break;

        int amount = Mathf.Max(0, mod.endLevelFullHullHullMaxAdd);
        if (amount <= 0)
            yield break;

        bool hullIsFullNow = runSessionState.Hull >= runSessionState.HullMax;
        if (!hullIsFullNow)
            yield break;

        // 4) C module
        if (statToastUI != null)
            statToastUI.ShowMaxHullGain(mod);

        if (toastToMaxHullDelay > 0f)
            yield return new WaitForSecondsRealtime(toastToMaxHullDelay);

        // 5) C stat
        if (hullSystem != null)
            hullSystem.BeginMaxHullUpgrade();

        runSessionState.AddBonusHullMaxInRun(amount, true);

        if (hullSystem != null)
        {
            hullSystem.PlayMaxHullUpgradeFeedback();
            hullSystem.EndMaxHullUpgrade();
        }

        if (maxHullStatStepDuration > 0f)
            yield return new WaitForSecondsRealtime(maxHullStatStepDuration);

        // 6) on laisse ensuite le toast finir
        if (statToastUI != null)
        {
            yield return statToastUI.WaitUntilIdle();
        }
        else if (fallbackMaxHullToastDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(fallbackMaxHullToastDuration);
        }
    }
}