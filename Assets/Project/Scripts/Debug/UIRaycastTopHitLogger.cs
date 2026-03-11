using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastTopHitLogger : MonoBehaviour
{
    private readonly List<RaycastResult> _results = new List<RaycastResult>();

    void Update()
    {
        if (EventSystem.current == null) return;

        _results.Clear();
        var data = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        EventSystem.current.RaycastAll(data, _results);

        if (_results.Count > 0)
            Debug.Log("[UIRaycast] Top: " + _results[0].gameObject.transform.GetHierarchyPath());
    }
}

public static class TransformExtensions
{
    public static string GetHierarchyPath(this Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
