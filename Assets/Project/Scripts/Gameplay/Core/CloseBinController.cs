using UnityEngine;

public class CloseBinController : MonoBehaviour
{
    [Header("Assign the CloseWall objects manually")]
    [SerializeField] private GameObject leftCloseWall;
    [SerializeField] private GameObject rightCloseWall;

    private bool isClosed = false;
    private bool canControl = true;

    public void SetActiveControl(bool state)
    {
        canControl = state;
        // Si on desactive le controle, on rouvre les bacs pour eviter un etat ferme persistant
        if (!canControl && isClosed)
            SetCloseWallsActive(false);
    }

    void Update()
    {
        if (!canControl) return;

        bool shiftDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shiftDown)
        {
            if (!isClosed) SetCloseWallsActive(true);
        }
        else
        {
            if (isClosed) SetCloseWallsActive(false);
        }
    }

    private void SetCloseWallsActive(bool state)
    {
        isClosed = state;
        if (leftCloseWall != null) leftCloseWall.SetActive(state);
        if (rightCloseWall != null) rightCloseWall.SetActive(state);
    }
}
