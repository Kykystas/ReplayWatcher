# Replay Watcher 0.3.8

A simple Windows app that restores NVIDIA Instant Replay after it turns off and optionally checks your microphone.

Made by Kykystas with the help of AI

**Status: portable preview release.** This project is not affiliated with NVIDIA and uses undocumented overlay data. The app currently has a Russian-language interface, a red-and-black theme, and a green pixel-art chameleon icon.

## Getting started

Extract the **entire** archive and run `ReplayWatcher.exe`. Both NAudio DLL files must remain next to the EXE. Administrator privileges are not required.

Requirements: Windows 10/11 x64, .NET Framework 4.8, and NVIDIA App or GeForce Experience with the overlay enabled. Audio playback uses Windows Media Foundation codecs; Windows N editions may require the Media Feature Pack.

On first launch, automatic replay recovery is enabled and microphone monitoring is disabled. Windows startup can be enabled separately in the app. If Windows or an antivirus blocks the file, read the Windows and antivirus warnings section below.

## Instant Replay

- Checks replay status every 2 seconds and attempts recovery after replay has been off for a few seconds.
- A manual check immediately refreshes NVIDIA status and connected microphones when microphone monitoring is enabled. The footer shows the time and result; the automatic check time updates every 2 seconds.
- Recovery attempts are limited to once every 10 seconds.
- No toggle is sent when replay is already on or its status is unknown.
- Keyboard input is postponed while modifier keys are held or the screen is locked.
- Automatic recovery can be turned off.
- The Windows startup option creates a shortcut in the current user's Startup folder.
- Closing the window minimizes the app to the system tray. Use the exit button to quit.
- Tray icon: a **green chameleon** means replay is on, **red** means it is off, and **gray** means its status is unknown or checking is unavailable. Hover over the icon for a status tooltip.

The NVIDIA flag `{1B1D3DAA-601D-49E5-8508-81736CA28C6D}` under `HKCU\Software\NVIDIA Corporation\Global\ShadowPlay\NVSPCAPS` is undocumented. It has been checked with NVIDIA App 11.0.7; compatibility with other versions is not guaranteed. Finding an installation does not guarantee compatibility with this flag. Values other than binary 0/1 are treated as unknown. The `IsShadowPlayEnabled` and `irEnabled` preferences are not used as evidence of an active replay buffer.

The app does not bypass DRM. While Spotify, protected video, or an active computer-control session blocks NVIDIA capture, replay may turn off again immediately. Recovery attempts continue after the block is removed. Hidden helper windows do not block the app's recovery logic.

## Optional microphone monitoring

**Disabled by default.** When enabled, the app reads Windows Core Audio meters: the selected microphone's level and mute state, active NVIDIA capture sessions, and their signal levels. Monitoring does not open a separate audio recording, save your voice, or transmit audio. Turning monitoring off releases the devices and stops polling them.

### Record and listen to a test

The recording test window provides three actions:

1. **Start recording** starts a regular NVIDIA recording. Screen and audio capture follow NVIDIA settings, including the microphone selected there. Choosing a device for the main window's meter does not change NVIDIA's microphone.
2. **Stop** finishes the test recording started by the app and waits for the video to be saved. Recording stops automatically after 30 seconds.
3. **Listen** plays the saved video's audio inside the app. Pressing it again stops playback. When there are two audio tracks, the second is selected by default because NVIDIA commonly uses it for the microphone. This is an assumption based on track order, not source detection; you can select another track.

The video stays in NVIDIA's normal recording folder and is not uploaded. To play a selected audio track, the app temporarily decodes it into a local WAV file, which is deleted after playback or when the test window closes. A crash may leave the temporary WAV in the Temp folder. Listening lets you check your actual voice: a nonzero audio level alone does not prove that the microphone was recorded.

The recording test is available independently of background microphone monitoring. Automatic Instant Replay toggle commands are paused while the test window is open and resume when it closes.

## Data and privacy

Settings and logs are stored in `data/` next to the app. If that folder is not writable, they are stored in the user's Local AppData instead. The diagnostics dialog shows the exact location. Logs contain no audio and do not record typed text.

Only the build script uses the internet to download pinned dependencies; the application itself makes no network requests. A diagnostic file created with `--diagnose` may contain local paths and a device name, so review it before sharing. The normal recording test creates a video only after you explicitly start recording.

Current status is kept in memory: `status.txt` is no longer created or updated. A file left by an older version is not used. Events are written to `watcher.log`; once it exceeds 1 MB, the previous log is rotated to `watcher.log.previous`.

## Windows and antivirus warnings

### SmartScreen and code signing

Version 0.3.8 **does not have a publisher's digital signature**. A "Windows protected your PC" warning about an unrecognized app is a SmartScreen reputation warning. It is not, by itself, a detection of a specific virus, and it does not establish that a file is safe. Microsoft considers the reputation of both the file and its publisher; even a valid signature does not guarantee that a new build will show no warnings. [Microsoft's explanation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

### What the app does

- Reads NVIDIA settings and running-process information to find the overlay and configured hotkeys.
- Sends the configured hotkeys through Windows `SendInput` to control replay and explicitly requested test recordings.
- Checks modifier-key states with `GetAsyncKeyState` to avoid sending shortcuts while keys are already held. Typed text is not recorded.
- Reads microphone and NVIDIA audio-session levels when monitoring is enabled.
- Creates a normal startup shortcut when you enable that option. Administrator privileges are not required.

## License

MIT License. Copyright (c) 2026 Kykystas. See `LICENSE` for the full license and `THIRD-PARTY-NOTICES.txt` for third-party notices.

## Changes in 0.3.8

- Fixed corrupted small icons in .NET Framework by using DIB frames in ICO files.
- Added Windows rendering checks for the green, red, and gray tray icons.
- Updated installation, privacy, and antivirus-warning documentation. Added a SHA-256 checksum list to the portable archive.
