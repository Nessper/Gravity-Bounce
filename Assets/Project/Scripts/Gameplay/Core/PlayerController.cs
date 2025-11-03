using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float xRange = 1.7f;
    [SerializeField] Camera mainCam; // pour pouvoir convetir la position du pointeur en coordonée monde (X)
    [SerializeField] private bool canControl = true; // mouvement du jouer = true


    private Rigidbody playerRb;
    private float targetX;              // pour stocker la position

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
     
    }

    // Update is called once per frame
    void Update()
    {
        if (!canControl) return;

        Vector3 screenPos = Input.mousePosition;                                                                    // donne la position du pointeur sur l'ecran en Px
        float distance = Mathf.Abs(mainCam.transform.position.z - playerRb.position.z);                             // calcule la distance entre la camera et le player sur l'axe Z
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distance));             // convertit la positiond pointeur en position monde (sur le plan du player)
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


}
