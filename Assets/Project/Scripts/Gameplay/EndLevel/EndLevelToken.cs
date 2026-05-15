using System;
using UnityEngine;

[Serializable]
public struct EndLevelToken
{
    public string RunId;
    public string WorldId;
    public string LevelId;
    public int NodeIndex;

    public long TimestampUtc;
}
