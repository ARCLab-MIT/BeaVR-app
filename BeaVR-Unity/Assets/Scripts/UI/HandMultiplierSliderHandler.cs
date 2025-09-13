using UnityEngine;
using UnityEngine.UI;

public class HandMultiplierSaver : MonoBehaviour
{
    [SerializeField] private Slider slider;

    [SerializeField] private string prefsKey = "HandMultiplier";
    [SerializeField] private float stepSize = 0.1f;
    [SerializeField] private float defaultValue = 1.6f;

    public void SaveFromSlider(float rawValue)
    {
        float snapped = Snap(rawValue);
        PlayerPrefs.SetFloat(prefsKey, snapped);
        PlayerPrefs.Save();
    }

    private void Start()
    {
        // Load saved value and apply to slider
        float savedValue = PlayerPrefs.GetFloat(prefsKey, defaultValue);
        slider.SetValueWithoutNotify(Snap(savedValue));

        // Subscribe to value change
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float rawValue)
    {
        float snapped = Snap(rawValue);
        PlayerPrefs.SetFloat(prefsKey, snapped);
        PlayerPrefs.Save();
    }

    private float Snap(float value)
    {
        return Mathf.Round(value / stepSize) * stepSize;
    }
}
