using UnityEngine;

public class MobileOnlyUI : MonoBehaviour
{
    [SerializeField] private bool hideInEditor = false;

    private void Awake()
    {
        bool isMobile = Application.isMobilePlatform;

#if UNITY_EDITOR
        if (!isMobile && hideInEditor)
        {
            gameObject.SetActive(false);
            return;
        }
#else
        if (!isMobile)
        {
            gameObject.SetActive(false);
            return;
        }
#endif
    }
}
