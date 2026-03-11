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
    public string id;          // ex: W1_L2_intro
    public string type;        // intro, phase, evac, outro (optionnel si id suffit)
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
