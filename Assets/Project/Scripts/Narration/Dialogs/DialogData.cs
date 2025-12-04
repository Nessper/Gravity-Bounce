using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogDatabase
{
    public string language;
    public DialogSequence[] sequences;
}

[Serializable]
public class DialogSequence
{
    public string id;

    // "intro", "phase", "postLevel" exactement comme dans le JSON
    public string type;

    public int world;
    public int level;

    // Optionnel, utilisé seulement pour les messages de phase
    public int phaseIndex;

    // Optionnel, utilisé pour la fin de niveau (ex: "success")
    public string outcome;

    public DialogVariant[] variants;
}

[Serializable]
public class DialogVariant
{
    public int weight = 1;
    public DialogLine[] lines;
}

[Serializable]
public class DialogLine
{
    public string speakerId;
    public string text;
}
