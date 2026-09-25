using UnityEngine;

[assembly: Keel.Main(typeof(Daywheel.Main))]

namespace Daywheel
{
    /// <summary>
    /// Shows how much daylight is left, as a turning ring.
    ///
    /// It patches no game code, adds nothing to the network, and writes
    /// nothing but its own settings file. Keel starts it on its own, or
    /// BepInEx does when BepInEx is installed; see Plugin.
    /// </summary>
    internal sealed class Main : Keel.Mod
    {
        public const string Guid = "com.vysesdarkheart.daywheel";
        public const string Title = "Daywheel";
        public const string Ver = "1.0.13";

        internal static Keel.Setting<bool> Show;
        internal static Keel.Setting<bool> ShowDay;
        internal static Keel.Setting<Color> DayColor;
        internal static Keel.Setting<float> Size;
        internal static Keel.Setting<float> X;
        internal static Keel.Setting<float> Y;
        internal static Keel.Setting<string> MoveKey;
        internal static Keel.Setting<string> ThemeName;
        internal static Keel.Setting<float> Glow;

        internal static Keel.Log Log;
        private static Keel.Host _host;

        internal override string Id { get { return Guid; } }
        internal override string Name { get { return Title; } }
        internal override string Version { get { return Ver; } }

        /// <summary>
        /// The settings file is written every time a setting changes, and
        /// dragging the wheel changes two settings every frame. So saving is
        /// paused while the wheel is being moved, and done once at the end.
        /// </summary>
        internal static void Saving(bool on)
        {
            if (_host != null) _host.Saving = on;
        }

        internal override void Start(Keel.Host host)
        {
            _host = host;
            Log = host.Log;
            Show = host.Bind("Wheel", "Show", true,
                "Turns the wheel on or off.");
            ShowDay = host.Bind("Wheel", "ShowDayCount", true,
                "Shows the day number under the wheel.");
            DayColor = host.Bind("Wheel", "DayColor", Wheel.DayDefault,
                "The day number's colour, as a hex code: FFFFFF is white, "
                + "FFD27A gold, 8B8477 the default. Two more digits on the "
                + "end set how solid it is, from 00 (invisible) to FF (solid).");
            Size = host.Bind("Wheel", "Size", 62f,
                "Width, in the game's interface units, so it follows the "
                + "game's UI scale.");
            X = host.Bind("Wheel", "X", -30f,
                "Distance from the right edge. A bigger negative number moves "
                + "it left.");
            Y = host.Bind("Wheel", "Y", -250f,
                "Distance from the top edge. A bigger negative number moves it "
                + "down. The minimap's size changes with screen resolution, so "
                + "change this one if the two overlap.");
            MoveKey = host.Bind("Wheel", "MoveKey", "F8",
                "Press this to move the wheel, and press it again when you're "
                + "done. Your character stands still in between, and a menu "
                + "next to the wheel lists the keys. Put away a hammer, hoe or "
                + "cultivator first. Any Unity key name, like F8 or Insert. A "
                + "name Unity doesn't know means F8, and the log says so.");
            ThemeName = host.Bind("Look", "Theme", Themes.All[0].Name,
                "Which look to use, spelled exactly as listed. While "
                + "you're moving the wheel, right-click or press End for "
                + "the next one, and Home for the one before.",
                Themes.Names());
            Glow = host.Bind("Look", "Glow", 1f,
                "Glow strength. 1 is normal, 0 turns it off.", 0f, 2f);

            Log.Info("Daywheel " + Ver + " loaded, started by " + host.Starter + ".");

            Guard.Run("start", delegate
            {
                GameObject go = new GameObject("Daywheel");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<Wheel>();
            });
        }
    }
}
