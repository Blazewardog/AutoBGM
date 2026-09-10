# Auto BGM

Enjoy your own music in Final Fantasy XIV. Auto BGM automatically mutes the game's background music while your music player is playing, then restores it when you pause, stop, or close the player.

- Keeps your volume settings, dialogue, and sound effects unchanged.
- Leaves BGM off if you had already muted it yourself.
- Works on Windows and Linux/Wine, including the main menu.
- Supports filtering to your preferred music apps.

## Install

You need [XIVLauncher with Dalamud](https://goatcorp.github.io/). No compilation or developer plugin setup is required for released versions.

> Installation through this feed becomes available once the first GitHub release is published.

1. Open Dalamud settings from `/xlplugins`.
2. Go to **Experimental → Custom Plugin Repositories**.
3. Add this URL, enable the entry, and save:

   ```text
   https://github.com/Blazewardog/AutoBGM/releases/latest/download/pluginmaster.json
   ```

4. Search for **Auto BGM** in the plugin installer and install it.
5. Open its settings from the installer or enter `/autobgm` in game.

Future plugin updates are available through Dalamud's plugin installer. Release downloads and notes are on the [Releases page](https://github.com/Blazewardog/AutoBGM/releases).

**Linux users:** also follow the [Linux setup](#linux-setup) below. Windows needs no helper.

## Use

Leave **Enabled** checked and **Detection Mode** set to **Auto**. Start music in your player: game BGM should mute within about a second. Pause or stop all monitored players to restore it.

**Detection Status** shows playback or connection status. Disabling Auto BGM restores BGM if the plugin muted it.

### Windows music apps

Under **Windows Settings**, open **Music apps** and check the apps you want to monitor. Start playback in an app to make it appear. Your selections stay saved when apps close.

No filters means all detected media apps, including browsers. **Advanced filters** lets you enter application ID substrings manually if needed.

## Linux setup

On Linux/Wine, a small helper runs outside the game and reports your music player's status to the plugin. It runs as your normal desktop user, outside Wine.

### 1. Install dependencies

Install **Python 3.10 or newer** and **playerctl** using your distribution's package manager. Your player needs MPRIS support; many Linux media players provide it.

Check that your player is detected:

```sh
playerctl --all-players status
```

### 2. Download and run the helper

Download [autobgm_helper.py](https://github.com/Blazewardog/AutoBGM/releases/latest/download/autobgm_helper.py) from the latest release. Open a terminal in the folder where you saved it and run:

```sh
python3 autobgm_helper.py
```

Keep it running while playing FFXIV. In Auto BGM settings, **Auto** should select the Linux backend; you can also choose **Linux helper** explicitly.

If you want it to start when you log in, add that command with the full path to the downloaded script to your desktop's startup applications.

### Choose which players count

List available players:

```sh
playerctl --list-all
```

Monitor only selected players:

```sh
python3 autobgm_helper.py --players spotify,vlc
```

Or exclude players:

```sh
python3 autobgm_helper.py --ignore firefox,chromium
```

Use `--help` for all options. The default connection port is `37984`; if you change it with `--port`, use the same **Helper port** in the plugin settings.

The helper reconnects automatically. If it stops or disconnects, Auto BGM restores game music within roughly three seconds. To update the helper, stop it, replace the downloaded script with the latest release's copy, and start it again. Dalamud updates the plugin separately.

## Troubleshooting

| Problem | Try this |
| --- | --- |
| Linux shows the Windows backend | Select **Linux helper** in Detection Mode. |
| Helper unavailable | Start the helper outside Wine and check that its port matches the plugin. |
| Linux player not detected | Run `playerctl --all-players status` in your desktop session and check player filters. |
| Browser videos mute game music | Limit detection to your preferred music apps. |
| BGM remains off after a crash | Reload Auto BGM to restore its saved state, or turn BGM on in the game's sound settings. |
| Plugin is missing from the installer | Confirm the custom repository is enabled and a release compatible with your Dalamud version exists. |

Detection follows the playback state reported by apps, so muted players and videos can still count as playing. Apps without MPRIS or Windows media-session support cannot be detected. While automation is active, disable it before manually overriding the BGM toggle.

The Linux helper only listens on localhost. Its connection is unauthenticated: other local users or programs can read the playing/not-playing status or impersonate the helper to affect BGM. Use it on a trusted local machine. Container or Flatpak network isolation can prevent Wine from reaching it.

## Feedback

Report problems through [GitHub Issues](https://github.com/Blazewardog/AutoBGM/issues). Include your OS, Detection Mode, Detection Status, media player, and relevant `/xllog` output or helper errors.

Linux/Wine playback and restoration have been tested at the main menu. Native Windows detection and its app picker still need live testing.

## Development and license

For building, testing, and publishing, see [DEVELOPMENT.md](DEVELOPMENT.md).

Based on [Dalamud SamplePlugin](https://github.com/goatcorp/SamplePlugin). Licensed under [AGPL-3.0](LICENSE.md).
