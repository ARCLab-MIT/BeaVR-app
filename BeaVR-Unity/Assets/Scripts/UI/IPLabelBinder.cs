using UnityEngine;
using TMPro;

public class IPLabelBinder : MonoBehaviour
{
    public TextMeshProUGUI label;

    private void OnEnable()
    {
        if (label == null) label = GetComponent<TextMeshProUGUI>();
        if (label == null) return;

        string value = PlayerPrefs.GetString(SaveAndReturnIP.PlayerPrefsKey, "-");
        label.text = $"IP Address: {value}";
    }
}


