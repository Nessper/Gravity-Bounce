using UnityEngine;

/// <summary>
/// Fait défiler un grand sprite lentement (pour BG2).
/// Quand il sort de l'écran, il revient au-dessus.
/// Parfait pour un parallax très lent.
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    [Tooltip("Direction du défilement. (0,-1) pour descendre.")]
    public Vector2 direction = new Vector2(0f, -1f);

    [Tooltip("Vitesse du défilement.")]
    public float speed = 0.02f;

    [Tooltip("Marges pour éviter un wrap visible.")]
    public float margin = 1f;

    private Camera mainCam;
    private float width;
    private float height;

    private void Start()
    {
        mainCam = Camera.main;

        Renderer r = GetComponent<Renderer>();
        width = r.bounds.size.x;
        height = r.bounds.size.y;
    }

    private void Update()
    {
        // Mouvement continu
        Vector3 move = (Vector3)(direction.normalized * speed * Time.deltaTime);
        transform.position += move;

        WrapIfNeeded();
    }

    private void WrapIfNeeded()
    {
        Vector3 camPos = mainCam.transform.position;
        float vertExtent = mainCam.orthographicSize;
        float horExtent = vertExtent * mainCam.aspect;

        float bottom = camPos.y - vertExtent - margin - height * 0.5f;
        float top = camPos.y + vertExtent + margin + height * 0.5f;

        Vector3 pos = transform.position;

        // Si ça sort par le bas -> respawn en haut
        if (direction.y < 0f && pos.y < bottom)
        {
            pos.y = top;
            transform.position = pos;
        }
        // Si ça sort par le haut -> respawn en bas
        else if (direction.y > 0f && pos.y > top)
        {
            pos.y = bottom;
            transform.position = pos;
        }
    }
}
