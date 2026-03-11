using System;
using UnityEngine;

/// <summary>
/// Detecte les ecritures externes sur localScale.
/// A mettre temporairement sur un GO UI qui "revient a 1" sans raison.
/// Attention : a retirer apres debug (log verbeux).
/// </summary>
public class UIScaleWriteDetector : MonoBehaviour
{
    [SerializeField] private bool logStackTrace = true;

    private Vector3 lastScale;

    private void OnEnable()
    {
        lastScale = transform.localScale;
    }

    private void LateUpdate()
    {
        Vector3 current = transform.localScale;

        if (current != lastScale)
        {
            Debug.Log("[UIScaleWriteDetector] Scale change sur " + gameObject.name +
                      " : " + lastScale + " -> " + current);

            if (logStackTrace)
            {
                // Donne une trace d'appel. Pas parfait, mais souvent suffisant pour trouver le script fautif.
                Debug.Log("[UIScaleWriteDetector] StackTrace:\n" + Environment.StackTrace);
            }

            lastScale = current;
        }
    }
}
