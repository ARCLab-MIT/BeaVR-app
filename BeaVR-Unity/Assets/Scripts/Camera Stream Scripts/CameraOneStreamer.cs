using UnityEngine;
using UnityEngine.UI;

using NetMQ;
using NetMQ.Sockets;

using System;
using System.Collections.Generic;
using System.Threading;

public class CameraOneStreamer : MonoBehaviour
{
    private Thread imageStreamer;
    private static List<byte[]> imageList;

    public RawImage image;
    private Texture2D texture;

    //public NetworkConfigs netConf;
    private bool connectionEstablished = false;
    private string communicationAddress;
    private NetworkManager netConfig;
    private SubscriberSocket socket;

    private void StartImageThread()
    {
        try
        {
            // Check if communication address is available and not forced to disconnect
            communicationAddress = netConfig.getCamAddress();
            bool AddressAvailable = !String.Equals(communicationAddress, "tcp://:");
            
            if (AddressAvailable && !netConfig.ForceDisconnect)
            {
                StartConnection();
                imageList = new List<byte[]>();
                imageStreamer = new Thread(getRobotImage);
                imageStreamer.Start();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error starting camera thread: " + e.Message);
        }
    }

    public void StartConnection()
    {
        try
        {
            // Clean up any existing socket first
            if (socket != null)
            {
                socket.Close();
                socket.Dispose();
            }
            
            // Initiate Subscriber Socket
            socket = new SubscriberSocket();
            socket.Options.ReceiveHighWatermark = 1000;
            socket.Connect(communicationAddress);
            socket.Subscribe("");
            connectionEstablished = true;
            Debug.Log("Camera connection established to: " + communicationAddress);
        }
        catch (Exception e)
        {
            Debug.LogError("Error establishing camera connection: " + e.Message);
            connectionEstablished = false;
        }
    }

    private void getRobotImage()
    {
        try
        {
            while (true)
            {
                // Exit thread if socket is null or Component is disabled
                if (socket == null || !enabled) break;
                
                byte[] imageBytes = socket.ReceiveFrameBytes();
                
                if (imageList != null)
                {
                    imageList.Add(imageBytes);
                    
                    if (imageList.Count > 5)
                    {
                        imageList.RemoveAt(0);
                    }
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Camera thread error: " + e.Message);
        }
    }

    public void Start()
    {
        // Getting the Network Config Updater gameobject
        GameObject netConfGame = GameObject.Find("NetworkConfigsLoader");
        if (netConfGame != null)
        {
            netConfig = netConfGame.GetComponent<NetworkManager>();
        }
        else
        {
            Debug.LogError("NetworkConfigsLoader not found!");
            return;
        }

        // Initializing the image texture
        texture = new Texture2D(640, 360, TextureFormat.RGB24, false);
        image.texture = texture;
    }

    public void Update()
    {
        if (connectionEstablished)
        {
            // Check if network manager is forcing disconnect
            if (netConfig.ForceDisconnect)
            {
                DisconnectNetMQ();
                return;
            }
            
            // To check if the same IP is being used
            if (String.Equals(communicationAddress, netConfig.getCamAddress()))
            {
                // Check if the list has any elements before trying to access them
                if (imageList != null && imageList.Count > 0)
                {
                    try
                    {
                        // Getting the image from the queue and displaying it
                        byte[] imageBytes = imageList[imageList.Count - 1];
                        texture.LoadImage(imageBytes);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Error updating camera texture: " + e.Message);
                    }
                }
            }
            else
            {
                // Address changed, disconnect and reconnect
                DisconnectNetMQ();
            }
        }
        else if (!netConfig.ForceDisconnect)
        {
            StartImageThread();
        }
    }
    
    // Add these methods for NetworkManager integration
    void OnDestroy()
    {
        DisconnectNetMQ();
    }

    void OnApplicationQuit()
    {
        DisconnectNetMQ();
    }

    public void DisconnectNetMQ()
    {
        // Safely stop the thread
        if (imageStreamer != null && imageStreamer.IsAlive)
        {
            try
            {
                imageStreamer.Abort();
                imageStreamer = null;
            }
            catch (Exception e)
            {
                Debug.LogError("Error stopping camera thread: " + e.Message);
            }
        }
        
        // Close socket
        if (socket != null)
        {
            try
            {
                socket.Close();
                socket.Dispose();
                socket = null;
            }
            catch (Exception e)
            {
                Debug.LogError("Error closing camera socket: " + e.Message);
            }
        }
        
        connectionEstablished = false;
        Debug.Log("Camera connection closed");
    }

    public void ConnectNetMQ()
    {
        // Only reconnect if we're not already connected
        if (!connectionEstablished)
        {
            StartImageThread();
        }
    }
}