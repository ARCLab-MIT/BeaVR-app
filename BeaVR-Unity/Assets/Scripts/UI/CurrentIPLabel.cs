using UnityEngine;
using TMPro;

public class CurrentIPLabel : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI target;

    [Header("Formatting")]
    public string prefix = "Current IP Address:";
    public string emptyFallback = "Unknown";

    private void OnEnable()
    {
        if (target == null) target = GetComponent<TextMeshProUGUI>();
        if (target == null) return;

        string ip = PlayerPrefs.GetString(SaveAndReturnIP.PlayerPrefsKey, string.Empty);
        target.text = string.IsNullOrEmpty(ip)
            ? $"{prefix} {emptyFallback}"
            : $"{prefix} {ip}";
    }
}


