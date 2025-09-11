using UnityEngine;

public class CanvasSwitch : MonoBehaviour
{
    public GameObject nextCanvas;
    GameObject currentCanvas;

    void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        if (c) currentCanvas = c.gameObject;
    }

    public void Switch()
    {
        if (currentCanvas) currentCanvas.SetActive(false);
        if (nextCanvas) nextCanvas.SetActive(true);
    }
}
