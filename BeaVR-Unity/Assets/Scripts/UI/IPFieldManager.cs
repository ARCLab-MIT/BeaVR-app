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

    [Header("Debug")]
    [Tooltip("When enabled, logs raw and normalized IP input for troubleshooting.")]
    public bool enableDebugLogging = false;

    [Header("Input Options")]
    [Tooltip("When true, blocks non-digit and non-dot characters at entry time.")]
    public bool restrictToDigitsAndDot = false;

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
        // Enforce single-line behavior at runtime
        if (ipInput != null)
        {
            ipInput.lineType = TMP_InputField.LineType.SingleLine;
            // Only allow digits and '.' at entry time (optional)
            if (restrictToDigitsAndDot)
                ipInput.onValidateInput += ValidateChar;
        }
    }

    private void OnDisable()
    {
        if (ipInput != null)
        {
            if (restrictToDigitsAndDot)
                ipInput.onValidateInput -= ValidateChar;
        }
    }

    

    /// <summary>
    /// Public handler intended for wiring via Inspector to TMP InputField's
    /// On End Edit (String) event. Mirrors the submit behavior.
    /// </summary>
    /// <param name="text">The final text from the input field.</param>
    public void OnEndEdit_FromInspector(string text)
    {
        if (enableDebugLogging)
            Debug.Log("[IPFieldManager] OnEndEdit param='" + (text ?? "<null>") + "' field='" + (ipInput != null ? ipInput.text : "<no field>") + "'");
        string submitted = !string.IsNullOrEmpty(text) ? text : (ipInput != null ? ipInput.text : string.Empty);
        TryProcessIP(submitted);
    }

    /// <summary>
    /// Optional: wire to TMP InputField's On Submit (String) to catch IME Done/Enter.
    /// </summary>
    public void OnSubmit_FromInspector(string text)
    {
        if (enableDebugLogging)
            Debug.Log("[IPFieldManager] OnSubmit param='" + (text ?? "<null>") + "' field='" + (ipInput != null ? ipInput.text : "<no field>") + "'");
        string submitted = !string.IsNullOrEmpty(text) ? text : (ipInput != null ? ipInput.text : string.Empty);
        TryProcessIP(submitted);
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

        string original = raw ?? string.Empty;
        string ip = NormalizeIPv4Input(original);

        if (enableDebugLogging)
        {
            Debug.Log($"[IPFieldManager] Raw='{original}' => Normalized='{ip}'");
        }

        // If user dismissed keyboard with empty text, do not block exit
        if (string.IsNullOrWhiteSpace(ip))
        {
            if (enableDebugLogging)
                Debug.Log("[IPFieldManager] Empty submit; not showing error or refocusing.");
            ipInput?.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null);
            return;
        }

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
            string shown = (original ?? string.Empty).Trim();
            if (toastManager != null) toastManager.Error($"Invalid IP: {shown}");
            else Debug.Log($"[Toast] Error: Invalid IP: {shown}");
            // Do not force keyboard to reappear; allow user to dismiss
            // (Optionally, you could re-focus only if there is text and user prefers)
        }
    }

    /// <summary>
    /// Validates IPv4 addresses (0.0.0.0 to 255.255.255.255) without DNS parsing quirks.
    /// </summary>
    public static bool IsValidIPv4(string candidate)
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

    /// <summary>
    /// Normalizes raw input into a plain IPv4 candidate string.
    /// - Trims whitespace
    /// - Replaces common unicode dot variants with '.'
    /// - If a port is included (e.g., 1.2.3.4:5555), strips the port
    /// </summary>
    public static string NormalizeIPv4Input(string raw)
    {
        if (raw == null) return string.Empty;
        string s = raw.Trim();

        // Strip scheme if present (e.g., http://)
        int schemeIdx = s.IndexOf("://");
        if (schemeIdx >= 0)
        {
            s = s.Substring(schemeIdx + 3);
        }

        // Cut off path/query if present
        int slashIdx = s.IndexOf('/');
        if (slashIdx >= 0)
        {
            s = s.Substring(0, slashIdx);
        }

        // Drop port if present
        int colon = s.IndexOf(':');
        if (colon >= 0)
        {
            s = s.Substring(0, colon);
        }

        // Replace common dot-like characters and separators with '.'
        char[] dotLikes = new char[] { '。', '．', '・', '·', '∙', '․', '‧', '⋅', '•', '●', '｡', ',', ' ' };
        for (int i = 0; i < dotLikes.Length; i++)
        {
            s = s.Replace(dotLikes[i], '.');
        }

        // Keep only digits and '.'; remove zero-width and other invisible chars
        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsDigit(c) || c == '.') sb.Append(c);
        }
        s = sb.ToString();

        // Collapse consecutive dots and trim
        while (s.Contains("..")) s = s.Replace("..", ".");
        s = s.Trim('.');

        return s;
    }

    private char ValidateChar(string text, int charIndex, char addedChar)
    {
        // Allow digits and '.' only
        if (char.IsDigit(addedChar) || addedChar == '.') return addedChar;
        return '\0';
    }
}
