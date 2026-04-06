using System;

/// <summary>
/// Base de donnees de textes localises simples, compatible avec JsonUtility.
/// </summary>
[Serializable]
public class LocalizedTextDatabase
{
    public LocalizedTextEntry[] entries;
}