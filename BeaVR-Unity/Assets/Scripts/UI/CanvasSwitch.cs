using UnityEngine;

public class CanvasSwitch : MonoBehaviour
{
    public GameObject nextCanvas;
    [Header("Optional")]
    public GameObject alsoDisableCanvas; // if set, this canvas will also be disabled
    GameObject currentCanvas;

    void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        if (c) currentCanvas = c.gameObject;
    }

    public void Switch()
    {
        if (currentCanvas) currentCanvas.SetActive(false);
        if (alsoDisableCanvas) alsoDisableCanvas.SetActive(false);
        if (nextCanvas) nextCanvas.SetActive(true);
    }
}
