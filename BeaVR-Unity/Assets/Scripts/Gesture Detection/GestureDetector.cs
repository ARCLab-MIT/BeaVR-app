using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

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

    // Add this at the top of your script to track network state
    private bool socketInitialized = false;
    private DateTime lastConnectionAttempt = DateTime.MinValue;
    private TimeSpan connectionRetryInterval = TimeSpan.FromSeconds(5);

    // Add StreamResolution variable to class definition
    bool StreamResolution = false;  // Default to false, matches your original code style

    // Starting the server connection
    public void CreateTCPConnection()
    {
        try
        {
            Debug.Log("Attempting to create TCP connections...");
            
            // Original right hand connection
            communicationAddress = netConfig.getRightKeypointAddress();
            bool addressAvailable = !String.Equals(communicationAddress, "tcp://:");
            Debug.Log("Right hand address: " + communicationAddress + ", available: " + addressAvailable);

            // New left hand connection
            leftCommunicationAddress = netConfig.getLeftKeypointAddress();
            bool leftAddressAvailable = !String.Equals(leftCommunicationAddress, "tcp://:");
            Debug.Log("Left hand address: " + leftCommunicationAddress + ", available: " + leftAddressAvailable);

            // Close existing sockets first to prevent resource conflicts
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

            if (addressAvailable)
            {
                client = new PushSocket();
                client.Connect(communicationAddress);
                connectionEstablished = true;
                Debug.Log("Right hand connection established!");
            }

            if (leftAddressAvailable)
            {
                leftClient = new PushSocket();
                leftClient.Connect(leftCommunicationAddress);
                leftConnectionEstablished = true;
                Debug.Log("Left hand connection established!");
            }

            // Setting color to green to indicate control
            if (connectionEstablished || leftConnectionEstablished)
            {
                StreamBorder.color = Color.green;
                ToggleMenuButton(false);
                // Send a test frame to verify connection
                if (connectionEstablished) client.SendFrame("ping");
                if (leftConnectionEstablished) leftClient.SendFrame("ping");
                Debug.Log("Connection indicators updated - status OK");
            }
            else
            {
                StreamBorder.color = Color.red;
                ToggleMenuButton(true);
                Debug.Log("Failed to establish connections");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error creating TCP connections: " + e.Message);
            Debug.LogException(e);
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

        // Make sure NetMQController is initialized
        StartCoroutine(InitializeNetMQAfterDelay());
    }

    IEnumerator InitializeNetMQAfterDelay()
    {
        // Wait a moment for everything to initialize
        yield return new WaitForSeconds(2f);
        
        // Create all sockets through the controller
        NetMQController.Instance.CreateStandardSockets();
        
        // Run diagnostic tests
        NetMQController.Instance.PerformDiagnosticTests();
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
            if (client != null && connectionEstablished)
            {
                // Getting bone positional information
                List<Vector3> rightHandGestureData = new List<Vector3>();
                foreach (var bone in RightHandFingerBones)
                {
                    Vector3 bonePosition = bone.Transform.position;
                    rightHandGestureData.Add(bonePosition);
                }

                // IMPORTANT: Format MUST match Python's expected format exactly
                // Format: <hand>:x,y,z|x,y,z|x,y,z
                StringBuilder sb = new StringBuilder(TypeMarker);
                sb.Append(":");
                
                bool firstVector = true;
                foreach (Vector3 v in rightHandGestureData)
                {
                    if (!firstVector)
                        sb.Append("|");
                        
                    sb.Append(v.x.ToString("F6")).Append(",")
                      .Append(v.y.ToString("F6")).Append(",")
                      .Append(v.z.ToString("F6"));
                      
                    firstVector = false;
                }
                
                string dataString = sb.ToString();
                Debug.Log($"Sending right hand data: {dataString.Substring(0, Math.Min(50, dataString.Length))}...");
                client.SendFrame(dataString);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending right hand data: " + e.Message);
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

    private float diagTimer = 0f;

    void Update()
    {
        // Check connection state first
        bool isConnected = NetMQController.Instance.AreSocketsConnected();
        
        // If not connected, show menu and red border
        if (!isConnected)
        {
            StreamBorder.color = Color.red;
            ToggleMenuButton(true);
            WristTracker.SetActive(false);
            
            // We don't create connections here, just check if address is available
            // to determine if we should attempt connection
            string ipAddress = netConfig.netConfig.IPAddress;
            if (!string.IsNullOrEmpty(ipAddress) && ipAddress != "undefined")
            {
                // Only try to initialize if we haven't already initiated a connection attempt
                if (!connectionAttemptInProgress)
                {
                    Debug.Log("IP is configured, attempting connection...");
                    connectionAttemptInProgress = true;
                    StartCoroutine(AttemptConnection());
                }
            }
            return;
        }
        
        // IP is defined and sockets are connected, continue with normal operation
        connectionAttemptInProgress = false;
        
        // Process finger gestures
        StreamPauser();
        
        // Send data based on current mode
        SendResolutionThroughController();
        SendPauseStatusThroughController();
        
        if (StreamAbsoluteData)
        {   
            SendHandDataThroughController("absolute");
            ToggleResolutionButton(false);
        }
        else if (StreamRelativeData)
        {
            SendHandDataThroughController("relative");
            ToggleResolutionButton(false);
        }
        else if (StreamResolution)
        {   
            ToggleHighResolutionButton(true);
            ToggleLowResolutionButton(true);
        }
    }

    // Coroutine to attempt connection through NetMQController
    private bool connectionAttemptInProgress = false;
    IEnumerator AttemptConnection()
    {
        // Request connection from NetMQController
        NetMQController.Instance.Connect(
            netConfig.netConfig.IPAddress,  // Direct access to IP address 
            netConfig.getRightKeypointAddress(),
            netConfig.getLeftKeypointAddress(),
            netConfig.getResolutionAddress(),
            netConfig.getPauseAddress()
        );
        
        // Wait for connection attempt
        yield return new WaitForSeconds(2f);
        
        // Update UI based on result
        bool success = NetMQController.Instance.AreSocketsConnected();
        if (success)
        {
            StreamBorder.color = Color.green;
            ToggleMenuButton(false);
            Debug.Log("Connection successful!");
        }
        else
        {
            StreamBorder.color = Color.red;
            ToggleMenuButton(true);
            Debug.Log("Connection failed!");
        }
        
        connectionAttemptInProgress = false;
    }

    // Send hand data through the controller - fixed names and data format
    private void SendHandDataThroughController(string typeMarker)
    {
        try
        {
            // Getting bone positional information for right hand
            List<Vector3> rightHandGestureData = new List<Vector3>();
            foreach (var bone in RightHandFingerBones)
            {
                Vector3 bonePosition = bone.Transform.position;
                rightHandGestureData.Add(bonePosition);
            }

            // Create string data
            string rightHandDataString = SerializeVector3List(rightHandGestureData);
            rightHandDataString = typeMarker + ":" + rightHandDataString;

            // Send via controller
            NetMQController.Instance.SendMessage("RightHand", rightHandDataString);
            
            // Getting bone positional information for left hand if available
            if (LeftHandFingerBones != null && LeftHandFingerBones.Count > 0)
            {
                List<Vector3> leftHandGestureData = new List<Vector3>();
                foreach (var bone in LeftHandFingerBones)
                {
                    Vector3 bonePosition = bone.Transform.position;
                    leftHandGestureData.Add(bonePosition);
                }

                // Create string data
                string leftHandDataString = SerializeVector3List(leftHandGestureData);
                leftHandDataString = typeMarker + ":" + leftHandDataString;

                // Send via controller
                NetMQController.Instance.SendMessage("LeftHand", leftHandDataString);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending hand data: " + e.Message);
        }
    }

    // Send resolution data through the controller with correct variable names
    private void SendResolutionThroughController()
    {
        try {
            string state = "None";
            
            if (HighResolutionButtonController != null && HighResolutionButtonController.HighResolution)
            {
                state = "High";
                Debug.Log("High Resolution Button was clicked!");
            }
            else if (LowResolutionButtonController != null && LowResolutionButtonController.LowResolution)
            {
                state = "Low";
                Debug.Log("Low Resolution Button was clicked!");
            }
            
            NetMQController.Instance.SendMessage("Resolution", state);
        }
        catch (Exception e) {
            Debug.LogError("Error sending resolution data: " + e.Message);
        }
    }

    // Send pause status through the controller
    private void SendPauseStatusThroughController()
    {
        try {
            string pauseState = ShouldContinueArmTeleop ? "High" : "Low";
            NetMQController.Instance.SendMessage("Pause", pauseState);
        }
        catch (Exception e) {
            Debug.LogError("Error sending pause status: " + e.Message);
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

    private void DiagnoseDataTransmission()
    {
        try
        {
            // Test message
            string testMessage = "DIAGNOSE_TEST_" + DateTime.Now.ToString("HH:mm:ss.fff");
            
            // Try sending to all sockets
            bool rightSent = false;
            bool leftSent = false;
            bool resSent = false;
            bool pauseSent = false;
            
            if (client != null)
            {
                client.SendFrame(testMessage);
                rightSent = true;
            }
            
            if (leftClient != null)
            {
                leftClient.SendFrame(testMessage);
                leftSent = true;
            }
            
            if (client2 != null)
            {
                client2.SendFrame(testMessage);
                resSent = true;
            }
            
            if (client3 != null)
            {
                client3.SendFrame(testMessage);
                pauseSent = true;
            }
            
            Debug.Log($"DIAGNOSTIC SEND - Right:{rightSent} Left:{leftSent} Res:{resSent} Pause:{pauseSent}");
        }
        catch (Exception e)
        {
            Debug.LogError("Diagnostic send error: " + e.Message);
        }
    }
}
