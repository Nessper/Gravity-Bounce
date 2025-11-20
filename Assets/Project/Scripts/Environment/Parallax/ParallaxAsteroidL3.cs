using UnityEngine;

/// <summary>
/// Parallax lent pour un astéroïde de décor (Layer 3).
/// Le sprite dérive dans une direction donnée, et lorsqu'il
/// sort du cadre de la caméra, il est replacé de l'autre côté
/// avec une position horizontale aléatoire.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ParallaxAsteroidL3 : MonoBehaviour
{
    [Tooltip("Vitesse minimale (unités par seconde).")]
    public float minSpeed = 0.2f;

    [Tooltip("Vitesse maximale (unités par seconde).")]
    public float maxSpeed = 0.5f;

    [Tooltip("Direction de dérive. Pour descendre: (0, -1).")]
    public Vector2 direction = new Vector2(0f, -1f);

    [Tooltip("Marge en dehors de l'écran avant le wrap.")]
    public float margin = 1f;

    private Camera mainCam;
    private float currentSpeed;
    private float halfWidth;
    private float halfHeight;

    private void Awake()
    {
        mainCam = Camera.main;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            Vector3 size = rend.bounds.size;
            halfWidth = size.x * 0.5f;
            halfHeight = size.y * 0.5f;
        }

        currentSpeed = Random.Range(minSpeed, maxSpeed);
    }

    private void Update()
    {
        if (mainCam == null)
            return;

        // Mouvement
        Vector3 move = (Vector3)(direction.normalized * currentSpeed * Time.deltaTime);
        transform.position += move;

        WrapIfOutside();
    }

    private void WrapIfOutside()
    {
        Vector3 camPos = mainCam.transform.position;

        float vertExtent = mainCam.orthographicSize;
        float horExtent = vertExtent * mainCam.aspect;

        float bottom = camPos.y - vertExtent - margin - halfHeight;
        float top = camPos.y + vertExtent + margin + halfHeight;

        Vector3 pos = transform.position;
        bool wrapped = false;

        // On part du principe que L3 descend (direction.y < 0)
        if (direction.y < 0f && pos.y < bottom)
        {
            pos.y = top;
            wrapped = true;
        }
        else if (direction.y > 0f && pos.y > top)
        {
            pos.y = bottom;
            wrapped = true;
        }

        if (wrapped)
        {
            // Nouvelle position X aléatoire dans la largeur de la caméra
            float minX = camPos.x - horExtent - margin;
            float maxX = camPos.x + horExtent + margin;
            pos.x = Random.Range(minX, maxX);

            transform.position = pos;

            // Optionnel: nouvelle vitesse à chaque wrap
            currentSpeed = Random.Range(minSpeed, maxSpeed);
        }
    }
}
