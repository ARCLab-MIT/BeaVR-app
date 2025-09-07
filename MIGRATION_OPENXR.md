## OpenXR Migration Architecture

This document describes the plan and current state for migrating the project from Oculus Integration (OVR) APIs to Unity OpenXR, XR Hands, and XR Interaction Toolkit (XRI). It inventories current OVR dependencies, defines replacements, and lists concrete scene and code changes.

### Goals
- Replace OVR hand tracking and gestures with OpenXR XR Hands.
- Use XRI for rays, interaction, and UI input (no OVR input modules).
- Preserve networking, message formats, and UI behavior.
- Optionally migrate Passthrough to OpenXR Meta features; otherwise keep it out of the gameplay loop.

---

## Current OVR Dependencies (inventory)

Code references:
- `Assets/Scripts/Gesture Detection/GestureDetector.cs`
  - Uses `OVRHand`, `OVRSkeleton`, `OVRPassthroughLayer` and pinch helpers.
- `Assets/Scripts/HandPointerLike.cs`
  - Uses `OVRInputModule`, `OVRRaycaster`, `OVRHand` to drive UI pointer.
- `Assets/Oculus/Interaction/Runtime/Scripts/Selection/Hands/HandPointerPose.cs` (third-party)
  - Uses Oculus Interaction `IHand`; used to position a pointer from hand pose.

Scene references (SampleScene):
- `OVRCameraRig` object present.
- `EventSystem` has `OVRInputModule` and canvases use `OVRRaycaster`.

Settings/assets:
- ProjectSettings contain Oculus loader entries.
- Many assets under `Assets/Oculus/` (legacy Oculus Integration).

---

## Target Architecture (OpenXR + XR Hands + XRI)

- Device runtime: OpenXR provider enabled (Meta XR feature group for Quest).
- Input/Hands: `XR Hands` package + OpenXR Hand Tracking + Hand Tracking Aim feature.
- Interaction/UI: `XR Interaction Toolkit` (XRI)
  - Rays: `XR Ray Interactor` + `XR Interactor Line Visual` (controllers or hands aim)
  - UI input: `XR UI Input Module` on `EventSystem`, `Tracked Device Graphic Raycaster` on canvases
- Origin: `XR Origin (XR Rig)` as the only rig (no `OVRCameraRig`).
- Networking: keep `NetMQController` flow and string formats.

---

## Component-by-Component Replacement

### Hand tracking and gesture logic
- Replaced by: `Assets/Scripts/Gesture Detection/GestureDetectorXR.cs`
  - Uses `XRHandSubsystem` for joints.
  - Computes pinches via `ThumbTip`–`Index/Middle/RingTip` distance thresholds.
  - Sends data via `NetMQController.Instance.SendMessage("RightHand"/"LeftHand")` in the same string format.
  - UI state and color changes preserved.

Action items:
- Remove or disable `GestureDetector` component in the scene.
- Add `GestureDetectorXR` to the same GameObject and wire:
  - `XrOrigin` → your `XR Origin (XR Rig)`
  - UI references (`MenuButton`, `ResolutionButton`, `WristTracker`, `StreamBorder`, High/Low resolution controllers)

### UI pointer and raycasting (OVR → XRI)
- Remove code-driven OVR UI pointer:
  - Delete or disable `Assets/Scripts/HandPointerLike.cs` (it depends on `OVRInputModule`, `OVRRaycaster`, `OVRHand`).
- Replace with XRI setup (no custom script required):
  - Add `XR UI Input Module` to the scene `EventSystem`.
  - On each world-space Canvas, add `Tracked Device Graphic Raycaster`.
  - Add left/right child objects under `XR Origin (XR Rig)` with:
    - `XR Controller (Action-based)` bound to the XRI default actions or Hands scheme
    - `XR Ray Interactor`
    - `XR Interactor Line Visual`
  - Set Interaction Layer Mask on the ray to include the `UI` layer if you want a UI-only ray.

If you need a world-space pointer Transform that follows the hand aim (like `HandPointerPose`), create a simple XRI/HandAim follower:
- Use the Hand Tracking Aim feature (OpenXR → Meta XR → Hand Tracking Aim), then bind a `Pose` action to the hand aim pose in the Input System and drive a transform with it. XRI’s default Hands Interaction profile already feeds rays from Hand Aim; prefer that instead of code.

### Oculus Interaction: `HandPointerPose`
- This component is from the Oculus Interaction package and not needed once XRI Hand Aim rays are used.
- Replace with:
  - XRI hand rays driven by Hand Tracking Aim (recommended, zero code), or
  - A small Input System–based `Pose` follower if you have special offsets/visuals.

### Passthrough
- If you currently rely on `OVRPassthroughLayer`, choose one:
  - Keep Oculus loader temporarily just for Passthrough until you migrate.
  - Or enable OpenXR Meta XR features and port to the corresponding Passthrough feature (availability and APIs vary by Unity/OpenXR versions). For now, keep Passthrough out of gameplay-critical paths.

---

## Scene Migration Steps

1) Rig
- Delete/disable `OVRCameraRig`.
- Ensure only `XR Origin (XR Rig)` is active. Assign it to `GestureDetectorXR.XrOrigin`.

2) UI/EventSystem
- Remove `OVRInputModule` from `EventSystem`.
- Add `XR UI Input Module`.
- On world-space canvases: remove `OVRRaycaster`, add `Tracked Device Graphic Raycaster`.

3) Rays/Interaction
- Under `XR Origin`, add left/right controller or hand interactor objects:
  - `XR Controller (Action-based)` + `XR Ray Interactor` + `XR Interactor Line Visual`.
  - Use XRI Starter Assets input actions; select Hands Interaction scheme if using hand rays.

4) Hand tracking
- In Project Settings → XR Plug‑in Management → OpenXR:
  - Enable `XR Hands` feature and Meta XR Hand Tracking Aim.

5) Networking
- Keep `NetworkConfigsLoader` and `NetMQController` in scene.
- Ensure IP is set; `GestureDetectorXR` uses the same `Connect`/`SendMessage` flow.

---

## Joint Set and Ordering Notes

- XR Hands provides 26 joints per hand: `Wrist`, `Palm`, per-finger `Metacarpal/Proximal/Intermediate/Distal/Tip`.
- OVR bones differ slightly (names, count, ordering).
- The sender now uses a fixed XR Hands order. If your Python receiver assumed OVR order, update it once or add a reordering step on the Unity side.

---

## Clean-up Plan (post-validation)

- Remove `GestureDetector` component and script if unneeded.
- Remove `HandPointerLike.cs` and any `OVRInputModule`/`OVRRaycaster` references from the scene.
- Remove/disable Oculus loader in XR Plug‑in Management once Passthrough is no longer needed.
- Optionally delete the `Assets/Oculus/` folder after verifying no remaining references.

---

## Risks and Validation

- Hand Aim availability depends on enabling the Meta XR Hand Tracking Aim feature. Without it, hand rays won’t appear.
- Coordinate space: XR Hands poses are in the XR origin space; `GestureDetectorXR` transforms to world via `XR Origin`.
- Expect small numeric differences vs OVR due to different providers/filters.

### Smoke Test
- Enter Play on device. Verify:
  - Console shows XR Hands subsystem created (no warnings).
  - Border turns green on successful connect.
  - Index/Middle/Ring pinches toggle modes and colors.
  - UI responds to XRI rays on canvases.
  - Receiver parses incoming joint strings.

---

## Status
- Implemented `GestureDetectorXR` (XR Hands) and preserved networking and UI modes.
- Next: remove OVR UI modules (`HandPointerLike.cs`, `OVRInputModule`, `OVRRaycaster`) and wire XRI rays & UI input.
