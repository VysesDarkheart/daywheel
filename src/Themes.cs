using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Daywheel
{
    /// <summary>
    /// One look for the wheel: nine pictures, and how strongly it glows.
    /// The colours are painted into the pictures, so nothing here tints them.
    /// </summary>
    internal sealed class Theme
    {
        internal readonly string Name;
        internal readonly string Folder;
        /// <summary>How strongly this look glows, before the player's own setting.</summary>
        internal readonly float Glow;

        internal Sprite HaloDay;
        internal Sprite HaloNight;
        internal Sprite Ring;
        internal Sprite MoonGlow;
        internal Sprite Fire;
        internal Sprite SunGlow;
        internal Sprite Sun;
        internal Sprite Moon;
        internal Sprite Mark;

        /// <summary>Read once, whether that worked or not, so a bad look is not retried every frame.</summary>
        internal bool Tried;
        internal bool Ready;
        /// <summary>Its five soft pictures were repacked for the game's colour space. See Themes.Soften.</summary>
        internal bool Softened;

        internal Theme(string name, string folder, float glow)
        {
            Name = name;
            Folder = folder;
            Glow = glow;
        }
    }

    /// <summary>
    /// Every look the wheel can wear.
    ///
    /// The pictures are built into the DLL itself, so there is nothing
    /// loose on disk to go missing or be changed underneath it. A look is
    /// read the first time it is worn and kept after that.
    /// </summary>
    internal static class Themes
    {
        internal static readonly Theme[] All =
        {
            new Theme("Ember and moonstone", "ember-and-moonstone", 0.82f),
            new Theme("Northern twilight", "northern-twilight", 0.68f),
            new Theme("Rootbound forest", "rootbound-forest", 0.56f),
            new Theme("Dvergr lantern", "dvergr-lantern", 0.75f),
            new Theme("The wolf chase", "the-wolf-chase", 0.33f),
            new Theme("Mountain relic", "mountain-relic", 0.43f),
            new Theme("Ashlands", "ashlands", 1.06f),
        };

        /// <summary>
        /// The looks were drawn in a browser, which mixes
        /// see-through colours as sRGB numbers. A game in Unity's linear
        /// colour space mixes them as light, and a faint glow comes out
        /// several times stronger: a haze, with its flicker washed out.
        /// Raising each soft pixel's opacity to this power, in the picture
        /// and in its fade, gives the browser's result over a dark
        /// background.
        /// </summary>
        internal const float Soften = 2.2f;

        private static Dictionary<string, string> _names;
        private static bool? _linear;
        private static bool _told;

        internal static string[] Names()
        {
            string[] names = new string[All.Length];
            for (int i = 0; i < All.Length; i++) names[i] = All[i].Name;
            return names;
        }

        /// <summary>The look with this name, or the first one if there is none.</summary>
        internal static Theme Find(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                string want = name.Trim();
                for (int i = 0; i < All.Length; i++)
                {
                    if (string.Equals(All[i].Name, want, StringComparison.OrdinalIgnoreCase))
                        return All[i];
                }
            }
            return All[0];
        }

        internal static int IndexOf(Theme t)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == t) return i;
            }
            return 0;
        }

        /// <summary>
        /// Read a look's pictures, once. Returns false if any of the nine is
        /// missing or unreadable, because a wheel with a piece gone is worse
        /// than another look.
        /// </summary>
        internal static bool Load(Theme t)
        {
            if (t == null) return false;
            if (t.Tried) return t.Ready;
            t.Tried = true;

            // If repacking the soft pictures fails, the look is read again
            // just as it was drawn, rather than lost.
            bool soften = Linear() && CanSoften();
            t.Ready = Read(t, soften);
            if (!t.Ready && soften)
            {
                Drop(t);
                soften = false;
                t.Ready = Read(t, false);
            }
            t.Softened = t.Ready && soften;
            if (t.Ready) Tell(t.Softened);

            if (!t.Ready && Main.Log != null)
                Main.Log.Warn("Daywheel: the " + t.Name + " look is missing a "
                    + "picture and was skipped.");
            return t.Ready;
        }

        /// <summary>All nine pictures. True if every one came through.</summary>
        private static bool Read(Theme t, bool soften)
        {
            t.HaloDay = Picture(t.Folder, "halo-day", soften);
            t.HaloNight = Picture(t.Folder, "halo-night", soften);
            t.Ring = Picture(t.Folder, "ring", false);
            t.MoonGlow = Picture(t.Folder, "glow-moon", soften);
            t.Fire = Picture(t.Folder, "glow-fire", soften);
            t.SunGlow = Picture(t.Folder, "glow-sun", soften);
            t.Sun = Picture(t.Folder, "sun", false);
            t.Moon = Picture(t.Folder, "moon", false);
            t.Mark = Picture(t.Folder, "marker", false);

            return t.HaloDay != null && t.HaloNight != null && t.Ring != null
                && t.MoonGlow != null && t.Fire != null && t.SunGlow != null
                && t.Sun != null && t.Moon != null && t.Mark != null;
        }

        /// <summary>Let go of a half-read look's pictures before it's read again.</summary>
        private static void Drop(Theme t)
        {
            Sprite[] all =
            {
                t.HaloDay, t.HaloNight, t.Ring, t.MoonGlow, t.Fire, t.SunGlow,
                t.Sun, t.Moon, t.Mark,
            };
            for (int i = 0; i < all.Length; i++)
            {
                Sprite s = all[i];
                if (s == null) continue;
                if (s.texture != null) UnityEngine.Object.Destroy(s.texture);
                UnityEngine.Object.Destroy(s);
            }
        }

        /// <summary>Does the game mix colours as light? Asked once.</summary>
        internal static bool Linear()
        {
            if (_linear.HasValue) return _linear.Value;
            bool linear = false;
            try { linear = QualitySettings.activeColorSpace == ColorSpace.Linear; }
            catch (Exception) { }
            _linear = linear;
            return linear;
        }

        /// <summary>
        /// Say once, in the log, how the glow ended up being drawn, so a
        /// report of how it looks can be read against it.
        /// </summary>
        private static void Tell(bool softened)
        {
            if (_told || Main.Log == null) return;
            _told = true;
            if (!Linear())
                Main.Log.Info("Daywheel: the game mixes colours in gamma space, "
                    + "so the glow is drawn as it is.");
            else if (softened)
                Main.Log.Info("Daywheel: the game mixes colours in linear space, "
                    + "so the glow is adjusted to look as it was drawn.");
            else
                Main.Log.Warn("Daywheel: the game mixes colours in linear space, "
                    + "but the glow couldn't be adjusted, so it may look stronger "
                    + "than it was drawn.");
        }

        /// <summary>The repacked pictures need half floats, which nearly every card has.</summary>
        private static bool CanSoften()
        {
            try { return SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// One picture out of the DLL, as a sprite.
        ///
        /// Loaded just as it was drawn: sRGB, straight alpha, smooth
        /// scaling, clamped at the edges. No mipmaps: every clear pixel has
        /// black stored behind it, and a smaller copy made from the picture
        /// would pull that black into every soft edge.
        ///
        /// With `soften`, it's repacked for a game that mixes colours as
        /// light; see Repack.
        /// </summary>
        private static Sprite Picture(string folder, string layer, bool soften)
        {
            string real;
            if (!Resources().TryGetValue("themes/" + folder + "/" + layer + ".png", out real))
                return null;

            byte[] bytes;
            using (Stream s = typeof(Themes).Assembly.GetManifestResourceStream(real))
            {
                if (s == null) return null;
                using (MemoryStream m = new MemoryStream())
                {
                    s.CopyTo(m);
                    bytes = m.ToArray();
                }
            }

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            // Kept readable only when its pixels are about to be repacked.
            if (!tex.LoadImage(bytes, !soften))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            if (soften)
            {
                Texture2D packed = Repack(tex);
                UnityEngine.Object.Destroy(tex);
                if (packed == null) return null;
                tex = packed;
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "Daywheel " + folder + " " + layer;

            // A whole rectangle rather than a mesh cut to the shape: cutting
            // one needs the pixels read back, and these are not kept readable.
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = tex.name;
            return sprite;
        }

        /// <summary>
        /// A soft picture remade for a game that mixes colours as light:
        /// colours turned to linear values, and each pixel's opacity raised
        /// to the Soften power. Stored as half floats, because in 8 bits the
        /// faint end collapses: a halo's twenty levels of opacity become two,
        /// a hard-edged disc. Null if it can't be done.
        /// </summary>
        private static Texture2D Repack(Texture2D drawn)
        {
            Texture2D tex = null;
            try
            {
                Color32[] px = drawn.GetPixels32();
                Color[] made = new Color[px.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 p = px[i];
                    made[i] = new Color(
                        Mathf.GammaToLinearSpace(p.r / 255f),
                        Mathf.GammaToLinearSpace(p.g / 255f),
                        Mathf.GammaToLinearSpace(p.b / 255f),
                        Mathf.Pow(p.a / 255f, Soften));
                }
                tex = new Texture2D(drawn.width, drawn.height,
                    TextureFormat.RGBAHalf, false, true);
                tex.SetPixels(made);
                tex.Apply(false, true);
                return tex;
            }
            catch (Exception)
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
                return null;
            }
        }

        /// <summary>
        /// The pictures inside the DLL, by a name with forward slashes.
        /// The build names each one after its folder, and a folder is
        /// written with whichever slash the machine that built it uses.
        /// </summary>
        private static Dictionary<string, string> Resources()
        {
            if (_names != null) return _names;
            _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string real in typeof(Themes).Assembly.GetManifestResourceNames())
                _names[real.Replace('\\', '/')] = real;
            return _names;
        }
    }
}
