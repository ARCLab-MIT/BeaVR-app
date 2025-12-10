using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using UnityEngine;

/// <summary>
/// Minimal ZMQ REQ signaling client for exchanging SDP offers/answers
/// with the Python WebRTC server. Media stays on WebRTC; ZMQ is only
/// used for signaling on tcp://host:port.
/// </summary>
public class WebRTCSignalingClient : IDisposable
{
    [Serializable]
    public class OfferPayload
    {
        public string type = "offer";
        public string client_id;
        public string sdp;
    }

    [Serializable]
    public class IceCandidatePayload
    {
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }

    [Serializable]
    public class AnswerPayload
    {
        public string type;
        public string client_id;
        public string sdp;
        public IceCandidatePayload[] candidates;
    }

    private readonly string _endpoint;
    private readonly bool _verboseLogs;

    public WebRTCSignalingClient(string endpoint, bool verboseLogs)
    {
        _endpoint = endpoint;
        _verboseLogs = verboseLogs;
    }

    public async Task<AnswerPayload> SendOfferAsync(OfferPayload offer, int timeoutMs, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            using (var req = new RequestSocket())
            {
                req.Options.Linger = TimeSpan.Zero;
                req.Connect(_endpoint);

                var json = JsonUtility.ToJson(offer);
                if (_verboseLogs)
                {
                    Debug.Log($"WebRTC signaling -> {_endpoint}: {json}");
                }

                req.SendFrame(json);

                bool replyReceived = req.TryReceiveFrameString(TimeSpan.FromMilliseconds(timeoutMs), out var reply);
                if (!replyReceived)
                {
                    throw new TimeoutException($"No signaling reply from {_endpoint} within {timeoutMs} ms");
                }

                if (_verboseLogs)
                {
                    Debug.Log($"WebRTC signaling <- {_endpoint}: {reply}");
                }

                var answer = JsonUtility.FromJson<AnswerPayload>(reply);
                if (answer == null || string.IsNullOrEmpty(answer.sdp))
                {
                    throw new InvalidOperationException("Signaling reply missing SDP");
                }

                return answer;
            }
        }, token);
    }

    public void Dispose()
    {
        // Nothing to dispose currently; placeholder in case we expand the client.
    }
}
