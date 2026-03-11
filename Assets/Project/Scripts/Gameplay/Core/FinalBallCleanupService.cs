using System.Collections;
using UnityEngine;

/// <summary>
/// Service de cleanup fin de niveau:
/// - scan des BallState encore actives
/// - RegisterLost si necessaire
/// - recycle via BallSpawner
///
/// But: sortir FinalSweepMarkLostAndRecycle de LevelManager.
/// </summary>
public class FinalBallCleanupService : MonoBehaviour
{
    public IEnumerator Execute(BallSpawner spawner, ScoreManager scoreManager)
    {
        // Laisse une frame (ou deux) pour laisser finir les callbacks de fin (flush, triggers, etc.)
        yield return null;
        yield return null;

#if UNITY_6000_0_OR_NEWER
        BallState[] balls = UnityEngine.Object.FindObjectsByType<BallState>(FindObjectsSortMode.None);
#else
        BallState[] balls = UnityEngine.Object.FindObjectsOfType<BallState>();
#endif

        for (int i = 0; i < balls.Length; i++)
        {
            BallState st = balls[i];
            if (st == null)
                continue;

            GameObject go = st.gameObject;
            if (go == null || !go.activeInHierarchy)
                continue;

            // Deja collectee: recycle "collected"
            if (st.collected)
            {
                if (spawner != null)
                    spawner.Recycle(go, collected: true);
                continue;
            }

            // Encore dans un bin: on ne touche pas (evite de casser un flush en cours)
            if (st.inBin)
                continue;

            // Sinon: consideree perdue
            if (scoreManager != null)
                scoreManager.RegisterLost(st.TypeName);

            if (spawner != null)
                spawner.Recycle(go, collected: false);
        }
    }
}
