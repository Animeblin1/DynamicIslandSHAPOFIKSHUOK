using Microsoft.Win32;

namespace DynamicIsland;

/// <summary>The switches of the menu, remembered in the registry between runs. All of them start on.</summary>
static class Settings
{
    const string Key = @"Software\DynamicIsland";

    static bool _lyrics = Read(nameof(Lyrics)), _network = Read(nameof(Network)), _hideFullscreen = Read(nameof(HideFullscreen));

    /// <summary>Look the lyrics of the track up and show them. Off: nothing is sent to LRCLIB.</summary>
    public static bool Lyrics
    {
        get => _lyrics;
        set => Write(nameof(Lyrics), _lyrics = value);
    }

    /// <summary>Notices about Wi-Fi, Ethernet and VPN.</summary>
    public static bool Network
    {
        get => _network;
        set => Write(nameof(Network), _network = value);
    }

    /// <summary>Slide away while a game or a video covers the whole screen.</summary>
    public static bool HideFullscreen
    {
        get => _hideFullscreen;
        set => Write(nameof(HideFullscreen), _hideFullscreen = value);
    }

    static bool Read(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);
            return key?.GetValue(name) is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }

    static void Write(string name, bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(Key);
            key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            // not saved: the switch still holds until the island is closed
            App.Log(ex);
        }
    }
}
