using AutoBgm.Detection;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AutoBgm;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration config;
    private readonly BgmController bgm;
    private PlaybackMonitor monitor;
    private bool visible;
    private bool disposed;
    private long nextUpdate;
    private long nextErrorLog;
    private int helperPortDraft;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new();
        config.HelperPort = Math.Clamp(config.HelperPort, 1024, 65535);
        helperPortDraft = config.HelperPort;
        bgm = new BgmController(
            () => GameConfig.TryGet(SystemConfigOption.IsSndBgm, out bool muted)
                ? muted : throw new InvalidOperationException("BGM setting unavailable"),
            muted => GameConfig.Set(SystemConfigOption.IsSndBgm, muted),
            () => config.RestorePending,
            pending =>
            {
                var previous = config.RestorePending;
                config.RestorePending = pending;
                try { config.Save(); }
                catch { config.RestorePending = previous; throw; }
            });
        monitor = new(config.Mode, config.HelperPort, config.WindowsPlayers);
        CommandManager.AddHandler("/autobgm", new CommandInfo(OnCommand) { HelpMessage = "Open Auto BGM settings and playback status." });
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenConfigUi += Open;
        PluginInterface.UiBuilder.OpenMainUi += Open;
        Framework.Update += Update;
    }

    private void OnCommand(string command, string args) => Open();
    private void Open() => visible = true;

    private void Update(IFramework framework)
    {
        if (disposed || Environment.TickCount64 < nextUpdate) return;
        nextUpdate = Environment.TickCount64 + 250;
        try
        {
            var state = monitor.State;
            bgm.Update(config.Enabled && state.Playing && Environment.TickCount64 - state.UpdatedAt < 3000);
        }
        catch (Exception ex)
        {
            if (Environment.TickCount64 < nextErrorLog) return;
            nextErrorLog = Environment.TickCount64 + 10000;
            Log.Error(ex, "Unable to update the game BGM setting; will retry.");
        }
    }

    private void Draw()
    {
        if (!visible) return;
        if (ImGui.Begin("Auto BGM", ref visible, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var enabled = config.Enabled;
            if (ImGui.Checkbox("Enabled", ref enabled))
            {
                config.Enabled = enabled;
                config.Save();
            }

            var mode = (int)config.Mode;
            if (ImGui.Combo("Detection Mode", ref mode, "Auto\0Windows\0Linux helper\0"))
            {
                config.Mode = (DetectionMode)mode;
                SaveAndRestartMonitor();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextUnformatted("Detection Status");
            ImGui.TextUnformatted(monitor.State.Detail);
            if (config.Mode == DetectionMode.Auto)
                ImGui.TextUnformatted($"Auto selected: {monitor.Backend}");
            if (config.Enabled && config.RestorePending)
                ImGui.TextUnformatted("Game BGM is automatically muted.");

            ImGui.Spacing();
            if (monitor.UsesLinuxHelper) DrawLinuxSettings();
            else DrawWindowsSettings();
        }
        ImGui.End();
    }

    private void SaveAndRestartMonitor()
    {
        config.Save();
        monitor.Dispose();
        monitor = new(config.Mode, config.HelperPort, config.WindowsPlayers);
    }

    private void DrawLinuxSettings()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("Linux Settings");
        ImGui.InputInt("Helper port", ref helperPortDraft);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            config.HelperPort = Math.Clamp(helperPortDraft, 1024, 65535);
            helperPortDraft = config.HelperPort;
            SaveAndRestartMonitor();
        }
    }

    private void DrawWindowsSettings()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("Windows Settings");
        var selected = config.WindowsPlayers.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var preview = selected.Count == 0 ? "All media apps" : $"{selected.Count} app filter(s)";
        if (ImGui.BeginCombo("Music apps", preview))
        {
            if (ImGui.Selectable("All media apps", selected.Count == 0))
            {
                selected.Clear();
                SaveWindowsFilters(selected);
            }
            var detected = monitor.DetectedPlayers;
            var choices = detected.Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var app in choices)
            {
                var included = selected.Contains(app);
                ImGui.PushID(app);
                if (ImGui.Checkbox(app, ref included))
                {
                    if (included) selected.Add(app);
                    else selected.Remove(app);
                    SaveWindowsFilters(selected);
                }
                if (!detected.Contains(app, StringComparer.OrdinalIgnoreCase))
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("(not currently detected)");
                }
                ImGui.PopID();
            }
            if (detected.Length == 0)
                ImGui.TextUnformatted("Open a media app and start playback to discover it.");
            ImGui.EndCombo();
        }
        ImGui.TextUnformatted("No filters selects all media apps, including browsers.\nSelected apps stay saved when closed.");
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.TreeNode("Advanced filters"))
        {
            var filters = config.WindowsPlayers;
            if (ImGui.InputText("App ID filters", ref filters, 512))
            {
                config.WindowsPlayers = filters;
                config.Save();
                monitor.SetWindowsPlayers(filters);
            }
            ImGui.TextUnformatted("Comma-separated app ID substrings.");
            ImGui.TreePop();
        }
    }

    private void SaveWindowsFilters(IEnumerable<string> filters)
    {
        config.WindowsPlayers = string.Join(",", filters.Order(StringComparer.OrdinalIgnoreCase));
        config.Save();
        monitor.SetWindowsPlayers(config.WindowsPlayers);
    }

    public void Dispose()
    {
        disposed = true;
        Framework.Update -= Update;
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= Open;
        PluginInterface.UiBuilder.OpenMainUi -= Open;
        CommandManager.RemoveHandler("/autobgm");
        monitor.Dispose();
        // Dalamud runs this directly if already on the framework thread.
        _ = Framework.RunOnFrameworkThread(() =>
        {
            try { bgm.Restore(); }
            catch (Exception ex) { Log.Error(ex, "BGM restoration failed; recovery remains saved for the next load."); }
        });
    }
}
