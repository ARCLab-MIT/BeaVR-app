using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Central controller for all NetMQ socket operations.
/// Manages initialization, socket creation, and cleanup.
/// </summary>
public class NetMQController : MonoBehaviour
{
    private static NetMQController _instance;
    public static NetMQController Instance 
    {
        get 
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("NetMQController");
                _instance = go.AddComponent<NetMQController>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Socket references
    private Dictionary<string, PushSocket> sockets = new Dictionary<string, PushSocket>();
    private Dictionary<string, bool> socketConnectionStatus = new Dictionary<string, bool>();
    
    // Network settings from JSON
    private string ipAddress;
    private string rightKeypointPort;
    private string leftKeypointPort;
    private string resolutionPort;
    private string pausePort;
    
    // Initialization flags
    private bool netMQInitialized = false;
    
    // Add this at class level
    private float lastLogTime = 0f;
    
    private Dictionary<string, int> socketFailCounts = new Dictionary<string, int>();
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize NetMQ early
        InitializeNetMQ();
        
        // Load network configuration
        LoadNetworkConfig();
    }
    
    /// <summary>
    /// Load network configuration from JSON file
    /// </summary>
    private void LoadNetworkConfig()
    {
        try
        {
            // Load JSON from Resources folder
            TextAsset configFile = Resources.Load<TextAsset>("Configurations/Network");
            if (configFile == null)
            {
                Debug.LogError("NetMQController: Failed to load Network.json");
                return;
            }
            
            // Parse JSON
            var configJson = JsonUtility.FromJson<NetworkSettings>(configFile.text);
            
            // Store configuration values
            ipAddress = configJson.IPAddress;
            rightKeypointPort = configJson.rightkeyptPortNum;
            leftKeypointPort = configJson.leftkeyptPortNum;
            resolutionPort = configJson.resolutionPortNum;
            pausePort = configJson.PausePortNum;
            
            Debug.Log($"NetMQController: Network config loaded - IP: {ipAddress}");
        }
        catch (Exception e)
        {
            Debug.LogError($"NetMQController: Error loading network config - {e.Message}");
        }
    }
    
    /// <summary>
    /// Initialize the NetMQ system
    /// </summary>
    public void InitializeNetMQ()
    {
        try
        {
            if (!netMQInitialized)
            {
                Debug.Log("NetMQController: Initializing NetMQ...");
                
                // Use the recommended approach instead of the obsolete ManualTerminationTakeOver
                // This ensures NetMQ is properly initialized for the current thread context
                AsyncIO.ForceDotNet.Force();
                
                // Mark as initialized
                netMQInitialized = true;
                Debug.Log("NetMQController: NetMQ initialized successfully");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"NetMQController: Error initializing NetMQ - {e.GetType().Name}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Create a socket with the given name and address
    /// </summary>
    public bool CreateSocket(string socketName, string address)
    {
        try
        {
            if (sockets.ContainsKey(socketName))
            {
                // Socket with this name already exists
                Debug.LogWarning($"NetMQController: Socket '{socketName}' already exists");
                return true;
            }
            
            // Create new socket
            Debug.Log($"NetMQController: Creating socket '{socketName}' at {address}");
            PushSocket socket = new PushSocket();
            socket.Connect(address);
            
            // Store socket
            sockets[socketName] = socket;
            socketConnectionStatus[socketName] = true;
            
            Debug.Log($"NetMQController: Socket '{socketName}' created and connected to {address}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"NetMQController: Error creating socket '{socketName}' - {e.GetType().Name}: {e.Message}");
            socketConnectionStatus[socketName] = false;
            return false;
        }
    }
    
    /// <summary>
    /// Create standard sockets based on network configuration
    /// </summary>
    public void CreateStandardSockets()
    {
        try
        {
            Debug.Log("NetMQController: Creating standard sockets...");
            
            // Check if IP is undefined, skip socket creation if so
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "undefined")
            {
                Debug.LogWarning("NetMQController: IP Address is undefined. Connection must be established manually.");
                return;
            }
            
            // Create right hand socket
            string rightHandAddress = $"tcp://{ipAddress}:{rightKeypointPort}";
            CreateSocket("RightHand", rightHandAddress);
            
            // Create left hand socket
            string leftHandAddress = $"tcp://{ipAddress}:{leftKeypointPort}";
            CreateSocket("LeftHand", leftHandAddress);
            
            // Create resolution socket
            string resolutionAddress = $"tcp://{ipAddress}:{resolutionPort}";
            CreateSocket("Resolution", resolutionAddress);
            
            // Create pause socket
            string pauseAddress = $"tcp://{ipAddress}:{pausePort}";
            CreateSocket("Pause", pauseAddress);
            
            // Log socket status
            LogSocketStatus();
        }
        catch (Exception e)
        {
            Debug.LogError($"NetMQController: Error creating standard sockets - {e.Message}");
        }
    }
    
    /// <summary>
    /// Send a message through a named socket with timeout protection
    /// </summary>
    public bool SendMessage(string socketName, string message)
    {
        try
        {
            if (!sockets.ContainsKey(socketName))
            {
                return false;
            }

            var socket = sockets[socketName];
            if (socket == null)
            {
                return false;
            }

            // Add timeout protection
            bool sent = socket.TrySendFrame(TimeSpan.FromMilliseconds(10), message);
            
            if (!sent)
            {
                // If send times out, mark this socket as potentially disconnected
                socketFailCounts[socketName] = socketFailCounts.GetValueOrDefault(socketName, 0) + 1;
                
                // If we've failed multiple times, try to reconnect this socket
                if (socketFailCounts[socketName] > 5)
                {
                    Debug.LogWarning($"Socket {socketName} has failed multiple times. Attempting reconnection...");
                    ReconnectSocket(socketName);
                    socketFailCounts[socketName] = 0;
                }
                return false;
            }
            
            // Reset fail count on success
            socketFailCounts[socketName] = 0;
            
            // Occasional logging
            if (Time.time - lastLogTime > 1.0f)
            {
                lastLogTime = Time.time;
                Debug.Log($"NetMQController: Sent message to '{socketName}'");
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"NetMQController: Error sending message to '{socketName}' - {e.Message}");
            socketFailCounts[socketName] = socketFailCounts.GetValueOrDefault(socketName, 0) + 1;
            
            // If exception keeps happening, try to reconnect
            if (socketFailCounts[socketName] > 3)
            {
                ReconnectSocket(socketName);
                socketFailCounts[socketName] = 0;
            }
            return false;
        }
    }
    
    /// <summary>
    /// Close all sockets
    /// </summary>
    public void CloseAllSockets()
    {
        foreach (var socketName in new List<string>(sockets.Keys))
        {
            CloseSocket(socketName);
        }
        
        sockets.Clear();
        socketConnectionStatus.Clear();
    }
    
    /// <summary>
    /// Close and dispose a specific socket
    /// </summary>
    public void CloseSocket(string socketName)
    {
        try
        {
            if (!sockets.ContainsKey(socketName))
            {
                Debug.LogWarning($"NetMQController: Socket '{socketName}' does not exist");
                return;
            }
            
            PushSocket socket = sockets[socketName];
            
            if (socket != null)
            {
                socket.Close();
                socket.Dispose();
                Debug.Log($"NetMQController: Socket '{socketName}' closed and disposed");
            }
            
            sockets.Remove(socketName);
            socketConnectionStatus.Remove(socketName);
        }
        catch (Exception e)
        {
            Debug.LogError($"NetMQController: Error closing socket '{socketName}' - {e.GetType().Name}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Log the status of all sockets
    /// </summary>
    public void LogSocketStatus()
    {
        Debug.Log("===== NETMQ SOCKET STATUS =====");
        Debug.Log($"IP Address: {ipAddress}");
        
        if (sockets.Count == 0)
        {
            Debug.Log("No sockets created");
        }
        else
        {
            foreach (var socketName in sockets.Keys)
            {
                Debug.Log($"Socket: {socketName} - Connected: {socketConnectionStatus[socketName]}");
            }
        }
        
        Debug.Log("===============================");
    }
    
    /// <summary>
    /// Perform cleanup when the application quits
    /// </summary>
    private void OnApplicationQuit()
    {
        CleanupNetMQ();
    }
    
    /// <summary>
    /// Cleanup NetMQ resources
    /// </summary>
    public void CleanupNetMQ()
    {
        try
        {
            // Close all sockets first
            CloseAllSockets();
            
            // Then clean up NetMQ
            if (netMQInitialized)
            {
                Debug.Log("NetMQController: Cleaning up NetMQ...");
                NetMQConfig.Cleanup(false);
                netMQInitialized = false;
                Debug.Log("NetMQController: NetMQ cleaned up");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"NetMQController: Error cleaning up NetMQ - {e.GetType().Name}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Perform diagnostic tests by sending test messages
    /// </summary>
    public bool PerformDiagnosticTests()
    {
        Debug.Log("NetMQController: Starting diagnostic tests...");
        bool allSuccessful = true;
        
        // If sockets are empty, likely IP was undefined
        if (sockets.Count == 0)
        {
            Debug.LogWarning("NetMQController: No sockets available for diagnostic tests");
            return false;
        }
        
        // Test each socket
        foreach (var socketName in sockets.Keys)
        {
            string testMsg = $"DIAGNOSTIC_TEST_{socketName}_{DateTime.Now:HH:mm:ss.fff}";
            bool success = SendMessage(socketName, testMsg);
            
            Debug.Log($"NetMQController: Diagnostic test for '{socketName}' - Success: {success}");
            
            if (!success)
            {
                allSuccessful = false;
            }
        }
        
        Debug.Log($"NetMQController: Diagnostic tests completed - Overall success: {allSuccessful}");
        return allSuccessful;
    }
    
    /// <summary>
    /// Check if NetMQ is initialized
    /// </summary>
    public bool IsInitialized()
    {
        return netMQInitialized;
    }

    /// <summary>
    /// Connect to all sockets using provided configuration
    /// </summary>
    public void Connect(string ipAddress, string rightHandAddress, string leftHandAddress, 
                       string resolutionAddress, string pauseAddress)
    {
        // Store the IP address
        this.ipAddress = ipAddress;
        
        // Close any existing sockets
        CloseAllSockets();
        
        // Initialize NetMQ if needed
        if (!netMQInitialized)
        {
            InitializeNetMQ();
        }
        
        // Create sockets with full addresses provided
        if (!string.IsNullOrEmpty(rightHandAddress) && rightHandAddress != "tcp://:")
            CreateSocket("RightHand", rightHandAddress);
        
        if (!string.IsNullOrEmpty(leftHandAddress) && leftHandAddress != "tcp://:")
            CreateSocket("LeftHand", leftHandAddress);
        
        if (!string.IsNullOrEmpty(resolutionAddress) && resolutionAddress != "tcp://:")
            CreateSocket("Resolution", resolutionAddress);
        
        if (!string.IsNullOrEmpty(pauseAddress) && pauseAddress != "tcp://:")
            CreateSocket("Pause", pauseAddress);
        
        // Log socket status
        LogSocketStatus();
        
        // Test connections
        PerformDiagnosticTests();
    }

    /// <summary>
    /// Check if all required sockets are connected
    /// </summary>
    public bool AreSocketsConnected()
    {
        // If IP is undefined, we're not connected
        if (string.IsNullOrEmpty(ipAddress) || ipAddress == "undefined")
            return false;
        
        // Check if we have the minimum required sockets
        bool hasRightHand = sockets.ContainsKey("RightHand") && sockets["RightHand"] != null;
        bool hasLeftHand = sockets.ContainsKey("LeftHand") && sockets["LeftHand"] != null;
        
        return hasRightHand && hasLeftHand;
    }

    /// <summary>
    /// Attempt to reconnect a specific socket
    /// </summary>
    private void ReconnectSocket(string socketName)
    {
        try
        {
            Debug.Log($"Attempting to reconnect socket: {socketName}");
            
            // Close the existing socket
            if (sockets.ContainsKey(socketName) && sockets[socketName] != null)
            {
                sockets[socketName].Close();
                sockets[socketName].Dispose();
            }
            
            // Determine the address based on socket type
            string address = "";
            switch (socketName)
            {
                case "RightHand":
                    address = $"tcp://{ipAddress}:{rightKeypointPort}";
                    break;
                case "LeftHand":
                    address = $"tcp://{ipAddress}:{leftKeypointPort}";
                    break;
                case "Resolution":
                    address = $"tcp://{ipAddress}:{resolutionPort}";
                    break;
                case "Pause":
                    address = $"tcp://{ipAddress}:{pausePort}";
                    break;
                default:
                    Debug.LogError($"Unknown socket type: {socketName}");
                    return;
            }
            
            // Create new socket
            var socket = new PushSocket();
            socket.Options.SendHighWatermark = 1000;
            socket.Options.Linger = TimeSpan.FromMilliseconds(100);
            socket.Connect(address);
            
            // Replace in dictionary
            sockets[socketName] = socket;
            
            Debug.Log($"Socket {socketName} reconnected to {address}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error reconnecting socket {socketName}: {e.Message}");
            // Mark as broken but don't throw
            if (sockets.ContainsKey(socketName))
            {
                sockets[socketName] = null;
            }
        }
    }
}

/// <summary>
/// Class to deserialize network settings from JSON
/// </summary>
[Serializable]
public class NetworkSettings
{
    public string IPAddress;
    public string rightkeyptPortNum;
    public string leftkeyptPortNum;
    public string camPortNum;
    public string graphPortNum;
    public string resolutionPortNum;
    public string PausePortNum;
    public string LeftPausePortNum;
    public string RightPausePortNum;
} 