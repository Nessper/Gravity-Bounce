using UnityEngine;

public class FlushTest : MonoBehaviour
{
    public BinFlushFX binLeft;
    public BinFlushFX binRight;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (binLeft != null)
            {
                Debug.Log("Flush LEFT");
                binLeft.PlayFlush(false, 520);
            }
            else
            {
                Debug.LogWarning("FlushTest: binLeft non assigne");
            }

            if (binRight != null)
            {
                Debug.Log("Flush RIGHT");
                binRight.PlayFlush(false, 730);
            }
            else
            {
                Debug.LogWarning("FlushTest: binRight non assigne");
            }
        }
    }
}
