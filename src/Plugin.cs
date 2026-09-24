using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Daywheel
{
    /// <summary>
    /// Shows how much daylight is left, as a turning ring.
    ///
    /// It patches no game code, adds nothing to the network, and writes
    /// nothing but its own config file.
    /// </summary>
    [BepInPlugin(Guid, "Daywheel", Ver)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.vysesdarkheart.daywheel";
        public const string Ver = "1.0.9";

        internal static ConfigEntry<bool> Show;
        internal static ConfigEntry<bool> ShowDay;
        internal static ConfigEntry<float> Size;
        internal static ConfigEntry<float> X;
        internal static ConfigEntry<float> Y;
        internal static ConfigEntry<string> MoveKey;
        internal static ConfigEntry<string> ThemeName;
        internal static ConfigEntry<float> Glow;

        internal static ManualLog Log;
        private static ConfigFile _file;

        /// <summary>
        /// BepInEx writes the config file every time a setting changes, and
        /// dragging the wheel changes two settings every frame. So saving is
        /// paused while the wheel is being moved, and done once at the end.
        /// </summary>
        internal static void Saving(bool on)
        {
            if (_file == null || _file.SaveOnConfigSet == on) return;
            _file.SaveOnConfigSet = on;
            if (on) _file.Save();
        }

        private void Awake()
        {
            _file = Config;
            Show = Config.Bind("Wheel", "Show", true,
                "Turns the wheel on or off.");
            ShowDay = Config.Bind("Wheel", "ShowDayCount", true,
                "Shows the day number under the wheel.");
            Size = Config.Bind("Wheel", "Size", 62f,
                "Width, in the game's interface units, so it follows the "
                + "game's UI scale.");
            X = Config.Bind("Wheel", "X", -30f,
                "Distance from the right edge. A bigger negative number moves "
                + "it left.");
            Y = Config.Bind("Wheel", "Y", -250f,
                "Distance from the top edge. A bigger negative number moves it "
                + "down. The minimap's size changes with screen resolution, so "
                + "change this one if the two overlap.");
            MoveKey = Config.Bind("Wheel", "MoveKey", "F8",
                "Press this to move the wheel. Your character stands still "
                + "while you hold the left mouse button and move the mouse to "
                + "drag it, use Page Up and Page Down to resize it, and "
                + "right-click for the next look. Press it again when you're "
                + "done. Any Unity key name, like F8 or Insert. A name Unity "
                + "doesn't know means F8, and the log says so.");
            ThemeName = Config.Bind("Look", "Theme", Themes.All[0].Name,
                new ConfigDescription(
                    "Which look to use, spelled exactly as listed. While "
                    + "you're moving the wheel, right-click or press End for "
                    + "the next one, and Home for the one before.",
                    new AcceptableValueList<string>(Themes.Names())));
            Glow = Config.Bind("Look", "Glow", 1f,
                new ConfigDescription(
                    "Glow strength. 1 is normal, 0 turns it off.",
                    new AcceptableValueRange<float>(0f, 2f)));

            Log = new ManualLog(Logger);
            Log.Info("Daywheel " + Ver + " loaded.");

            Guard.Run("start", delegate
            {
                GameObject go = new GameObject("Daywheel");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<Wheel>();
            });
        }
    }

    /// <summary>Wraps the logger, so the rest of the code logs without BepInEx's logging types.</summary>
    internal sealed class ManualLog
    {
        private readonly BepInEx.Logging.ManualLogSource _to;

        internal ManualLog(BepInEx.Logging.ManualLogSource to) { _to = to; }

        internal void Info(string msg)
        {
            if (_to != null) _to.LogInfo(msg);
        }

        internal void Warn(string msg)
        {
            if (_to != null) _to.LogWarning(msg);
        }
    }
}
