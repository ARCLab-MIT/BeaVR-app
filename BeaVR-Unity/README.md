# BeaVR-Unity

This Unity project runs on **Unity version 2021.3.45f**. It provides a 2D VR experience that streams hand-tracking data, camera feeds, and graph data over a network. The primary visualization includes hand keypoints, which indicate the status of the connection by mirroring hand movements.

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Main Features](#main-features)
3. [Networking Details](#networking-details)
4. [How Data Flows](#how-data-flows)
5. [Usage](#usage)
6. [Troubleshooting](#troubleshooting)
7. [License](#license)

---

## Project Structure

- **Scripts:**
  - Primary scripts (`GestureDetector.cs`, `NetworkManager.cs`) are located in:
    ```
    BeaVR-Unity/Assets/Scripts/Gesture Detection/
    ```

- **Configurations:**
  - Network settings (IP addresses, ports) in:
    ```
    BeaVR-Unity/Assets/Resources/Configurations/Network.json
    ```

- **Other Notable Folders:**
  - Camera streaming scripts:
    ```
    BeaVR-Unity/Assets/Camera Stream Scripts/
    ```
  - Oculus plugins and resources:
    ```
    BeaVR-Unity/Assets/Oculus/
    ```

---

## Main Features

- Real-time streaming of camera images and graph data.
- Precise tracking of left and right hand keypoints.
- Network-driven 2D visualization.

---

## Networking Details

- `NetworkManager.cs`:
  - Manages network connections with configurations including:
    - `IPAddress`
    - `rightkeyptPortNum`
    - `leftkeyptPortNum`
    - Camera, graph, resolution, and pause ports.

- Default port configurations are defined in:
  ```
  BeaVR-Unity/Assets/Resources/Configurations/Network.json
  ```

---

## How Data Flows

- `GestureDetector.cs`:
  - Sends hand-tracking keypoints via NetMQ sockets.
  - Uses methods `getRightKeypointAddress()` and `getLeftKeypointAddress()`.
  - The `Update()` method switches from a menu to real-time streaming once connections are established.

- **CameraOneStreamer.cs** and **GraphStream.cs**:
  - Receive real-time camera and graph feeds using addresses from `NetworkManager.cs`.

---

## Usage

1. **Configure IP Address:**
   - Edit through `FieldInputManager.cs` or Unity’s `PlayerPrefs`.

2. **Launching Application:**
   - Shows a menu on startup if no connection is detected.
   - Connect VR equipment to transition automatically into streaming.

3. **Testing & Deployment:**
   - Ensure Oculus hardware is properly configured for network streaming.

---

## Troubleshooting

- Verify IP and port configurations.
- Check network connectivity if menu persists on startup.

---

## Future Plans

- Enhanced resolution and streaming settings.
- Advanced logging and error handling.
- Multi-user capabilities and additional Unity scenes.

---

## License

[Insert License Information Here]

---

## Contact & Support

For support, contributions, or issues:
- **Email:** [Your Contact Email]
- **GitHub Issues:** Open an issue directly on GitHub.

