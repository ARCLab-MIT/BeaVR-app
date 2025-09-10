using Oculus.Interaction;
using TMPro;
using UnityEngine;

public class VirtualKeyboardTMPBinder : MonoBehaviour
{
    [SerializeField] private OVRVirtualKeyboard keyboard;
    [SerializeField] private TMP_InputField targetField;

    private string currentText = "";

    private void OnEnable()
    {
        if (keyboard == null) return;

        keyboard.CommitTextEvent.AddListener(OnTextCommit);
        keyboard.BackspaceEvent.AddListener(OnBackspace);
    }

    private void OnDisable()
    {
        if (keyboard == null) return;

        keyboard.CommitTextEvent.RemoveListener(OnTextCommit);
        keyboard.BackspaceEvent.RemoveListener(OnBackspace);
    }

    private void OnTextCommit(string typed)
    {
        currentText += typed;
        targetField.text = currentText;
    }

    private void OnBackspace()
    {
        if (currentText.Length > 0)
        {
            currentText = currentText.Substring(0, currentText.Length - 1);
            targetField.text = currentText;
        }
    }

    public void OpenKeyboard()
    {
        if (keyboard != null)
        {
            currentText = targetField.text;
            keyboard.gameObject.SetActive(true);
        }
    }

    public void CloseKeyboard()
    {
        if (keyboard != null)
        {
            keyboard.gameObject.SetActive(false);
        }
    }
}
