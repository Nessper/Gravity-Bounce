using UnityEngine;

public class MobileOnlyUI : MonoBehaviour
{
    [SerializeField] private bool enableInEditor = false;

    private void Awake()
    {
#if UNITY_EDITOR
        if (!enableInEditor)
        {
            gameObject.SetActive(false);
            return;
        }
#endif
        if (!Application.isMobilePlatform)
        {
            gameObject.SetActive(false);
        }
    }
}
