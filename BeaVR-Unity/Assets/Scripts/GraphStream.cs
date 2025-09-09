using UnityEngine;
using UnityEngine.UI;

using NetMQ;
using NetMQ.Sockets;

using System;
using System.Collections.Generic;
using System.Threading;

public class GraphStream : MonoBehaviour
{
    private Thread graphStreamer;
    private static List<byte[]> graphList;

    public RawImage image;
    private Texture2D texture;

    private bool connectionEstablished = false;
    private string communicationAddress;
    private NetworkManager netConfig;
    private SubscriberSocket socket;

    private void StartGraphThread()
    {
        // Check if communication address is available
        communicationAddress = netConfig.getGraphAddress();
        bool AddressAvailable = !String.Equals(communicationAddress, "tcp://:");
        
        if (AddressAvailable)
        {
            StartConnection();
            graphList = new List<byte[]>();
            graphStreamer = new Thread(getGraphImage);
            graphStreamer.Start();
        }
    }

    public void StartConnection()
    {
        // Initiate Subscriber Socket
        socket = new SubscriberSocket();
        socket.Options.ReceiveHighWatermark = 1000;
        socket.Connect(communicationAddress);
        socket.Subscribe("");
        connectionEstablished = true;
    }

    private void getGraphImage()
    {
        try
        {
            while (true)
            {
                if (socket == null) break;
                
                byte[] imageBytes = socket.ReceiveFrameBytes();
                
                if (graphList != null)
                {
                    graphList.Add(imageBytes);
        
                    if (graphList.Count > 2)
                    {
                        graphList.RemoveAt(0);
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
            Debug.LogError("Graph thread error: " + e.Message);
        }
    }

    public void Start()
    {
        // Getting the Network Config Updater gameobject
        GameObject netConfGame = GameObject.Find("NetworkConfigsLoader");
        netConfig = netConfGame.GetComponent<NetworkManager>();

        // Initializing the image texture
        texture = new Texture2D(640, 360, TextureFormat.RGB24, false);
        image.texture = texture;
    }

    public void Update()
    {
        if (connectionEstablished)
        {
            // To check if the same IP is being used
            if (String.Equals(communicationAddress, netConfig.getGraphAddress()))
            {
                // Check if the list has any elements before trying to access them
                if (graphList != null && graphList.Count > 0)
                {
                    // Getting the image from the queue and displaying it
                    byte[] imageBytes = graphList[graphList.Count - 1];
                    texture.LoadImage(imageBytes);
                }
                else
                {
                    // Log for debugging
                    Debug.Log("GraphStream: Graph list is empty or null");
                }
            }
            else
            {
                // Aborting the queue
                if (graphStreamer != null)
                {
                    graphStreamer.Abort();
                }
                connectionEstablished = false;
            }
        }
        else
        {
            StartGraphThread();
        }
    }

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
        if (graphStreamer != null && graphStreamer.IsAlive)
        {
            try
            {
                graphStreamer.Abort();
                graphStreamer = null;
            }
            catch (Exception e)
            {
                Debug.LogError("Error stopping graph thread: " + e.Message);
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
                Debug.LogError("Error closing graph socket: " + e.Message);
            }
        }
        
        connectionEstablished = false;
    }

    public void ConnectNetMQ()
    {
        // Only reconnect if we're not already connected
        if (!connectionEstablished)
        {
            StartGraphThread();
        }
    }
}
