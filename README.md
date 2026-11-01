# RemoteLink

RemoteLink is being developed for a **Windows 11 host** and an **Android client**, with Xiaomi 15 Pro / HyperOS as the initial mobile test target.

## Current development stage

The current branch implements a first **LAN pairing and authorization prototype**:

- `windows-host/`: .NET 8 WPF host with visible start/stop controls and an ASP.NET Core pairing API on TCP 43821.
- `android-app/`: Flutter client that submits a host address and six-digit pairing code, securely stores the returned session token, checks session validity, and requests disconnection.
- Pairing codes are single-use and expire after five minutes. The host throttles repeated attempts and allows one paired client at a time.
- `.github/workflows/build.yml`: builds a self-contained Windows x64 publish output and an Android release APK.

**Important limitations:** this is not yet a remote desktop. Pairing is LAN-only and currently uses HTTP; do not forward TCP 43821 to the public internet. Desktop capture, video encoding, WebRTC/H.264, remote input, audio, clipboard, file transfer, camera streaming, STUN/TURN, cross-network operation, Windows tray/service and a signed installer are not implemented.

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

## LAN pairing test

1. Start RemoteLink Host on Windows and allow the app through Windows Defender Firewall on a **Private** network when prompted.
2. Keep the phone and PC on the same trusted Wi-Fi network.
3. Copy the IPv4 address and six-digit code shown by the host into the Android app.
4. Pair, then use **檢查連線** to validate the bearer-token session.
5. Use **解除配對** in Android or **停止主機** in Windows to revoke the session.

Do not expose the prototype pairing endpoint to the public internet. The current HTTP transport is for trusted LAN testing only.

## Security and release criteria

Remote access must remain visible and user-controllable. First-time pairing and session authorization must be explicit; the Windows host must provide an obvious stop/disconnect control. Do not expose a hidden remote-control path or bypass Windows sign-in/security.

A release must not be called complete until both packages build, install on the target devices, and complete a real authenticated remote desktop session with tested reconnect and stop behavior.
