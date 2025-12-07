using UnityEngine;

public class TestProgressBar : MonoBehaviour
{
    // Par exemple dans un script de test temporaire
    public SegmentedProgressBarUI progressBar;

    void Update()
    {
        // Juste pour tester: progression qui avance avec le temps
        float t = Mathf.PingPong(Time.time * 0.2f, 1f);
        progressBar.SetProgress01(t);
    }

}
