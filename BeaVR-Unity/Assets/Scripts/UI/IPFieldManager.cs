using UnityEngine;
using TMPro;
using System.Net;
using System.Net.Sockets;
using UnityEngine.EventSystems;


public class IPFieldManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TMP Input Field for entering the IP address.")]
    public TMP_InputField ipInput;

    [Tooltip("Required: ToastManager reference for this canvas/menu.")]
    public ToastManager toastManager;

    private void Awake()
    {
        if (ipInput == null)
            Debug.LogWarning("[IPFieldManager] ipInput is not assigned.");

        if (toastManager == null)
            Debug.LogWarning("[IPFieldManager] toastManager is not assigned. Please assign it in the inspector.");

        // Validation will be triggered via Inspector-wired On End Edit event.
    }


    private void OnEnable()
    {
        // Some TMP versions need explicit setting to submit on return for multi-line;
        // best practice: set Line Type = Single Line in the inspector.
    }

    

    /// <summary>
    /// Public handler intended for wiring via Inspector to TMP InputField's
    /// On End Edit (String) event. Mirrors the submit behavior.
    /// </summary>
    /// <param name="text">The final text from the input field.</param>
    public void OnEndEdit_FromInspector(string text)
    {
        TryProcessIP(text);
    }

    /// <summary>
    /// Public hook if you also want a button to trigger validation.
    /// </summary>
    public void OnClick_SubmitIP()
    {
        if (ipInput == null) return;
        TryProcessIP(ipInput.text);
    }

    private void TryProcessIP(string raw)
    {
        if (ipInput == null)
        {
            Debug.LogWarning("[IPFieldManager] No input field set.");
            return;
        }

        string ip = (raw ?? string.Empty).Trim();

        if (IsValidIPv4(ip))
        {
            // ✅ Valid
            if (toastManager != null) toastManager.Success($"IP set to {ip}");
            else Debug.Log($"[Toast] Success: IP set to {ip}");

            ipInput.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null);

            // Clear selection
            ipInput.caretPosition = ipInput.text.Length;
            ipInput.selectionStringAnchorPosition = ipInput.caretPosition;
            ipInput.selectionStringFocusPosition = ipInput.caretPosition;
        }
        else
        {
            // ❌ Invalid
            if (toastManager != null) toastManager.Error("Invalid IP format. Use IPv4 like 192.168.1.10");
            else Debug.Log("[Toast] Error: Invalid IP format. Use IPv4 like 192.168.1.10");

            // Keep focus and select all so user can retype quickly
            ipInput.ActivateInputField();
            ipInput.Select();
        }
    }

    /// <summary>
    /// Validates IPv4 addresses (0.0.0.0 to 255.255.255.255) without DNS parsing quirks.
    /// </summary>
    private bool IsValidIPv4(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        // Trim whitespace that can come from TMP events
        candidate = candidate.Trim();

        // Simple split-and-range validation to avoid platform-specific parsing issues
        var parts = candidate.Split('.');
        if (parts.Length != 4) return false;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0 || part.Length > 3) return false;
            if (!int.TryParse(part, out var value)) return false;
            if (value < 0 || value > 255) return false;
            // Reject leading zeros like 01 (but allow single 0)
            if (part.Length > 1 && part[0] == '0') return false;
        }

        return true;
    }
}
