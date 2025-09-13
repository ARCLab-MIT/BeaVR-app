using UnityEditor;
using UnityEngine;

public class IPValidatorWindow : EditorWindow
{
    string _input = "192.168.1.1";
    string _normalized = string.Empty;
    bool _isValid = false;

    [MenuItem("Tools/Networking/IP Validator")]    
    public static void ShowWindow()
    {
        var window = GetWindow<IPValidatorWindow>(false, "IP Validator", true);
        window.minSize = new Vector2(360, 140);
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Validate IPv4 without Play Mode", EditorStyles.boldLabel);
        _input = EditorGUILayout.TextField("Input", _input);

        if (GUILayout.Button("Validate"))
        {
            _normalized = IPFieldManager.NormalizeIPv4Input(_input);
            _isValid = IPFieldManager.IsValidIPv4(_normalized);
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Normalized", _normalized);
            EditorGUILayout.Toggle("Is Valid", _isValid);
        }

        if (!string.IsNullOrEmpty(_normalized))
        {
            var msg = _isValid ? $"Valid: {_normalized}" : $"Invalid: {_normalized}";
            var type = _isValid ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(msg, type);
        }
    }
}


