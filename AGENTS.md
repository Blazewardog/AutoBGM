# Auto BGM contributor guidance

## Project

Repository: https://github.com/Blazewardog/AutoBGM

Auto BGM is a Dalamud plugin based on goatcorp/SamplePlugin, targeting Dalamud API 15 with Dalamud.NET.Sdk 15.0.0 and .NET 10. Keep the existing AGPL-3.0 license and template attribution.

## Architecture and behavior

- `AutoBgm/Plugin.cs`: service injection, framework-thread BGM updates, and ImGui settings.
- `AutoBgm/BgmController.cs`: mute ownership, restoration, and crash recovery.
- `AutoBgm/Detection/PlaybackMonitor.cs`: backend selection, background worker, heartbeat timeout, and reconnection.
- `AutoBgm/Detection/WindowsPlayback.cs`: Windows media sessions and app discovery.
- `helper/autobgm_helper.py`: native Linux Python helper using playerctl/MPRIS, not Wine.
- `scripts/prepare_release.py` and `.github/workflows/release.yml`: validated release assets and custom Dalamud feed.

Use Dalamud's public `IGameConfig` API for `SystemConfigOption.IsSndBgm`: true means muted. Do not alter volume sliders or other sound categories. Read/write game configuration on the framework thread, not from the constructor or detection workers. Preserve initially muted BGM and the persisted ownership marker. Restore owned mutes on inactivity, disconnect, disable, and unload; keep recovery available after failures.

Use `Dalamud.Utility.Util.IsWine()` for Auto detection. A direct ntdll export probe failed in the maintainer's Wine environment. Keep Windows media API activation isolated from the Linux path.

The helper binds IPv4 loopback, default port 37984. Protocol: one ASCII byte per heartbeat, `1` playing, `0` inactive, `?` unavailable. Missing/stale data must not leave game BGM muted. Detection aggregates all matching media sessions. APIs report playback, not audible sound or music classification.

## UI and documentation preferences

Order: Enabled, Detection Mode, Detection Status, then only the active backend's settings. Use spacing and dividers. Keep Linux settings limited to plugin controls; do not duplicate helper CLI/filter help there. Windows has a detected-app checklist plus advanced manual filters.

Keep README.md written for end users: custom repository installation, usage, Linux helper download/setup, and troubleshooting. Keep build/release details in DEVELOPMENT.md. Do not add systemd units or systemd setup documentation; the maintainer explicitly removed this and handles their own service privately.

## Validation

```sh
dotnet build AutoBgm.slnx -c Release -p:RestoreLockedMode=true
dotnet run --project tests/AutoBgm.Tests
python3 -m unittest discover -s helper -v
```

Dalamud must be installed; its SDK resolves standard locations or `DALAMUD_HOME`. On the maintainer's Linux system, libraries are usually in `~/.xlcore/dalamud/Hooks/dev/`. Restore needs NuGet access; helper integration tests need localhost sockets. Do not confuse sandbox/network restrictions with implementation failures.

Build output: `AutoBgm/bin/x64/Release/AutoBgm.dll`; retain its manifest and Windows runtime dependencies. Packaged ZIP: `AutoBgm/bin/x64/Release/AutoBgm/latest.zip`. Run appropriate tests for changed behavior; do not claim Windows or in-game verification from compilation/unit tests. Linux/Wine playback and restoration have been manually confirmed at the main menu. Windows detection/picker remain unverified live.

## Releases

Keep InternalName/assembly name `AutoBgm` (repository spelling is AutoBGM). Version tags must match the csproj version: `vMAJOR.MINOR.PATCH`. The release script derives pluginmaster.json from the built manifest and pins install/update URLs to the exact tagged ZIP. The stable feed is a latest-release asset, not a checked-in hand-maintained manifest. Publish stable tags in increasing order. Do not claim an installation URL is live before its release exists. Follow the user's requested scope for commits, pushes, tags, and publication.
