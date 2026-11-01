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
- Android client performs real HTTP pairing, stores the session token with Android secure storage, validates sessions and requests disconnection.
- Windows user can explicitly enable/stop desktop preview after a client has paired.
- Authenticated `GET /desktop.jpg` endpoint captures the primary interactive desktop using Windows GDI, scales it to a maximum width of 1280 px and encodes JPEG. The endpoint limits capture requests to at most one frame per 800 ms.
- Android client polls the authenticated JPEG endpoint approximately once per second and displays the latest frame.
- GitHub Actions builds Windows x64 and Android release artifacts.

## Current limitations
- Pairing and preview are LAN-only and use HTTP. Do not expose TCP 43821 to the public internet; bearer tokens are not encrypted in transit.
- Desktop preview is low-rate JPEG polling, not real-time streaming/WebRTC.
- Only the primary interactive desktop is captured. Secure desktop/UAC prompts are intentionally not captured.
- No remote mouse/keyboard input, audio, clipboard, file transfer, camera streaming, STUN/TURN or cross-network operation yet.
- No Windows tray/service, autostart, signed installer or physical-device end-to-end test yet.

## Next milestones
1. Verify latest Windows and Android CI builds; install and test pairing and preview on Windows 11 + Xiaomi 15 Pro.
2. Replace JPEG polling with an authenticated WebRTC media pipeline and adaptive video encoding.
3. Add explicit remote input consent and touch/mouse/keyboard controls.
4. Add signaling, STUN/TURN and cross-network connectivity.
5. Add audio, clipboard, file transfer, camera support, robust packaging and end-to-end regression tests.

## Release criteria
Do not label the project as a usable remote desktop release until both installable packages build successfully and a real device-to-device desktop session has been tested.
