# RemoteLink

RemoteLink is being developed for a **Windows 11 host** and an **Android client**, with Xiaomi 15 Pro / HyperOS as the initial mobile test target.

## Current development stage

The current branch implements an early **LAN pairing + consent-gated desktop preview prototype**:

- `windows-host/`: .NET 8 WPF host with visible start/stop controls, a LAN pairing API on TCP 43821, and a separate **允許桌面預覽** consent control.
- Pairing uses a six-digit, single-use code (five-minute expiry), per-IP attempt throttling, one active client, and a random 256-bit bearer token.
- Desktop preview is served by an authenticated `GET /desktop.jpg` endpoint. The Windows user must explicitly enable preview after pairing.
- The capture prototype reads the primary interactive desktop using Windows GDI, scales to at most 1280 px wide, and returns JPEG.
- `android-app/`: Flutter client performs pairing, stores the token using secure storage, checks session validity, disconnects, and has a desktop preview page that polls for a new JPEG approximately once per second.
- `.github/workflows/build.yml`: builds a self-contained Windows x64 publish output and an Android release APK.

**Important limitations:** this is not yet a full remote desktop. Pairing and preview are LAN-only and currently use HTTP; do not forward TCP 43821 to the public internet. The preview is low-rate JPEG polling, not WebRTC or real-time video. Only the primary interactive desktop is captured; Windows secure desktop/UAC prompts are not captured. Remote mouse/keyboard input, audio, clipboard, file transfer, camera streaming, STUN/TURN, cross-network operation, Windows tray/service, and a signed installer are not implemented.

## Build outputs

Push to `main` or a `feature/**` branch, or run the workflow manually from GitHub Actions.

When both jobs succeed, download:
- `RemoteLink-Windows-x64` — Windows publish files, including the executable.
- `RemoteLink-Android-APK` — Android release APK.

The Windows artifact is a self-contained publish folder, not yet a signed installer. The Android APK is installable.

## Local development

### Windows host

Requires Windows 10/11 and .NET 8 SDK:

```powershell
dotnet publish windows-host/RemoteLink.Host.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/windows
```

### Android client

Requires Flutter stable and Android SDK:

```powershell
cd android-app
flutter create --platforms=android --org com.remotelink .
flutter pub get
flutter build apk --release
```

## LAN pairing and preview test

1. Start RemoteLink Host on Windows and allow the app through Windows Defender Firewall on a **Private** network when prompted.
2. Keep the phone and PC on the same trusted Wi-Fi network.
3. Enter the host IPv4 address and six-digit code in Android.
4. After pairing, press **允許桌面預覽** in the Windows host window.
5. In Android, press **查看 Windows 桌面**. The app should receive a refreshed JPEG frame about once per second.
6. Stop preview in Windows when finished, or use **解除配對** / **停止主機** to revoke the session.

Do not expose the prototype endpoint to the public internet. HTTP bearer tokens are not encrypted in transit; use only a trusted LAN for this prototype.

## Security and release criteria

Remote access must remain visible and user-controllable. First-time pairing and desktop-preview consent must be explicit; the Windows host must provide an obvious stop control. Do not expose a hidden remote-control path or bypass Windows sign-in/security.

A release must not be called complete until both packages build, install on the target devices, and complete a real authenticated remote desktop session with tested reconnect and stop behavior.
