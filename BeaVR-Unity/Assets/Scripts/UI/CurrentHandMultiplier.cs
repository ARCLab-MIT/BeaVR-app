using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HandMultiplierDisplay : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private float stepSize = 0.1f;

    private void Start()
    {
        // Snap and display the initial value
        float initialValue = Snap(slider.value);
        valueText.text = $"HandMultiplier: {initialValue}";

        // Subscribe to slider changes to update display
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float rawValue)
    {
        float snapped = Snap(rawValue);
        valueText.text = $"Hand Multiplier: {snapped:F2}";
    }

    private float Snap(float value)
    {
        return Mathf.Round(value / stepSize) * stepSize;
    }
}
