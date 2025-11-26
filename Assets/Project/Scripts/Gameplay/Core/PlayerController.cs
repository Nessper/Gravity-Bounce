using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float xRange = 1.7f;
    [SerializeField] private Camera mainCam;
    [SerializeField] private bool canControl = true;

    [Header("Feedback visuel")]
    [SerializeField] private PlayerFlashFeedback flashFeedback;

    private Rigidbody playerRb;
    private float targetX;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!canControl) return;

        Vector3 screenPos = Input.mousePosition;
        float distance = Mathf.Abs(mainCam.transform.position.z - playerRb.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distance));
        targetX = Mathf.Clamp(worldPos.x, -xRange, xRange);
    }

    private void FixedUpdate()
    {
        if (!canControl) return;

        Vector3 p = playerRb.position;
        playerRb.MovePosition(new Vector3(targetX, p.y, p.z));
    }

    public void SetActiveControl(bool state)
    {
        canControl = state;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Adapte selon ton setup (tag, layer, etc.)
        if (collision.collider.CompareTag("Ball"))
        {
            if (flashFeedback != null)
            {
                flashFeedback.TriggerFlash();
            }
        }
    }
}
