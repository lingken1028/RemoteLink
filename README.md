# RemoteLink

RemoteLink is being developed for a **Windows 11 host** and an **Android client**, with Xiaomi 15 Pro / HyperOS as the initial mobile test target.

## Current development stage

This branch adds the first buildable UI skeletons and a GitHub Actions pipeline:

- `windows-host/`: .NET 8 WPF Windows host shell with visible host status and start/stop UI.
- `android-app/`: Flutter Android client shell with a six-digit pairing-code input.
- `.github/workflows/build.yml`: builds a self-contained Windows x64 publish output and a release Android APK, then uploads both as workflow artifacts.

**Important:** these are build-validation shells, not a functioning remote desktop product. Pairing is not connected to a signaling server. Desktop capture, WebRTC/H.264, remote input, audio, clipboard, file transfer, camera streaming, STUN/TURN, persistent authentication, Windows tray/service, and unattended reconnection are not implemented.

## Build outputs

Push to `main` or a `feature/**` branch, or run the workflow manually from GitHub Actions.

When both jobs succeed, download:
- `RemoteLink-Windows-x64` — Windows publish files, including the executable.
- `RemoteLink-Android-APK` — Android release APK.

The Windows artifact is a self-contained publish folder, not yet a signed installer. The Android APK is an installable build artifact, but the current app is only a pairing UI shell.

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

## Security and release criteria

Remote access must remain visible and user-controllable. First-time pairing and session authorization must be explicit; the Windows host must provide an obvious stop/disconnect control. Do not expose a hidden remote-control path or bypass Windows sign-in/security.

A release must not be called complete until both packages build, install on the target devices, and complete a real authenticated remote session with tested reconnect and stop behavior.
