using System;
using System.Collections.Generic;
using System.Collections;

using UnityEngine;
using UnityEngine.UI;

using NetMQ;
using NetMQ.Sockets;

class GestureDetector : MonoBehaviour
{
    // Hand objects
    public OVRHand LeftHand;
    public OVRHand RightHand;
    public OVRSkeleton LeftHandSkeleton;
    public OVRSkeleton RightHandSkeleton;
    public OVRPassthroughLayer PassthroughLayerManager;
    private List<OVRBone> RightHandFingerBones;
    private List<OVRBone> LeftHandFingerBones;

    // Menu and RayCaster GameObjects
    public GameObject MenuButton;
    public GameObject ResolutionButton;
    public GameObject HighResolutionButton;
    public GameObject LowResolutionButton;

    public GameObject WristTracker;
    private GameObject LaserPointer;
    private LineRenderer LineRenderer;


    // Hand Usage indicator
    public RawImage StreamBorder;

    // Stream Enablers
    bool StreamRelativeData = true;
    bool StreamAbsoluteData = false;

    public HighResolutionButtonController HighResolutionButtonController;
    public LowResolutionButtonController LowResolutionButtonController;

    // Network enablers
    private NetworkManager netConfig;
    private PushSocket client;
    private PushSocket client2;
    private PushSocket client3;
    private string communicationAddress;
    private string ResolutionAddress;

    private string PauseAddress;

    private string state;
    private string pauseState;
    private bool connectionEstablished = false;

    private bool resolutionconnectionEstablished = false;

    private bool ShouldContinueArmTeleop = false;

    private bool PauseEstablished = false;
    private bool resolutioncreated = false;
    private bool PauseCreated = false;

    private PushSocket leftClient;  // New socket just for left hand
    private string leftCommunicationAddress;
    private bool leftConnectionEstablished = false;

    // Starting the server connection
    public void CreateTCPConnection()
    {
        try
        {
            // Original right hand connection
            communicationAddress = netConfig.getRightKeypointAddress();
            bool addressAvailable = !String.Equals(communicationAddress, "tcp://:");

            // New left hand connection
            leftCommunicationAddress = netConfig.getLeftKeypointAddress();
            bool leftAddressAvailable = !String.Equals(leftCommunicationAddress, "tcp://:");

            if (addressAvailable)
            {
                client = new PushSocket();
                client.Connect(communicationAddress);
                connectionEstablished = true;
            }

            if (leftAddressAvailable)
            {
                leftClient = new PushSocket();
                leftClient.Connect(leftCommunicationAddress);
                leftConnectionEstablished = true;
            }

            // Setting color to green to indicate control
            if (connectionEstablished && leftConnectionEstablished)
            {
                StreamBorder.color = Color.green;
                ToggleMenuButton(false);
            }
            else
            {
                StreamBorder.color = Color.red;
                ToggleMenuButton(true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error creating TCP connections: " + e.Message);
            StreamBorder.color = Color.red;
            ToggleMenuButton(true);
        }
    }
   

    public void ToggleMenuButton(bool toggle)
    {
        if (MenuButton != null)
        {
            MenuButton.SetActive(toggle);
            Debug.Log("Menu button toggled: " + toggle);
        }
        else
        {
            Debug.LogWarning("Menu button reference is null in GestureDetector");
        }
    }

    public void ToggleResolutionButton(bool toggle)
    {
        // Completely safe implementation with nested null checks
        try {
            if (ResolutionButton != null)
            {
                ResolutionButton.SetActive(toggle);
            }
            
            if (LineRenderer != null)
            {
                LineRenderer.enabled = toggle;
            }
        }
        catch (Exception e) {
            Debug.LogError("Error in ToggleResolutionButton: " + e.Message);
        }
    }

    public void ToggleHighResolutionButton(bool toggle)
    {
        // No-op since we don't need this button
        // Just log for debugging if needed
        Debug.Log("HighResolutionButton toggle called (ignored): " + toggle);
    }

    public void ToggleLowResolutionButton(bool toggle)
    {
        // No-op since we don't need this button
        // Just log for debugging if needed
        Debug.Log("LowResolutionButton toggle called (ignored): " + toggle);
    }

    // Start function
    void Start()
     {
        // Getting the Network Config Updater gameobject
        GameObject netConfGameObject = GameObject.Find("NetworkConfigsLoader");
        netConfig = netConfGameObject.GetComponent<NetworkManager>();

        // Create fallback components if needed
        LaserPointer = GameObject.Find("LaserPointer");
        if (LaserPointer == null) {
            Debug.LogWarning("LaserPointer not found - creating simple replacement");
            LaserPointer = new GameObject("LaserPointer");
            LineRenderer = LaserPointer.AddComponent<LineRenderer>();
        } else {
            LineRenderer = LaserPointer.GetComponent<LineRenderer>();
            if (LineRenderer == null) {
                Debug.LogWarning("LineRenderer not found on LaserPointer - adding one");
                LineRenderer = LaserPointer.AddComponent<LineRenderer>();
            }
        }

        // Initializing the hand skeleton
        RightHandFingerBones = new List<OVRBone>(RightHandSkeleton.Bones);
        LeftHandFingerBones = new List<OVRBone>(LeftHandSkeleton.Bones);

        // Register with NetworkKeepAlive if it exists
        GameObject keepAliveObj = GameObject.Find("NetworkKeepAlive");
        if (keepAliveObj != null)
        {
            NetworkKeepAlive keepAlive = keepAliveObj.GetComponent<NetworkKeepAlive>();
            if (keepAlive != null)
            {
                Debug.Log("GestureDetector registered with NetworkKeepAlive");
            }
        }
    }


    // Function to serialize the Vector3 List
    public static string SerializeVector3List(List<Vector3> gestureData)
    {
        string vectorString = "";
        foreach (Vector3 vec in gestureData)
            vectorString = vectorString + vec.x + "," + vec.y + "," + vec.z + "|";

        // Clipping last element and using a semi colon instead
        if (vectorString.Length > 0)
            vectorString = vectorString.Substring(0, vectorString.Length - 1) + ":";

        return vectorString;
    }

    public void SendRightHandData(String TypeMarker)
    {
        try
        {
            // Getting bone positional information
            List<Vector3> rightHandGestureData = new List<Vector3>();
            foreach (var bone in RightHandFingerBones)
            {
                Vector3 bonePosition = bone.Transform.position;
                rightHandGestureData.Add(bonePosition);
            }

            // Creating a string from the vectors
            string RightHandDataString = SerializeVector3List(rightHandGestureData);
            RightHandDataString = TypeMarker + ":" + RightHandDataString;

            client.SendFrame(RightHandDataString);
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending right hand data: " + e.Message);
            connectionEstablished = false; // Force reconnection
        }
    }

    public void SendLeftHandData(String TypeMarker)
    {
        try
        {
            if (leftClient != null && leftConnectionEstablished)
            {
                // Getting bone positional information for left hand
                List<Vector3> leftHandGestureData = new List<Vector3>();
                foreach (var bone in LeftHandFingerBones)
                {
                    Vector3 bonePosition = bone.Transform.position;
                    leftHandGestureData.Add(bonePosition);
                }

                // Creating a string from the vectors
                string leftHandDataString = SerializeVector3List(leftHandGestureData);
                leftHandDataString = TypeMarker + ":" + leftHandDataString;

                leftClient.SendFrame(leftHandDataString);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending left hand data: " + e.Message);
            leftConnectionEstablished = false; // Force reconnection
        }
    }

    public void SendResolution()
    {
        if (resolutionconnectionEstablished)
        {
            if (HighResolutionButtonController.HighResolution)
            {   
                state = "High";
                client2.SendFrame(state);
                Debug.Log("High Button was clicked!");
            }
            else if (LowResolutionButtonController.LowResolution)
            {   
                state = "Low";
                client2.SendFrame(state);
                Debug.Log("Low Button was clicked!");
            }
            else 
            {   
                client2.SendFrame("None"); 
                Debug.Log("No button was pressed");
            }
        }
        else if (client2 != null)
        {
            client2.SendFrame("None");
        }
    }

    public void SendResetStatus()
    {
        if (PauseEstablished)
        {
            if (ShouldContinueArmTeleop){
                pauseState = "High";
            } else {
                pauseState = "Low";
            }
            client3.SendFrame(pauseState);
        }
        else 
        {
            
            pauseState="None";
            client3.SendFrame(pauseState);
        }
    }
    
    public void SendCont()
    {
        if (PauseEstablished)
        {
           
            pauseState="High";
            client3.SendFrame(pauseState);
            
        }
        else 
        {
            
            pauseState="None";
            client3.SendFrame(pauseState);
        }
    }

    public void SendPause()
    {
        if (PauseEstablished)
        {
           
            pauseState="Low";
            client3.SendFrame(pauseState);
            
        }
        else 
        {
            
            pauseState="None";
            client3.SendFrame(pauseState);
        }
    }
    

    public void StreamPauser()
    {
        // Switching from Right hand control
        if (LeftHand.GetFingerIsPinching(OVRHand.HandFinger.Middle))
        {
            StreamRelativeData = false;
            StreamAbsoluteData = true;
            StreamBorder.color = Color.blue; // Blue for left hand stream
            ToggleMenuButton(false);
            WristTracker.SetActive(true);
            ShouldContinueArmTeleop = true;
        }

        // Switching from Left hand control
        if (LeftHand.GetFingerIsPinching(OVRHand.HandFinger.Index))
        {
            StreamRelativeData = true;
            StreamAbsoluteData = false;
            StreamBorder.color = Color.green; // Green for right hand stream
            ToggleMenuButton(false);
            WristTracker.SetActive(false);
            ShouldContinueArmTeleop = false;
        }

        // Pausing Stream
        if (LeftHand.GetFingerIsPinching(OVRHand.HandFinger.Ring))
        {
            StreamRelativeData = false;
            StreamAbsoluteData = false;
            StreamBorder.color = Color.red; // Red color for no stream 
            ToggleMenuButton(true);
            WristTracker.SetActive(false);
            ShouldContinueArmTeleop = false;
        }
    }

    void Update()
    {
        // Early return if essential components are missing
        if (RightHandFingerBones == null || LeftHandFingerBones == null) {
            Debug.LogWarning("Hand finger bones not initialized!");
            return;
        }

        if (connectionEstablished && leftConnectionEstablished)
        {   
            SendResolution();
            SendResetStatus();
            if (String.Equals(communicationAddress, netConfig.getRightKeypointAddress()) &&
                String.Equals(leftCommunicationAddress, netConfig.getLeftKeypointAddress()))
            {   
                StreamPauser();

                if (StreamAbsoluteData)
                {   
                    SendRightHandData("absolute");
                    SendLeftHandData("absolute");
                    
                    // IMPORTANT: Only call if ResolutionButton and LineRenderer exist
                    if (ResolutionButton != null && LineRenderer != null)
                    {
                        ToggleResolutionButton(false);
                    }
                }

                if (StreamRelativeData)
                {
                    SendRightHandData("relative");
                    SendLeftHandData("relative");
                    
                    // IMPORTANT: Only call if ResolutionButton and LineRenderer exist
                    if (ResolutionButton != null && LineRenderer != null)
                    {
                        ToggleResolutionButton(false);
                    }
                }
            }
            else
            {
                connectionEstablished = false;
                leftConnectionEstablished = false;
            }
        } 
        else
        {
            StreamBorder.color = Color.red;
            ToggleMenuButton(true);
            CreateTCPConnection();
        }
    }

    // Add this public method to check connection status
    public bool AreAllConnectionsEstablished()
    {
        return connectionEstablished || leftConnectionEstablished;
    }

    // Add this method to respond to disconnect message
    public void DisconnectNetMQ()
    {
        CleanupSockets();
        connectionEstablished = false;
        leftConnectionEstablished = false;
        resolutionconnectionEstablished = false;
        PauseEstablished = false;
        
        // Update UI
        StreamBorder.color = Color.red;
        ToggleMenuButton(true);
    }

    // Add this method to respond to connect message  
    public void ConnectNetMQ()
    {
        if (!connectionEstablished && !leftConnectionEstablished)
        {
            CreateTCPConnection();
        }
    }

    // Enhanced keep-alive method to ping all active connections
    public void SendKeepAlivePing()
    {
        try
        {
            // Ping main hand tracking sockets
            if (connectionEstablished)
            {
                client.SendFrame("ping");
                Debug.Log("Sent keep-alive ping to right hand socket");
            }
            
            if (leftConnectionEstablished)
            {
                leftClient.SendFrame("ping");
                Debug.Log("Sent keep-alive ping to left hand socket");
            }
            
            // Ping resolution socket
            if (resolutionconnectionEstablished && resolutioncreated && client2 != null)
            {
                client2.SendFrame("ping");
                Debug.Log("Sent keep-alive ping to resolution socket");
            }
            
            // Ping pause/control socket
            if (PauseEstablished && PauseCreated && client3 != null)
            {
                client3.SendFrame("ping");
                Debug.Log("Sent keep-alive ping to pause/control socket");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending keep-alive ping: " + e.Message);
            // Connection might be broken, reset status to force reconnection
            connectionEstablished = false;
            leftConnectionEstablished = false;
            resolutionconnectionEstablished = false;
            PauseEstablished = false;
        }
    }

    void OnApplicationQuit()
    {
        CleanupSockets();
    }

    void OnDestroy()
    {
        CleanupSockets();
    }

    private void CleanupSockets()
    {
        Debug.Log("Cleaning up NetMQ sockets...");
        
        // Clean up all sockets
        if (client != null)
        {
            client.Close();
            client.Dispose();
            client = null;
        }
        
        if (leftClient != null)
        {
            leftClient.Close();
            leftClient.Dispose();
            leftClient = null;
        }
        
        if (client2 != null)
        {
            client2.Close();
            client2.Dispose();
            client2 = null;
        }
        
        if (client3 != null)
        {
            client3.Close();
            client3.Dispose();
            client3 = null;
        }
        
        // This is critical - it terminates all NetMQ background threads
        NetMQConfig.Cleanup(false);
    }
}
