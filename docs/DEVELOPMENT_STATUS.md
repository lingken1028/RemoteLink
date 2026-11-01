# RemoteLink development status

Last checked: 2026-11-01

## Target platforms
- Windows 11 host
- Xiaomi 15 Pro (Android / HyperOS) client

## Current repository state
This repository currently contains only a minimal README. A working remote desktop application has not yet been implemented or verified.

## Required implementation milestones
1. Windows interactive-session host: desktop capture, encoder, input injection with explicit user consent, tray status and stop control.
2. Android client: secure pairing, session UI, touch/mouse and keyboard controls.
3. Signaling and connectivity: authenticated signaling, reconnect, STUN/TURN for cross-network operation.
4. Media and peripherals: WebRTC video/audio, camera selection, clipboard and file transfer.
5. Packaging: signed or clearly identified Windows installer and installable Android APK.
6. Verification: build logs plus installation and end-to-end connection tests on Windows 11 and Xiaomi 15 Pro.

## Release criteria
Do not label the project as a usable release until both installable packages build successfully and a real device-to-device session has been tested.