# Development

## Build

Install the .NET 10 SDK and Dalamud API 15. The Dalamud SDK finds standard XIVLauncher installations automatically; set `DALAMUD_HOME` if yours differs.

```sh
dotnet build AutoBgm.slnx -c Release --locked-mode
dotnet run --project tests/AutoBgm.Tests
python3 -m unittest discover -s helper -v
```

The plugin output is `AutoBgm/bin/x64/Release/`. Keep the DLL, manifest, and dependencies together. Add `AutoBgm.dll` to Dalamud's Dev Plugin Locations for local testing. Reload the plugin after rebuilding. The packager creates `AutoBgm/bin/x64/Release/AutoBgm/latest.zip`.

## Publish a release

1. Set `Version` in `AutoBgm/AutoBgm.csproj` to the intended `MAJOR.MINOR.PATCH` version.
2. Commit and push the reviewed changes, including the release workflow.
3. Create and push a matching tag, for example:

   ```sh
   git tag v0.1.0
   git push origin v0.1.0
   ```

The Release workflow builds the tagged source, runs C# and Linux helper checks, validates the package, and publishes a GitHub release containing:

- `AutoBgm.zip`: Dalamud plugin and runtime dependencies.
- `autobgm_helper.py`: standalone Linux helper.
- `pluginmaster.json`: Dalamud repository feed generated from the build manifest.

The feed's stable URL is `https://github.com/Blazewardog/AutoBGM/releases/latest/download/pluginmaster.json`. Each entry links to its exact tagged ZIP, keeping the advertised version and download consistent. No GitHub Pages or repository-branch writeback is required. Publish stable versions in increasing order: the workflow marks each published release as latest. Do not use it for prereleases or backport tags.

Assets are attached to a draft before publication so users do not receive a partially uploaded release. If publication fails after creating the draft, inspect that draft and its assets before retrying; the workflow does not overwrite existing releases. The feed does not exist until the first release is published. Repository Actions must permit the publish job's `contents: write` permission.

Validate release assets locally without publishing:

```sh
python3 scripts/prepare_release.py --tag v0.1.0 --output /tmp/autobgm-release
```

Follow Dalamud's [custom repository documentation](https://dalamud.dev/plugin-publishing/custom-repositories/) when changing the feed format.

## Manual validation

Verify playback/pause/stop, multiple players, initially muted BGM, disable/unload, helper failure/reconnection, and title-screen/zone transitions. Linux/Wine playback and restoration at the main menu have been confirmed by the maintainer; Windows detection and the app picker still need live testing.
