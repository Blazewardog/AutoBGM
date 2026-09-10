# Auto BGM

Automatically mute Final Fantasy XIV's background music while your music player is playing, and restore it when playback pauses or stops.

Auto BGM is a [Dalamud](https://github.com/goatcorp/Dalamud) plugin with native Windows media detection and Linux/Wine support through a lightweight helper.

## Features

- Uses the game's existing BGM toggle without changing volume or other sound categories.
- Preserves BGM that was already muted before playback began.
- Supports multiple players: BGM stays muted while any matching player is playing.
- Offers a Windows app picker with saved selections and advanced filters.
- Restores BGM when the plugin is disabled or the Linux helper disconnects.
- Reconnects to the helper automatically.

## Installation

Requires Dalamud API 15. Build the plugin using the instructions below, then:

1. Add `AutoBgm.dll` to Dalamud's **Dev Plugin Locations**.
2. Enable **Auto BGM** in the development plugin installer.
3. Open the plugin settings from the installer or enter `/autobgm` in game.

Keep the generated manifest and dependencies alongside the DLL, including `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll`. Under Wine, select the DLL using its Windows path, such as `Z:\home\you\...\AutoBgm.dll`.

Linux users also need the [Linux helper](#linux-and-wine).

## Usage

Enable automation and choose a **Detection Mode**. **Auto** selects the Linux helper under Wine and Windows media sessions on Windows. **Detection Status** shows the current playback or connection state.

Start music to mute game BGM; pause, stop, or close all matching players to restore it. Normal response time is approximately one second. The plugin can operate at the main menu as well as in game.

### Windows

No helper is required. Under **Windows Settings**, open **Music apps** and select the apps to monitor. Start playback in an app to make its media session discoverable. Saved selections remain listed after apps close.

An empty selection monitors all media apps. **Advanced filters** accepts comma-separated application ID substrings, such as `Spotify`.

## Linux and Wine

The helper runs natively on Linux, outside Wine, as your desktop user. It reads [MPRIS](https://specifications.freedesktop.org/mpris-spec/latest/Player_Interface.html) playback status through [playerctl](https://github.com/altdesktop/playerctl) and sends it to the plugin over localhost TCP.

### Requirements

- Python 3.10 or newer.
- `playerctl`, installed through your distribution's package manager.
- A media player with MPRIS support, running in the same desktop/D-Bus session.

### Run manually

From the repository directory:

```sh
playerctl --all-players status
python3 helper/autobgm_helper.py
```

Leave the helper running while playing. In the plugin, use **Auto** or **Linux helper** detection. The default port is `37984`; if you change it with `--port`, update **Helper port** in the plugin too.

To limit detection to particular players:

```sh
playerctl --list-all
python3 helper/autobgm_helper.py --players spotify,vlc
```

To exclude players:

```sh
python3 helper/autobgm_helper.py --ignore firefox,chromium
```

Run `python3 helper/autobgm_helper.py --help` for all options.

### Automatic startup with systemd

An optional [systemd user unit](helper/autobgm-helper.service) is included for desktops that activate `graphical-session.target`. It starts with your desktop session and stops when that session ends. Run these commands as your normal user, without `sudo`.

Install the helper as an executable named `autobgm-helper` in a directory on your PATH. For example, from this repository:

```sh
install -Dm755 helper/autobgm_helper.py "$HOME/.local/bin/autobgm-helper"
install -Dm644 helper/autobgm-helper.service "$HOME/.config/systemd/user/autobgm-helper.service"
```

The unit uses `env autobgm-helper` to look up the executable on the **systemd user manager's PATH**. This can differ from your terminal's PATH. For a persistent PATH containing `~/.local/bin`, create an [environment.d](https://www.freedesktop.org/software/systemd/man/latest/environment.d.html) configuration:

```sh
mkdir -p "$HOME/.config/environment.d"
cat > "$HOME/.config/environment.d/60-autobgm.conf" <<'CONFIG'
PATH=${HOME}/.local/bin:${PATH}
CONFIG
systemctl --user daemon-reload
systemctl --user enable --now autobgm-helper.service
```

The helper can live in any directory included in that PATH, or you can symlink it into `~/.local/bin` as `autobgm-helper`. No edits to the service unit are needed. If the directory is only added in your shell configuration, that alone does not make it available to systemd; add it to your user environment configuration as well. Python and playerctl must also be available on the service's PATH.

Check status and logs:

```sh
systemctl --user status autobgm-helper.service
journalctl --user -u autobgm-helper.service
```

Stop automatic startup:

```sh
systemctl --user disable --now autobgm-helper.service
```

If your desktop does not use systemd's graphical session target, use its startup-applications feature to run the helper instead.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| Windows backend selected under Wine | Select **Linux helper** explicitly in the plugin settings. |
| Helper unavailable | Check that the helper is running and both ports match. |
| Player not detected on Linux | Run `playerctl --all-players status` as the same desktop user. Confirm your player supports MPRIS. |
| Service cannot find the helper | Check the executable name, permissions, and systemd user PATH. |
| Browser videos mute BGM | Limit detection to your music apps using filters. |
| BGM stays off after a crash | Reload the plugin to recover its saved mute state, or enable BGM in the game's sound settings. |

Playback detection uses the state reported by media apps. A muted player or video can still report `Playing`; this is not audio-level or music-content detection. Apps without MPRIS or Windows media-session support cannot be detected.

The helper binds only to `127.0.0.1` and sends no track metadata. Its protocol is unauthenticated and intended for a trusted local machine. Wine and the helper must share a network namespace; container or Flatpak isolation may require additional setup. Missing or stale helper status restores BGM within approximately three seconds.

## Development

Requires the .NET 10 SDK and a compatible Dalamud installation. The SDK finds standard XIVLauncher installations automatically; set `DALAMUD_HOME` for a custom location.

```sh
dotnet build AutoBgm.slnx -c Release --locked-mode
```

Output:

- Plugin and dependencies: `AutoBgm/bin/x64/Release/`
- Distributable archive: `AutoBgm/bin/x64/Release/AutoBgm/latest.zip`

Run checks:

```sh
dotnet run --project tests/AutoBgm.Tests
python3 -m unittest discover -s helper -v
```

Tests cover BGM ownership and restoration, crash recovery, failed writes, player status parsing, and helper socket behavior. Linux/Wine playback and restoration have been manually confirmed at the main menu. Native Windows detection and the Windows app picker still need live validation.

## Credits and license

Based on [goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin). Built using Dalamud's public configuration API and bundled dependencies. Linux playback detection uses playerctl.

Licensed under [AGPL-3.0](LICENSE.md).
