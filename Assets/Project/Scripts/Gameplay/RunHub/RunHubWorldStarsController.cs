using System.Collections.Generic;
using UnityEngine;

public class RunHubWorldStarsController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RunSessionState runSession;

    [Header("Stars (order: World1..World6)")]
    [SerializeField] private List<WorldStarView> stars = new List<WorldStarView>();

    [Header("Current World Index (temp)")]
    [SerializeField] private int debugWorldIndex = 0;

    private void OnEnable()
    {
        if (runSession != null)
            runSession.OnNodeChanged.AddListener(Refresh);
    }

    private void OnDisable()
    {
        if (runSession != null)
            runSession.OnNodeChanged.RemoveListener(Refresh);
    }

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (stars == null || stars.Count == 0)
            return;

        int currentWorldIndex = ResolveCurrentWorldIndex();
        currentWorldIndex = Mathf.Clamp(currentWorldIndex, 0, stars.Count - 1);

        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] == null)
                continue;

            bool isCurrent = (i == currentWorldIndex);
            stars[i].SetShipBadgeVisible(isCurrent);

            if (i < currentWorldIndex)
                stars[i].SetState(WorldStarView.StarState.Done);
            else if (isCurrent)
                stars[i].SetState(WorldStarView.StarState.Current);
            else
                stars[i].SetState(WorldStarView.StarState.Locked);
        }
    }


    private int ResolveCurrentWorldIndex()
    {
        // Pour l'instant, on n'a qu'un monde W1.
        // Donc le world index est 0.
        // Ce hook te permettra plus tard de mapper W2..W6 sans refactor.
        if (runSession == null)
            return debugWorldIndex;

        string worldId = runSession.WorldId;
        if (string.IsNullOrEmpty(worldId))
            return debugWorldIndex;

        // Convention simple: "W1" -> 0, "W2" -> 1, etc.
        if (worldId.Length >= 2 && worldId[0] == 'W')
        {
            int n;
            if (int.TryParse(worldId.Substring(1), out n))
                return Mathf.Max(0, n - 1);
        }

        return debugWorldIndex;
    }
}
