using System;

/// <summary>
/// Une entree de texte localise simple, identifiee par une cle unique.
/// Utilisee pour les packs de type tutorial, ui, labels, etc.
/// </summary>
[Serializable]
public class LocalizedTextEntry
{
    public string key;
    public string text;
}