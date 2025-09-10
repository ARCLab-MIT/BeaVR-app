using UnityEngine;
using TMPro;

public class KeypadInputController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private GameObject keypadRoot;

    [Header("Spawn Settings")]
    [SerializeField] private bool autoPlaceInFront = true;
    [SerializeField] private float distanceFromCamera = 0.6f;
    [SerializeField] private Vector3 offset = new Vector3(0, -0.1f, 0);

    private string currentInput = "";

    private void Start()
    {
        // Optionally hide the keypad at start
        if (keypadRoot != null)
        {
            keypadRoot.SetActive(false);
        }
    }

    public void ShowKeypad()
    {
        if (keypadRoot == null) return;

        if (autoPlaceInFront)
        {
            Transform cam = Camera.main.transform;
            Vector3 pos = cam.position + cam.forward * distanceFromCamera + offset;
            keypadRoot.transform.position = pos;

            keypadRoot.transform.LookAt(cam.position);
            keypadRoot.transform.Rotate(0, 180f, 0); // Face the user
        }

        keypadRoot.SetActive(true);
        currentInput = inputField.text;
    }

    public void HideKeypad()
    {
        if (keypadRoot != null)
            keypadRoot.SetActive(false);
    }

    public void AddDigit(string digit)
    {
        currentInput += digit;
        inputField.text = currentInput;
    }

    public void AddDot()
    {
        currentInput += ".";
        inputField.text = currentInput;
    }

    public void Backspace()
    {
        if (currentInput.Length > 0)
        {
            currentInput = currentInput.Substring(0, currentInput.Length - 1);
            inputField.text = currentInput;
        }
    }

    public void Clear()
    {
        currentInput = "";
        inputField.text = currentInput;
    }

    public void Save()
    {
        if (IsValidIP(currentInput))
        {
            PlayerPrefs.SetString("teleop_ip", currentInput);
            PlayerPrefs.Save();
            Debug.Log("✅ IP saved: " + currentInput);
            HideKeypad();
        }
        else
        {
            Debug.LogWarning("❌ Invalid IP format.");
        }
    }

    public void Cancel()
    {
        currentInput = PlayerPrefs.GetString("teleop_ip", "");
        inputField.text = currentInput;
        HideKeypad();
    }

    private bool IsValidIP(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4) return false;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out int num)) return false;
            if (num < 0 || num > 255) return false;
        }

        return true;
    }
}
