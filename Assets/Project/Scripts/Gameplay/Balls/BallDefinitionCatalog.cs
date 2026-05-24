using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "404/Balls/Ball Definition Catalog")]
public class BallDefinitionCatalog : ScriptableObject
{
    [SerializeField] private BallDefinition[] balls;

    private Dictionary<string, BallDefinition> byId;

    public BallDefinition[] Balls => balls;

    public void Initialize()
    {
        byId = new Dictionary<string, BallDefinition>();

        if (balls == null)
            return;

        foreach (BallDefinition ball in balls)
        {
            if (ball == null || string.IsNullOrWhiteSpace(ball.Id))
                continue;

            byId[ball.Id] = ball;
        }
    }

    public bool TryGet(string id, out BallDefinition definition)
    {
        if (byId == null)
            Initialize();

        return byId.TryGetValue(id, out definition);
    }
}