using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastDebug : MonoBehaviour
{
    [SerializeField] private int maxToLog = 5;

    private readonly List<RaycastResult> _hits = new List<RaycastResult>(64);

    private void Update()
    {
        if (EventSystem.current == null)
            return;

        _hits.Clear();

        PointerEventData ped = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        EventSystem.current.RaycastAll(ped, _hits);

        if (_hits.Count == 0)
            return;

        StringBuilder sb = new StringBuilder();
        int n = Mathf.Min(maxToLog, _hits.Count);

        sb.Append("[UIRaycastDebug] Hits: ");
        sb.Append(n);
        sb.Append(" | ");

        for (int i = 0; i < n; i++)
        {
            GameObject go = _hits[i].gameObject;
            sb.Append(i);
            sb.Append(":");
            sb.Append(GetHierarchyPath(go));
            sb.Append("  ");
        }

        Debug.Log(sb.ToString());
    }

    private string GetHierarchyPath(GameObject go)
    {
        if (go == null)
            return "(null)";

        StringBuilder sb = new StringBuilder();
        Transform t = go.transform;

        while (t != null)
        {
            if (sb.Length == 0) sb.Insert(0, t.name);
            else sb.Insert(0, t.name + "/");
            t = t.parent;
        }

        return sb.ToString();
    }
}
