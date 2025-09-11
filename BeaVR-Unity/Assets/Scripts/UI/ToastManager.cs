using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public enum ToastStatus
{
    Success,
    Error
}

public class ToastManager : MonoBehaviour
{
    // Removed singleton - use direct references instead
    // public static ToastManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Root panel of the toast. Enable/disable happens here.")]
    public GameObject toastPanel;

    [Tooltip("Outline component used to render the border color around the toast panel.")]
    public Outline panelOutline;

    [Tooltip("Text component for the toast message.")]
    public TextMeshProUGUI messageText;

    [Tooltip("Icon image that changes by status.")]
    public Image iconImage;

    [Header("Icons")]
    public Sprite successIcon;
    public Sprite errorIcon;

    [Header("Colors")]
    public Color successColor = Color.green;
    public Color errorColor = Color.red;

    [Header("Behavior")]
    [Tooltip("How long the toast stays visible, in seconds.")]
    public float defaultDuration = 2f;

    Coroutine _hideRoutine;

    private void Awake()
    {
        // Removed singleton logic - each ToastManager is independent
        if (toastPanel != null)
            toastPanel.SetActive(false);
    }

    /// <summary>
    /// Shows a toast with the given message and status.
    /// </summary>
    public void ShowToast(string message, ToastStatus status, float? duration = null)
    {
        if (toastPanel == null || messageText == null || iconImage == null || panelOutline == null)
        {
            Debug.LogWarning("[ToastManager] Missing references. Please wire all UI fields.");
            return;
        }

        // Apply content
        toastPanel.SetActive(true);
        messageText.text = message;

        switch (status)
        {
            case ToastStatus.Success:
                iconImage.sprite = successIcon;
                panelOutline.effectColor = successColor;
                break;
            case ToastStatus.Error:
                iconImage.sprite = errorIcon;
                panelOutline.effectColor = errorColor;
                break;
        }

        // Restart hide timer if needed
        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);

        _hideRoutine = StartCoroutine(HideAfterDelay(duration ?? defaultDuration));
    }

    /// <summary>
    /// Convenience wrapper to show a success toast.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="duration">Optional custom duration in seconds.</param>
    public void Success(string message, float? duration = null)
    {
        ShowToast(message, ToastStatus.Success, duration);
    }

    /// <summary>
    /// Convenience wrapper to show an error toast.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="duration">Optional custom duration in seconds.</param>
    public void Error(string message, float? duration = null)
    {
        ShowToast(message, ToastStatus.Error, duration);
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (toastPanel != null)
            toastPanel.SetActive(false);
        _hideRoutine = null;
    }
}
