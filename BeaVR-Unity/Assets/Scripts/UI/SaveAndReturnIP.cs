using UnityEngine;
using TMPro;

public class SaveAndReturnIP : MonoBehaviour
{
    public TMP_InputField ipInput;
    public CanvasSwitch canvasSwitch;

    public const string PlayerPrefsKey = "ServerIP";

    private void Start()
    {
        string saved = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(saved))
        {
            if (ipInput != null) ipInput.SetTextWithoutNotify(saved);
        }
    }

    public void OnClick_SaveAndReturn()
    {
        // Prefer last validated value from IPFieldManager
        string normalized = IPFieldManager.GetLastValidatedIPv4();
        if (string.IsNullOrEmpty(normalized)) { Debug.Log("[SaveAndReturnIP] No validated IP available."); return; }

        PlayerPrefs.SetString(PlayerPrefsKey, normalized);
        PlayerPrefs.Save();

        Debug.Log($"[SaveAndReturnIP] Saved: {normalized}");

        var switcher = canvasSwitch != null ? canvasSwitch : GetComponent<CanvasSwitch>();
        if (switcher != null) switcher.Switch();

        // Clear ephemeral stash after successful save
        IPFieldManager.ClearLastValidatedIPv4();
    }

    // Read saved IP anywhere when needed: PlayerPrefs.GetString(PlayerPrefsKey, "")
}


