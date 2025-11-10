using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class UIButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Effets visuels")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.4f, 0.9f, 1f);
    [SerializeField] private float scaleUp = 1.1f;
    [SerializeField] private float lerpSpeed = 10f;

    private Button button;
    private Image image;
    private Vector3 baseScale;
    private bool isHovered;

    private void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        baseScale = transform.localScale;
    }

    private void Update()
    {
        // transition fluide couleur + scale
        if (image)
        {
            Color targetColor = isHovered ? hoverColor : normalColor;
            image.color = Color.Lerp(image.color, targetColor, Time.deltaTime * lerpSpeed);
        }

        Vector3 targetScale = isHovered ? baseScale * scaleUp : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * lerpSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }
}
