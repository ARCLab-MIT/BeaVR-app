# 🦫 BeaVR

> **VR teleoperation for robotics** - Stream your hand movements to control robots in real-time

BeaVR is a VR application for Meta Quest that enables intuitive robot teleoperation. Put on your headset, see through the robot's camera, and control it naturally with your hands.

---

## What It Does

- **Natural Hand Control**: Your hand movements directly control the robot
- **Live Camera Feed**: See what the robot sees in real-time
- **Visual Feedback**: Monitor system status and data streams
- **Network-Based**: Connects to your robot over WiFi

---

## Controls

### Hand Gestures
| Gesture | Action |
|---------|--------|
| 👌 **Index-Thumb Pinch** | Activate teleoperation mode |
| 🤏 **Middle Finger Pinch** | Pause streaming |
| ✊ **Ring Finger Pinch** | Enable "Return to Menu" button |

### Menu Navigation
- **On Startup**: Configure the server IP address using the in-VR keyboard
- **During Operation**: The current IP is displayed in the main UI
- **IP Address**: Can be changed anytime from the menu using the virtual keyboard

---

## Quick Start

1. **Launch the app** on your Meta Quest
2. **Enter IP address** of your robot/server using the virtual keyboard
   - Find your server IP by running `hostname -I` on the server machine
3. **Pinch index-thumb** to start teleoperation
4. **See the border turn green** when connected successfully
5. **Control the robot** with natural hand movements!

---

## How It Works

```
┌─────────────┐                  ┌──────────────┐
│  Meta Quest │  Hand Tracking   │    Server    │
│   (BeaVR)   │ ────────────────>│   (Robot)    │
│             │                  │              │
│             │  Camera Stream   │              │
│             │ <────────────────│              │
└─────────────┘                  └──────────────┘
```

1. **Hand Tracking**: Your hand movements are captured at high precision
2. **Network Streaming**: Hand data is sent to the server over WiFi
3. **Visual Feedback**: Robot's camera feed and status info stream back to your headset
4. **Low Latency**: Optimized for responsive control

The app maintains the same core networking and functionality while now using an improved IP management system.

---

## Requirements

- **Meta Quest** (Quest 2, Quest Pro, Quest 3)
- **WiFi connection** (same network as server)
- **Robot/Server** running compatible receiver

---

## Development

For Unity development and technical details, see [`BeaVR-Unity/README.md`](BeaVR-Unity/README.md)

**Built with**: Unity 6.2, OpenXR, XR Hands

---

## License

This project is licensed under the [MIT License](LICENSE).
