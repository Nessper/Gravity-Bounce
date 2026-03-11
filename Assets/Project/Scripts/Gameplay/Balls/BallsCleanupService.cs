using UnityEngine;

namespace VoidScrappers.Gameplay.Balls
{
    /// <summary>
    /// Service de nettoyage des billes runtime.
    /// But : éviter toute accumulation (leak mémoire) en détruisant
    /// toutes les billes restantes dans la scène à la sortie d'un niveau
    /// (Menu / Retry / Next) ou au reset.
    /// </summary>
    public sealed class BallsCleanupService : MonoBehaviour
    {
        [Header("Référence")]
        [Tooltip("Parent qui contient toutes les billes instanciées (ex: BallsRoot).")]
        [SerializeField] private Transform ballsRoot;

        /// <summary>
        /// Détruit toutes les billes actuellement enfants de ballsRoot.
        /// À appeler de façon déterministe (ex: au clic sur Menu/Retry/Next).
        /// </summary>
        public void ClearAllBalls()
        {
            if (ballsRoot == null)
            {
                Debug.LogWarning("[BallsCleanupService] ballsRoot est null : nettoyage ignoré.");
                return;
            }

            int countBefore = ballsRoot.childCount;

            // Boucle inversée : robuste si la hiérarchie bouge pendant les Destroy.
            for (int i = ballsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = ballsRoot.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }

            Debug.Log($"[BallsCleanupService] ClearAllBalls() : {countBefore} bille(s) détruite(s).");
        }

#if UNITY_EDITOR
        [ContextMenu("DEBUG / Clear All Balls")]
        private void Debug_ClearAllBalls()
        {
            ClearAllBalls();
        }
#endif
    }
}