using Dalamud.Configuration;

namespace AutoBgm;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public DetectionMode Mode { get; set; } = DetectionMode.Auto;
    public int HelperPort { get; set; } = 37984;
    public string WindowsPlayers { get; set; } = "";
    // Persist ownership before muting so a later load can recover after a crash.
    public bool RestorePending { get; set; }
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}

public enum DetectionMode { Auto, Windows, LinuxHelper }
