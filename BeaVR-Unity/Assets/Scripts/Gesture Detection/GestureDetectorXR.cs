using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

public class GestureDetectorXR : MonoBehaviour
{
	// XR / Hands (no XROrigin; using world space or OVRHand)
	private XRHandSubsystem _handSubsystem;

	// Throttled telemetry logging for keypoints
	private float _lastKeypointLogTime = 0f;

	// OVR Hands (optional; assign in inspector). If set, we use OVR pinch flags instead of distance checks
	public OVRHand leftHand;
	public OVRHand rightHand;

	// UI and helpers (kept to match original behavior)
	public GameObject MenuButton;
	public GameObject ResolutionButton;
	public GameObject HighResolutionButton;
	public GameObject LowResolutionButton;
	// WristTracker visual removed
	public RawImage StreamBorder;

	public HighResolutionButtonController HighResolutionButtonController;
	public LowResolutionButtonController LowResolutionButtonController;

	// Networking
	private NetworkManager netConfig;
	private bool connectionAttemptInProgress = false;

	// Modes
	bool StreamRelativeData = true;
	bool StreamAbsoluteData = false;
	bool StreamResolution = false;
	private bool ShouldContinueArmTeleop = false;

	// Joint order definition (26 joints)
	static readonly XRHandJointID[] k_JointOrder = new XRHandJointID[]
	{
		XRHandJointID.Wrist,
		XRHandJointID.Palm,
		XRHandJointID.ThumbMetacarpal,
		XRHandJointID.ThumbProximal,
		XRHandJointID.ThumbDistal,
		XRHandJointID.ThumbTip,
		XRHandJointID.IndexMetacarpal,
		XRHandJointID.IndexProximal,
		XRHandJointID.IndexIntermediate,
		XRHandJointID.IndexDistal,
		XRHandJointID.IndexTip,
		XRHandJointID.MiddleMetacarpal,
		XRHandJointID.MiddleProximal,
		XRHandJointID.MiddleIntermediate,
		XRHandJointID.MiddleDistal,
		XRHandJointID.MiddleTip,
		XRHandJointID.RingMetacarpal,
		XRHandJointID.RingProximal,
		XRHandJointID.RingIntermediate,
		XRHandJointID.RingDistal,
		XRHandJointID.RingTip,
		XRHandJointID.LittleMetacarpal,
		XRHandJointID.LittleProximal,
		XRHandJointID.LittleIntermediate,
		XRHandJointID.LittleDistal,
		XRHandJointID.LittleTip
	};

    void Start()
    {
		// Network config
		GameObject netConfGameObject = GameObject.Find("NetworkConfigsLoader");
		if (netConfGameObject != null)
			netConfig = netConfGameObject.GetComponent<NetworkManager>();

		// Acquire XR Hands subsystem
		TryResolveHandSubsystem();

		// Give OpenXR a moment and run NetMQController init
		StartCoroutine(InitializeNetMQAfterDelay());
	}

	IEnumerator InitializeNetMQAfterDelay()
	{
		yield return new WaitForSeconds(2f);
		NetMQController.Instance.CreateStandardSockets();
		NetMQController.Instance.PerformDiagnosticTests();
	}

	void TryResolveHandSubsystem()
	{
		if (_handSubsystem != null)
			return;
		var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
		if (loader != null)
		{
			_handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
			if (_handSubsystem == null)
			{
				Debug.LogWarning("XRHandSubsystem not found. Ensure XR Hands package/feature is enabled.");
			}
		}
	}

	public static string SerializeVector3List(List<Vector3> gestureData)
	{
		string vectorString = "";
		foreach (Vector3 vec in gestureData)
			vectorString = vectorString + vec.x + "," + vec.y + "," + vec.z + "|";

		if (vectorString.Length > 0)
			vectorString = vectorString.Substring(0, vectorString.Length - 1) + ":";

		return vectorString;
	}

    void Update()
    {
		// Reacquire subsystem if needed (domain reloads, etc.)
		if (_handSubsystem == null)
			TryResolveHandSubsystem();

		bool isConnected = NetMQController.Instance.AreSocketsConnected();
		if (!isConnected)
		{
			if (StreamBorder != null) StreamBorder.color = Color.red;
			ToggleMenuButton(true);

			string ipAddress = netConfig != null ? netConfig.netConfig.IPAddress : null;
			if (!string.IsNullOrEmpty(ipAddress) && ipAddress != "undefined")
			{
				if (!connectionAttemptInProgress)
				{
					connectionAttemptInProgress = true;
					StartCoroutine(AttemptConnection());
				}
			}
			return;
		}

		connectionAttemptInProgress = false;

		// Process gestures (left hand pinches)
		StreamPauser();

		// Send auxiliary channels
		SendResolutionThroughController();
		SendPauseStatusThroughController();

		// Send hand data
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

	IEnumerator AttemptConnection()
	{
		NetMQController.Instance.Connect(
			netConfig.netConfig.IPAddress,
			netConfig.getRightKeypointAddress(),
			netConfig.getLeftKeypointAddress(),
			netConfig.getResolutionAddress(),
			netConfig.getPauseAddress()
		);

		yield return new WaitForSeconds(2f);

		bool success = NetMQController.Instance.AreSocketsConnected();
		if (StreamBorder != null) StreamBorder.color = success ? Color.green : Color.red;
		ToggleMenuButton(!success);
		connectionAttemptInProgress = false;
	}

	// Gesture toggling using XR Hands (left hand only, to match original)
	void StreamPauser()
	{
		bool pinchIndex = false;
		bool pinchMiddle = false;
		bool pinchRing = false;

		// Prefer OVR pinch detection if available
		if (leftHand != null)
		{
			if (!leftHand.IsTracked)
				return;
			pinchIndex = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
			pinchMiddle = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Middle);
			pinchRing = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Ring);
		}
		else
		{
			if (_handSubsystem == null)
				return;
			var left = _handSubsystem.leftHand;
			if (!left.isTracked)
				return;
			pinchIndex = IsPinching(left, XRHandJointID.IndexTip);
			pinchMiddle = IsPinching(left, XRHandJointID.MiddleTip);
			pinchRing = IsPinching(left, XRHandJointID.RingTip);
		}

		if (pinchMiddle)
		{
			StreamRelativeData = false;
			StreamAbsoluteData = true;
			if (StreamBorder != null) StreamBorder.color = Color.blue;
			ToggleMenuButton(false);
			ShouldContinueArmTeleop = true;
		}

		if (pinchIndex)
		{
			StreamRelativeData = true;
			StreamAbsoluteData = false;
			if (StreamBorder != null) StreamBorder.color = Color.green;
			ToggleMenuButton(false);
			ShouldContinueArmTeleop = true;
		}

		if (pinchRing)
		{
			StreamRelativeData = false;
			StreamAbsoluteData = false;
			if (StreamBorder != null) StreamBorder.color = Color.red;
			ToggleMenuButton(true);
			ShouldContinueArmTeleop = false;
		}
	}

	bool IsPinching(XRHand hand, XRHandJointID fingerTip, float thresholdMeters = 0.02f)
	{
		var thumb = hand.GetJoint(XRHandJointID.ThumbTip);
		var tip = hand.GetJoint(fingerTip);
		if (!thumb.TryGetPose(out var tPose) || !tip.TryGetPose(out var fPose))
			return false;
		Vector3 tp = ToWorldPosition(tPose.position);
		Vector3 fp = ToWorldPosition(fPose.position);
		return Vector3.Distance(tp, fp) < thresholdMeters;
	}

	Vector3 ToWorldPosition(Vector3 pos)
	{
		// Using OVR Camera Rig: world space is fine
		return pos;
	}

	void SendHandDataThroughController(string typeMarker)
	{
		try
		{
			if (_handSubsystem == null)
				return;

			// Right hand
			List<Vector3> rightHandGestureData = new List<Vector3>();
			CollectHandJointPositions(_handSubsystem.rightHand, rightHandGestureData);
			string rightHandDataString = SerializeVector3List(rightHandGestureData);
			rightHandDataString = typeMarker + ":" + rightHandDataString;
			NetMQController.Instance.SendMessage("RightHand", rightHandDataString);

			// Left hand
			List<Vector3> leftHandGestureData = new List<Vector3>();
			CollectHandJointPositions(_handSubsystem.leftHand, leftHandGestureData);
			string leftHandDataString = SerializeVector3List(leftHandGestureData);
			leftHandDataString = typeMarker + ":" + leftHandDataString;
			NetMQController.Instance.SendMessage("LeftHand", leftHandDataString);

			// Throttled on-device log so you can verify what we're sending via adb
			if (Time.time - _lastKeypointLogTime > 1.0f)
			{
				int rTotal = rightHandGestureData.Count;
				int lTotal = leftHandGestureData.Count;
				int rTracked = CountNonZeroJoints(rightHandGestureData);
				int lTracked = CountNonZeroJoints(leftHandGestureData);
				int sampleIndex = Mathf.Min(10, Mathf.Max(0, rTotal - 1)); // prefer IndexTip if available
				Vector3 rSample = rTotal > 0 ? rightHandGestureData[sampleIndex] : Vector3.zero;
				Vector3 lSample = lTotal > 0 ? leftHandGestureData[sampleIndex] : Vector3.zero;
				Debug.Log(
					$"GestureDetectorXR: sent {typeMarker} | RH joints={rTotal} tracked={rTracked} sample={FormatVec(rSample)} | LH joints={lTotal} tracked={lTracked} sample={FormatVec(lSample)}");
				_lastKeypointLogTime = Time.time;
			}
		}
		catch (Exception e)
		{
			Debug.LogError("Error sending hand data (XR): " + e.Message);
		}
	}

	void CollectHandJointPositions(XRHand hand, List<Vector3> outPositions)
	{
		outPositions.Clear();
		for (int i = 0; i < k_JointOrder.Length; i++)
		{
			var joint = hand.GetJoint(k_JointOrder[i]);
			if (joint.TryGetPose(out Pose pose))
			{
				outPositions.Add(ToWorldPosition(pose.position));
			}
			else
			{
				outPositions.Add(Vector3.zero);
			}
		}
	}

	int CountNonZeroJoints(List<Vector3> positions)
	{
		int count = 0;
		for (int i = 0; i < positions.Count; i++)
		{
			if (positions[i] != Vector3.zero) count++;
		}
		return count;
	}

	string FormatVec(Vector3 v)
	{
		return $"({v.x:F3},{v.y:F3},{v.z:F3})";
	}

	void SendResolutionThroughController()
	{
		try
		{
			string state = "None";
			if (HighResolutionButtonController != null && HighResolutionButtonController.HighResolution)
			{
				state = "High";
			}
			else if (LowResolutionButtonController != null && LowResolutionButtonController.LowResolution)
			{
				state = "Low";
			}
			NetMQController.Instance.SendMessage("Resolution", state);
		}
		catch (Exception e)
		{
			Debug.LogError("Error sending resolution data: " + e.Message);
		}
	}

	void SendPauseStatusThroughController()
	{
		try
		{
			string pauseState = ShouldContinueArmTeleop ? "High" : "Low";
			NetMQController.Instance.SendMessage("Pause", pauseState);
		}
		catch (Exception e)
		{
			Debug.LogError("Error sending pause status: " + e.Message);
		}
	}

	public void ToggleMenuButton(bool toggle)
	{
		try
		{
			if (MenuButton != null)
				MenuButton.SetActive(toggle);
		}
		catch (Exception e)
		{
			Debug.LogError("Error in ToggleMenuButton: " + e.Message);
		}
	}

	public void ToggleResolutionButton(bool toggle)
	{
		try
		{
			if (ResolutionButton != null)
				ResolutionButton.SetActive(toggle);
		}
		catch (Exception e)
		{
			Debug.LogError("Error in ToggleResolutionButton: " + e.Message);
		}
	}

	public void ToggleHighResolutionButton(bool toggle)
	{
		Debug.Log("HighResolutionButton toggle (XR): " + toggle);
	}

	public void ToggleLowResolutionButton(bool toggle)
	{
		Debug.Log("LowResolutionButton toggle (XR): " + toggle);
	}

	// Exposed helpers for keep-alive
	public bool AreAllConnectionsEstablished()
	{
		return NetMQController.Instance != null && NetMQController.Instance.AreSocketsConnected();
	}

	public void SendKeepAlivePing()
	{
		try
		{
			NetMQController.Instance.SendMessage("Pause", "KEEPALIVE");
		}
		catch (Exception e)
		{
			Debug.LogError("Keep-alive ping failed: " + e.Message);
		}
	}

	void OnApplicationQuit()
	{
	}

	void OnDestroy()
	{
	}
}
