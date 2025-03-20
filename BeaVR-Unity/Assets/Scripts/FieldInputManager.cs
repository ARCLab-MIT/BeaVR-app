using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FieldInputManager : MonoBehaviour
{
    public TMP_Dropdown FirstDropDown;
    public TMP_Dropdown SecondDropDown;
    public TMP_Dropdown ThirdDropDown;
    public TMP_Dropdown FourthDropDown;
    public TextMeshProUGUI feedbackText;

    private NetworkManager netConfig;

    void Start()
    {
        List<string> optionList = new List<string>();

        // Create a for loop which generates all the options
        for (int optionNumber = 0; optionNumber < 256; optionNumber++)
            optionList.Add(optionNumber.ToString());

        FirstDropDown.AddOptions(optionList);
        SecondDropDown.AddOptions(optionList);
        ThirdDropDown.AddOptions(optionList);
        FourthDropDown.AddOptions(optionList);

        // Getting the Network Config Updater gameobject
        GameObject netConfGame = GameObject.Find("NetworkConfigsLoader");
        if (netConfGame != null)
        {
            netConfig = netConfGame.GetComponent<NetworkManager>();
        }

        // Initialize feedback text
        if (feedbackText != null)
            feedbackText.text = "Enter server IP address";
    }

    public void getIPAddress()
    {
        string newIPAddress = FirstDropDown.options[FirstDropDown.value].text + "." +
            SecondDropDown.options[SecondDropDown.value].text + "." +
            ThirdDropDown.options[ThirdDropDown.value].text + "." +
            FourthDropDown.options[FourthDropDown.value].text;

        // Change the IP Address
        if (netConfig != null)
        {
            // First disconnect any existing connections
            netConfig.DisconnectAllNetworkComponents();
            
            // Update the IP
            netConfig.changeIPAddress(newIPAddress);
            
            // Display feedback message
            if (feedbackText != null)
            {
                feedbackText.text = "Connecting to " + newIPAddress + "...";
            }
            
            // Now explicitly connect
            netConfig.ConnectAllNetworkComponents();
            
            // Schedule a delayed check
            StartCoroutine(UpdateConnectionStatusAfterDelay(3.0f));
        }
    }

    private IEnumerator UpdateConnectionStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        GameObject gestureDetector = GameObject.Find("GestureDetector");
        if (gestureDetector != null)
        {
            GestureDetector detector = gestureDetector.GetComponent<GestureDetector>();
            if (detector != null)
            {
                if (detector.AreAllConnectionsEstablished())
                {
                    if (feedbackText != null)
                        feedbackText.text = "Connected successfully!";
                }
                else
                {
                    if (feedbackText != null)
                        feedbackText.text = "Connection failed. Check server and try again.";
                }
            }
        }
    }
}
