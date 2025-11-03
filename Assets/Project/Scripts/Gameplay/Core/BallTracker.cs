using System.Collections;
using UnityEngine;

public class BallTracker : MonoBehaviour
{ 

    /// <summary>
    /// True si toutes les billes actives sont soit en bin, soit déjà collectées.
    /// </summary>
    public bool AllBallsInBinOrCollected()
    {
        var all = Object.FindObjectsByType<BallState>(FindObjectsSortMode.None);
        foreach (var st in all)
        {
            if (st == null) continue;
            if (!st.collected && !st.inBin) return false;
        }
        return true;
    }

 
}
   