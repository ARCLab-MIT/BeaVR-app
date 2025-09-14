using System.Collections;
using UnityEngine;

public class StartTeleopButton : MonoBehaviour
{
    [Header("References")]
    public NetworkManager networkManager; // optional, will auto-find if null
    public CanvasSwitch canvasSwitch;     // target canvas switcher

    [Header("Behavior")]
    public float connectTimeoutSeconds = 2.0f;

    public void OnClick_StartTeleop()
    {
        if (networkManager == null)
        {
            var go = GameObject.Find("NetworkConfigsLoader");
            if (go != null) networkManager = go.GetComponent<NetworkManager>();
        }

        if (networkManager == null)
        {
            Debug.LogError("[StartTeleop] NetworkManager not found.");
            return;
        }

        // Build addresses using NetworkManager (ports from JSON, IP from PlayerPrefs)
        string ip = PlayerPrefs.GetString(SaveAndReturnIP.PlayerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning("[StartTeleop] No IP set in PlayerPrefs[ServerIP].");
            return;
        }

        NetMQController.Instance.Connect(
            ip,
            networkManager.getRightKeypointAddress(),
            networkManager.getLeftKeypointAddress(),
            networkManager.getResolutionAddress(),
            networkManager.getPauseAddress()
        );

        StartCoroutine(WaitAndSwitch());
    }

    private IEnumerator WaitAndSwitch()
    {
        float t = 0f;
        while (t < connectTimeoutSeconds)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
        }

        bool ok = NetMQController.Instance.AreSocketsConnected();
        Debug.Log("[StartTeleop] Connection status after timeout: " + ok);

        if (ok && canvasSwitch != null)
        {
            canvasSwitch.Switch();
        }
    }
}


