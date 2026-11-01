# RemoteLink development status

Last checked: 2026-11-01

## Target platforms
- Windows 11 host
- Xiaomi 15 Pro (Android / HyperOS) client

## Implemented in the current development branch
- .NET 8 WPF Windows host with visible start/stop controls.
- LAN pairing API on TCP port 43821.
- Six-digit, single-use pairing code with a five-minute expiry.
- Per-IP attempt throttling and one active paired client at a time.
- Random 256-bit session token returned only after successful pairing.
- Bearer-token session validation and explicit session-close endpoint.
- Android client accepts host IP and pairing code, performs the real HTTP pairing request, and stores the session token with Android secure storage.
- Android client can validate an existing session and request disconnection.
- GitHub Actions builds Windows x64 and Android release artifacts.

## Current limitations
- Pairing is LAN-only and uses HTTP; do not expose TCP 43821 to the public internet.
- The six-digit pairing code is suitable only for this local prototype. Production remote access requires TLS, stronger abuse controls, device identity and a hardened authentication/signaling service.
- No desktop capture, video encoding, WebRTC, remote mouse/keyboard input, remote audio, clipboard, file transfer, camera streaming, STUN/TURN or cross-network operation yet.
- No Windows tray/service, autostart, signed installer or physical-device end-to-end test yet.

## Next milestones
1. Verify Windows and Android CI builds; install and test LAN pairing on Windows 11 and Xiaomi 15 Pro.
2. Implement authenticated desktop capture and video transport with visible session consent/stop control.
3. Add WebRTC signaling, STUN/TURN and cross-network connectivity.
4. Add touch/mouse/keyboard, audio, clipboard, file transfer and camera support.
5. Add robust packaging, signing and end-to-end regression tests.

## Release criteria
Do not label the project as a usable remote desktop release until both installable packages build successfully and a real device-to-device desktop session has been tested.
