using System;
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
    [Tooltip("Material for external WebRTC textures (e.g., Quest). Leave empty to auto-create with WebRTC/ExternalVideo shader.")]
    [SerializeField] private Material externalVideoMaterial;

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
    private Material runtimeVideoMaterial;

    private void Awake()
    {
        // Explicitly initialize WebRTC; force software path so it runs on machines/Quest without GPU decode
        if (!webRtcInitialized)
        {
            WebRTC.Initialize(EncoderType.Software);
            webRtcInitialized = true;
            Debug.Log("WebRTC initialized (software encoder)");
        }

        // Prepare a material that can sample external textures on Android/Quest
        if (externalVideoMaterial != null)
        {
            runtimeVideoMaterial = externalVideoMaterial;
        }
        else
        {
            var shader = Shader.Find("WebRTC/ExternalVideo");
            if (shader != null)
            {
                runtimeVideoMaterial = new Material(shader);
                runtimeVideoMaterial.name = "RuntimeExternalVideoMaterial";
            }
            else
            {
                Debug.LogWarning("WebRTC/ExternalVideo shader not found; RawImage will use default UI material (may stay black on Quest).");
            }
        }
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

        Debug.Log($"CameraOneStreamer start: webrtcVerboseLogs={netConfig != null && netConfig.IsWebRTCVerbose()}");
    }

    private void Update()
    {
        WebRTC.Update();

        if (netConfig == null)
            return;

        if (netConfig.ForceDisconnect)
        {
            DisconnectNetMQ();
            return;
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
        connectionCts?.Cancel();
        connectionCts = null;
        isConnecting = false;
        connectionEstablished = false;

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
        Debug.Log($"HandleVideoReceived: {texture.width}x{texture.height}");
        unitySync.Post(_ => ApplyTexture(texture), null);
    }

    private void ApplyTexture(Texture texture)
    {
        currentTexture = texture;
        if (currentTexture != null)
        {
            if (runtimeVideoMaterial != null)
            {
                image.material = runtimeVideoMaterial;
            }
            image.texture = currentTexture;
            image.SetNativeSize();
            Debug.Log($"ApplyTexture: {currentTexture.width}x{currentTexture.height} ({currentTexture.GetType().Name})");
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
        if (webRtcInitialized)
        {
            WebRTC.Dispose();
            webRtcInitialized = false;
            Debug.Log("WebRTC disposed (OnDestroy)");
        }
        if (runtimeVideoMaterial != null && externalVideoMaterial == null)
        {
            Destroy(runtimeVideoMaterial);
            runtimeVideoMaterial = null;
        }
    }

    private void OnApplicationQuit()
    {
        DisconnectNetMQ();
        if (webRtcInitialized)
        {
            WebRTC.Dispose();
            webRtcInitialized = false;
            Debug.Log("WebRTC disposed (OnApplicationQuit)");
        }
        if (runtimeVideoMaterial != null && externalVideoMaterial == null)
        {
            Destroy(runtimeVideoMaterial);
            runtimeVideoMaterial = null;
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