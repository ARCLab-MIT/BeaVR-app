# BeaVR-Unity

This Unity project, built on Unity 2021.3.45f, provides a VR-based motion tracking experience. It streams real-time hand-tracking data, camera feeds, and graphical data over a network, and simultaneously receives image streams for direct display on a 2D projection. The primary visualization includes hand keypoints, which indicate the status of the connection by mirroring hand movements.

---
## Table of Contents

1. [Project Structure](#project-structure)
2. [Main Features](#main-features)
3. [Networking Details](#networking-details)
4. [How Data Flows](#how-data-flows)
5. [Usage](#usage)
6. [Oculus Integration Setup](#oculus-integration-setup)
7. [Troubleshooting](#troubleshooting)
8. [Future Plans](#future-plans)
9. [License](#license)
10. [Contact & Support](#contact--support)

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

- **NetworkManager.cs**:
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

- **GestureDetector.cs**:
- Sends hand-tracking keypoints via NetMQ sockets.
- Uses methods `getRightKeypointAddress()` and `getLeftKeypointAddress()`.
- The `Update()` method switches from a menu to real-time streaming once connections are established.

- **CameraOneStreamer.cs** and **GraphStream.cs**:
- Receive real-time camera and graph feeds using addresses from `NetworkManager.cs`.

---

## Usage

1. **Configure IP Address:**
 - On the server, run `hostname -I` to obtain the IP address.
 - Input the obtained IP address directly into the application.

2. **Launching Application:**
 - Shows a menu on startup if no connection is detected.
 - Connect VR equipment to transition automatically into streaming.

3. **Testing & Deployment:**
 - Ensure Oculus hardware is properly configured for network streaming.

---

## Oculus Integration Setup

To use the latest Oculus Integration package rather than the pre-bundled folder:

1. **Remove Existing Folder:**
 - In your Unity project, delete or remove the entire folder:
   ```
   BeaVR-Unity/Assets/Oculus/
   ```

2. **Download Oculus Integration (Deprecated)**:
 - In Unity, go to **Window** → **Package Manager**.
 - In the **Package Manager** window, switch the filter from "In Project" to "My Assets" (top-left corner).
 - Find **Oculus Integration (Deprecated)** from the [Unity Asset Store](https://assetstore.unity.com/packages/tools/integration/oculus-integration-deprecated-82022).
 - **Download** and then **Import** the integration package into the project.

3. **Complete Setup**:
 - Allow Unity to compile and re-import the necessary files for Oculus VR support.
 - Check that you have the newly imported Oculus Integration scripts under `BeaVR-Unity/Assets/Oculus/`.

---

## Troubleshooting

- Verify IP and port configurations.
- Check network connectivity if the menu persists on startup.
- Ensure the Oculus Integration is installed and that the Oculus VR hardware is properly recognized.

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Contact & Support

For support, contributions, or issues:
- **Email:** [Your Contact Email]
- **GitHub Issues:** Open an issue directly on GitHub.