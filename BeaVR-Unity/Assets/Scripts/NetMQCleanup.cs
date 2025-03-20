using UnityEngine;
using NetMQ;
using System;

public class NetMQCleanup : MonoBehaviour
{
    private static bool hasCleanedUp = false;
    
    public static void SafeCleanup()
    {
        try 
        {
            if (!hasCleanedUp)
            {
                Debug.Log("NetMQ: Performing global cleanup");
                NetMQConfig.Cleanup(false);
                hasCleanedUp = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("NetMQ cleanup error: " + e.Message);
        }
    }
    
    void OnApplicationQuit()
    {
        SafeCleanup();
    }
    
    void OnDestroy()
    {
        SafeCleanup();
    }
} 