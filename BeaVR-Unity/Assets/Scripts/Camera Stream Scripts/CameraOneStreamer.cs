using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WebRTC video receiver that replaces the previous NetMQ JPEG stream.
/// Signaling happens via ZMQ REQ/REP against the Python server; media
/// flows over WebRTC and renders into a RawImage.
/// Config comes from Resources/Configurations/Network.json:
/// - webrtcSignalingPort + IPAddress build tcp://host:port for ZMQ REQ
/// - webrtcClientId identifies this client to the Python server
/// - webrtcVerboseLogs gates all Debug.Log noise for troubleshooting
/// </summary>
public class CameraOneStreamer : MonoBehaviour
{
    [Header("UI")]
    public RawImage image;

    [Header("WebRTC Settings")]
    [SerializeField] private int signalingTimeoutMs = 15000;
    [SerializeField] private int videoWidth = 640;
    [SerializeField] private int videoHeight = 360;
    [SerializeField] private bool autoConnectOnStart = true;
    private NetworkManager netConfig;
    private WebRTCSignalingClient signalingClient;
    private RTCPeerConnection peerConnection;
    private VideoStreamTrack currentVideoTrack;
    private Texture currentTexture;
    private CancellationTokenSource connectionCts;
    private bool connectionEstablished;
    private bool isConnecting;
    private Texture2D placeholderTexture;
    private SynchronizationContext unitySync;
    private static bool webRtcInitialized;
    private int frameCount = 0;
    private float lastFrameTime = 0f;

    private static CameraOneStreamer _instance;

    private void Awake()
    {
        // Singleton pattern: prevent duplicate instances
        if (_instance != null && _instance != this)
        {
            Debug.LogError($"Multiple CameraOneStreamer instances detected! Destroying duplicate on {gameObject.name}.");
            Destroy(this);
            return;
        }
        _instance = this;

        // Explicitly initialize WebRTC; prefer software path when available to run on Quest without GPU decode.
        // Use reflection so the code compiles against older/newer com.unity.webrtc versions that may not expose EncoderType/Dispose.
        if (!webRtcInitialized)
        {
            InitializeWebRTCWithFallback();
            webRtcInitialized = true;
        }

        // For VP8 (software decoding), use Unity's default UI shader
        // runtimeVideoMaterial remains null so Unity uses default material
    }

    private void Start()
    {
        unitySync = SynchronizationContext.Current;

        GameObject netConfGame = GameObject.Find("NetworkConfigsLoader");
        if (netConfGame != null)
        {
            netConfig = netConfGame.GetComponent<NetworkManager>();
        }
        else
        {
            Debug.LogError("NetworkConfigsLoader not found!");
            enabled = false;
            return;
        }

        placeholderTexture = new Texture2D(videoWidth, videoHeight, TextureFormat.RGB24, false);
        image.texture = placeholderTexture;
        image.SetNativeSize();

        if (autoConnectOnStart)
        {
            _ = EnsureConnectionAsync();
        }

        // DIAGNOSTIC: Count all CameraOneStreamer instances in the scene
        var allStreamers = FindObjectsOfType<CameraOneStreamer>();
        Debug.Log($"DIAGNOSTIC: Found {allStreamers.Length} CameraOneStreamer instance(s) in scene. This instance ID: {GetInstanceID()}");
        
        Debug.Log($"CameraOneStreamer start: webrtcVerboseLogs={netConfig != null && netConfig.IsWebRTCVerbose()}");
    }

    private void Update()
    {
        WebRTC.Update();

        if (netConfig == null)
            return;
        
        // DIAGNOSTIC: Periodically log video track state (every 60 frames ~= 1 second)
        if (Time.frameCount % 60 == 0)
        {
            bool hasTrack = currentVideoTrack != null;
            bool hasTexture = hasTrack && currentVideoTrack.Texture != null;
            Debug.Log($"DIAGNOSTIC: Frame {Time.frameCount} - connectionEstablished:{connectionEstablished}, hasTrack:{hasTrack}, hasTexture:{hasTexture}, frameCount:{frameCount}");
        }

        if (netConfig.ForceDisconnect)
        {
            DisconnectNetMQ();
            return;
        }

        if (connectionEstablished)
        {
            KeepAliveCheck();
            
            // Force texture refresh every frame (WebRTC auto-update may not work on Quest)
            // This mimics the old NetMQ pattern where we actively updated the texture in Update()
            if (currentVideoTrack != null && currentVideoTrack.Texture != null)
            {
                image.texture = currentVideoTrack.Texture;
                
                // DIAGNOSTIC: Check if texture native pointer changes (indicates new frame data)
                if (Time.frameCount % 60 == 0)
                {
                    var tex = currentVideoTrack.Texture;
                    var ptr = tex.GetNativeTexturePtr();
                    Debug.Log($"TEXTURE_PTR: {ptr} - dimensions: {tex.width}x{tex.height}, format: {tex.graphicsFormat}");
                }
            }
        }

        if (autoConnectOnStart && !connectionEstablished && !isConnecting)
        {
            _ = EnsureConnectionAsync();
        }
    }

    public void ConnectNetMQ()
    {
        _ = EnsureConnectionAsync();
    }

    public void DisconnectNetMQ()
    {
        // Debug.LogError($"DisconnectNetMQ called! Stack: {Environment.StackTrace}");
        connectionCts?.Cancel();
        connectionCts = null;
        isConnecting = false;
        connectionEstablished = false;
        frameCount = 0;
        lastFrameTime = 0f;

        CleanupPeer();

        if (signalingClient != null)
        {
            signalingClient.Dispose();
            signalingClient = null;
        }

        if (placeholderTexture != null)
        {
            image.texture = placeholderTexture;
        }
        image.material = null; // revert to default UI material
    }

    private async Task EnsureConnectionAsync()
    {
        if (connectionEstablished || isConnecting)
            return;

        if (netConfig == null)
            return;

        string signalingAddress = netConfig.getWebRTCSignalingAddress();
        if (string.Equals(signalingAddress, "tcp://:", StringComparison.Ordinal))
        {
            if (netConfig.IsWebRTCVerbose())
            {
                Debug.LogWarning("WebRTC signaling address is not set; skipping connection");
            }
            return;
        }

        isConnecting = true;
        connectionCts = new CancellationTokenSource();

        try
        {
            signalingClient?.Dispose();
            signalingClient = new WebRTCSignalingClient(signalingAddress, netConfig.IsWebRTCVerbose());

            var config = BuildRtcConfig();
            peerConnection = new RTCPeerConnection(ref config);
            peerConnection.OnIceCandidate = candidate =>
            {
                if (netConfig.IsWebRTCVerbose())
                {
                    Debug.Log($"Local ICE candidate gathered: {candidate.Candidate}");
                }
            };
            peerConnection.OnTrack = OnTrackReceived;

            var transceiver = peerConnection.AddTransceiver(TrackKind.Video);
            transceiver.Direction = RTCRtpTransceiverDirection.RecvOnly;

            var offerOp = peerConnection.CreateOffer();
            var offer = await AwaitSdpAsync(offerOp);

            var setLocalOp = peerConnection.SetLocalDescription(ref offer);
            await AwaitSetSdpAsync(setLocalOp);

            var offerPayload = new WebRTCSignalingClient.OfferPayload
            {
                client_id = netConfig.getWebRTCClientId(),
                sdp = offer.sdp
            };

            var answer = await signalingClient.SendOfferAsync(offerPayload, signalingTimeoutMs, connectionCts.Token);

            var answerDesc = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = answer.sdp
            };
            var setRemoteOp = peerConnection.SetRemoteDescription(ref answerDesc);
            await AwaitSetSdpAsync(setRemoteOp);

            if (answer.candidates != null)
            {
                foreach (var cand in answer.candidates)
                {
                    var init = new RTCIceCandidateInit
                    {
                        candidate = cand.candidate,
                        sdpMid = cand.sdpMid,
                        sdpMLineIndex = cand.sdpMLineIndex
                    };

                    bool added = peerConnection.AddIceCandidate(new RTCIceCandidate(init));
                    if (!added && netConfig.IsWebRTCVerbose())
                    {
                        Debug.LogWarning($"Failed to add remote ICE candidate: {cand.candidate}");
                    }
                }
            }

            connectionEstablished = true;
            if (netConfig.IsWebRTCVerbose())
            {
                Debug.Log($"WebRTC video connected via {signalingAddress} as {offerPayload.client_id}");
            }
        }
        catch (OperationCanceledException)
        {
            if (netConfig.IsWebRTCVerbose())
            {
                Debug.Log("WebRTC connection attempt canceled");
            }
            DisconnectNetMQ();
        }
        catch (Exception e)
        {
            Debug.LogError($"WebRTC connection error: {e.Message}");
            DisconnectNetMQ();
        }
        finally
        {
            isConnecting = false;
        }
    }

    private RTCConfiguration BuildRtcConfig()
    {
        return new RTCConfiguration
        {
            iceServers = new[]
            {
                new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
            }
        };
    }

    private void OnTrackReceived(RTCTrackEvent e)
    {
        if (e.Track is VideoStreamTrack videoTrack)
        {
            Debug.Log("OnTrackReceived: video track received");

            // Ensure Unity-thread operations
            unitySync.Post(_ =>
            {
                CleanupTrack();
                currentVideoTrack = videoTrack;
                currentVideoTrack.OnVideoReceived += HandleVideoReceived;

                // If a texture already exists (e.g., fast first frame), apply immediately
                if (currentVideoTrack.Texture != null)
                {
                    Debug.Log($"OnTrackReceived: applying existing texture {currentVideoTrack.Texture.width}x{currentVideoTrack.Texture.height}");
                    ApplyTexture(currentVideoTrack.Texture);
                }
            }, null);
        }
    }

    private void HandleVideoReceived(Texture texture)
    {
        if (texture == null) return;
        frameCount++;
        lastFrameTime = Time.time; // Track when we received this frame
        Debug.Log($"HandleVideoReceived: Frame {frameCount} - {texture.width}x{texture.height}");

        // DEBUG: Check if texture has actual video data
        /*
        if (texture is Texture2D tex2D)
        {
            try
            {
                Color[] pixels = tex2D.GetPixels(0, 0, 4, 4, 0); // Sample 4x4 pixels
                Color avgColor = Color.black;
                foreach (Color pixel in pixels)
                {
                    avgColor += pixel;
                }
                avgColor /= pixels.Length;
                Debug.Log($"Texture sample - avg color: {avgColor}, has data: {!avgColor.Equals(Color.black) && !avgColor.Equals(Color.white)}");
            }
            catch (Exception e)
            {
                Debug.Log($"Could not sample texture: {e.Message}");
            }
        }
        */

        unitySync.Post(_ => ApplyTexture(texture), null);
    }

    private void ApplyTexture(Texture texture)
    {
        currentTexture = texture;
        if (currentTexture != null)
        {
            // KEEP ALIVE: Check WebRTC connection health
            // KeepAliveCheck(); // Moved to Update() to catch stalls when frames stop arriving

            // DEBUG: Detailed texture analysis
            Debug.Log($"ApplyTexture: {currentTexture.width}x{currentTexture.height} ({currentTexture.GetType().Name})");
            Debug.Log($"Texture format: {currentTexture.graphicsFormat}, filter: {currentTexture.filterMode}, wrap: {currentTexture.wrapMode}");

            // For VP8 (software decoding), use Unity's default UI shader
            // Explicitly set to null to ensure default material
            image.material = null;

            image.texture = currentTexture;
            image.color = Color.white; // Fix for VP8 transparency
            image.SetNativeSize();

            // DEBUG: Check RawImage state
            Debug.Log($"RawImage state - enabled:{image.enabled}, active:{image.gameObject.activeInHierarchy}, color:{image.color}, material:{image.material?.name ?? "null"}");

            // FIX: Activate the GameObject if inactive
            if (!image.gameObject.activeInHierarchy)
            {
                Debug.Log("Activating RawImage GameObject!");
                image.gameObject.SetActive(true);
            }

            // Force refresh by toggling
            image.enabled = false;
            image.enabled = true;

            // Force Canvas update
            if (image.canvas != null)
            {
                UnityEngine.Canvas.ForceUpdateCanvases();
            }
        }
    }

    private void KeepAliveCheck()
    {
        // Check if WebRTC connection is still healthy
        if (peerConnection != null)
        {
            var state = peerConnection.ConnectionState;
            
            // Only log state changes, not every frame (reduce noise)
            // Debug.Log($"KeepAlive: WebRTC connection state: {state}");

            if (state == RTCPeerConnectionState.Disconnected ||
                state == RTCPeerConnectionState.Failed ||
                state == RTCPeerConnectionState.Closed)
            {
                Debug.LogWarning("KeepAlive: WebRTC connection lost, attempting reconnect...");
                connectionEstablished = false;
                isConnecting = false;
                // Trigger reconnection
                _ = EnsureConnectionAsync();
            }
            
            // REMOVED: The stall detection based on frameCount==1 was WRONG!
            // OnVideoReceived only fires ONCE (on texture resize), so frameCount
            // will always be 1 even when video is streaming normally.
            // The texture updates in-place without triggering callbacks.
        }

        // Check if we've received frames recently (prevent frozen stream)
        // Note: This only checks the initial frame arrival, not ongoing frames
        if (frameCount == 0 && Time.frameCount % 60 == 0) // Only log occasionally
        {
            Debug.LogWarning("KeepAlive: No frames received yet, connection may be slow");
        }
    }

    private void CleanupTrack()
    {
        if (currentVideoTrack != null)
        {
            currentVideoTrack.OnVideoReceived -= HandleVideoReceived;
            currentVideoTrack = null;
        }
        currentTexture = null;
    }

    private void CleanupPeer()
    {
        CleanupTrack();

        if (peerConnection != null)
        {
            peerConnection.Close();
            peerConnection.Dispose();
            peerConnection = null;
        }
    }

    private void OnDestroy()
    {
        DisconnectNetMQ();
        
        // Only clear global state if this is the singleton instance
        if (_instance == this)
        {
            _instance = null;
            if (webRtcInitialized)
            {
                webRtcInitialized = false;
            }
        }
    }

    private void OnApplicationQuit()
    {
        DisconnectNetMQ();
        if (webRtcInitialized)
        {
            webRtcInitialized = false;
        }
    }

    /// <summary>
    /// Initialize WebRTC in a version-tolerant way. If the EncoderType overload exists,
    /// prefer the Software encoder; otherwise fall back to the parameterless Initialize.
    /// </summary>
    private static void InitializeWebRTCWithFallback()
    {
        try
        {
            var encoderType = Type.GetType("Unity.WebRTC.EncoderType, Unity.WebRTC");
            var methods = typeof(WebRTC).GetMethods(BindingFlags.Public | BindingFlags.Static);

            // Look for Initialize(EncoderType)
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (method.Name == "Initialize" && parameters.Length == 1 && encoderType != null && parameters[0].ParameterType == encoderType)
                {
                    var softwareValue = Enum.Parse(encoderType, "Software");
                    method.Invoke(null, new[] { softwareValue });
                    Debug.Log("WebRTC initialized (software encoder via reflection)");
                    return;
                }
            }

            // Fallback: parameterless Initialize (default encoder)
            var noArgInit = typeof(WebRTC).GetMethod("Initialize", Type.EmptyTypes);
            noArgInit?.Invoke(null, null);
            Debug.Log("WebRTC initialized (default encoder)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebRTC initialization failed: {ex.Message}");
        }
    }

    private static async Task<RTCSessionDescription> AwaitSdpAsync(RTCSessionDescriptionAsyncOperation op)
    {
        while (!op.IsDone)
        {
            await Task.Yield();
        }

        if (op.IsError)
        {
            throw new Exception($"SDP operation failed: {op.Error.message}");
        }

        return op.Desc;
    }

    private static async Task AwaitSetSdpAsync(RTCSetSessionDescriptionAsyncOperation op)
    {
        while (!op.IsDone)
        {
            await Task.Yield();
        }

        if (op.IsError)
        {
            throw new Exception($"Set SDP failed: {op.Error.message}");
        }
    }
}