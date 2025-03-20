using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System;

public class NetworkKeepAlive : MonoBehaviour
{
    [SerializeField] private float ping_interval = 5.0f; // Seconds between pings
    private NetworkManager netManager;
    private GestureDetector gestureDetector;
    private bool is_running = false;
    
    void Start()
    {
        // Get references
        GameObject netConfGame = GameObject.Find("NetworkConfigsLoader");
        if (netConfGame != null)
        {
            netManager = netConfGame.GetComponent<NetworkManager>();
        }
        
        GameObject detector = GameObject.Find("GestureDetector");
        if (detector != null)
        {
            gestureDetector = detector.GetComponent<GestureDetector>();
        }
        
        // Start the ping coroutine
        StartCoroutine(PingRoutine());
    }
    
    private IEnumerator PingRoutine()
    {
        is_running = true;
        
        while (is_running)
        {
            // Wait for the specified interval
            yield return new WaitForSeconds(ping_interval);
            
            // Check if connections exist and send keep-alive
            if (gestureDetector != null && gestureDetector.AreAllConnectionsEstablished())
            {
                SendKeepAlive();
            }
        }
    }
    
    private void SendKeepAlive()
    {
        try
        {
            Debug.Log("Sending keep-alive ping");
            
            if (gestureDetector != null)
            {
                gestureDetector.SendKeepAlivePing();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Keep-alive error: " + e.Message);
        }
    }
    
    // Call this method from GestureDetector once you add it
    public void SendPingToServer()
    {
        // Implement in GestureDetector to access your TCP client(s)
        if (gestureDetector != null)
        {
            // gestureDetector.SendEmptyPacketToServer();
        }
    }
    
    void OnDestroy()
    {
        is_running = false;
    }
} 