using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Daywheel
{
    /// <summary>
    /// A ring that's light down one half and dark down the other, turning
    /// with the day. The mark at the top stays still, so the gap between the
    /// mark and the dark half is the daylight you have left.
    ///
    /// Nine pictures, stacked: two halos at the back, the ring with its
    /// moonglow and fire turning together on top, the sun and moon riding
    /// round inside it while staying upright, and the mark in front. Which
    /// pictures depends on the look chosen; see Themes.
    /// </summary>
    internal class Wheel : MonoBehaviour
    {
        /// <summary>The game's own day boundaries, as a fraction of the day.</summary>
        private const float Dawn = 0.25f;
        private const float Dusk = 0.75f;
        /// <summary>How long the light takes to rise or fall, each side of a boundary.</summary>
        private const float Twilight = 0.04f;
        /// <summary>How far out the sun and moon ride, as a share of the width.</summary>
        private const float Orbit = 0.164f;
        private const double Tau = Math.PI * 2.0;
        private const string DimHex = "#8B8477";
        /// <summary>
        /// A key can reach us through two inputs, sometimes a frame apart.
        /// A second press of the same key inside this time is the same press.
        /// </summary>
        private const float SamePress = 0.25f;

        private RectTransform _root;
        private RectTransform _ringRt;
        private RectTransform _moonGlowRt;
        private RectTransform _fireRt;
        private RectTransform _sunGlowRt;
        private RectTransform _sunRt;
        private RectTransform _moonRt;
        private RectTransform _markRt;
        private Image _haloDay;
        private Image _haloNight;
        private Image _ring;
        private Image _moonGlow;
        private Image _fire;
        private Image _sunGlow;
        private Image _sun;
        private Image _moon;
        private Image _mark;
        private RectTransform _under;
        private TMP_Text _label;
        private TMP_Text _hint;
        private Theme _theme;
        /// <summary>The setting as it read when the look was last put on.</summary>
        private string _worn;
        private bool _shown;
        private int _lastDay = -1;
        /// <summary>Every piece and its width as a share of the whole.</summary>
        private readonly List<KeyValuePair<RectTransform, float>> _pieces =
            new List<KeyValuePair<RectTransform, float>>();
        private float _across;

        private void Update()
        {
            Guard.Run("wheel", Tick);
        }

        private void OnDestroy()
        {
            Guard.Run("wheel cleanup", Clear);
        }

        private void Tick()
        {
            bool wanted = Plugin.Show == null || Plugin.Show.Value;

            if (!wanted || Player.m_localPlayer == null || Hud.instance == null
                || Hidden())
            {
                End();
                Show(false);
                return;
            }

            EnvMan env = EnvMan.instance;
            if (env == null) { End(); Show(false); return; }

            if (_root == null && !Build()) return;

            // A new look takes hold at once, whether it came from the
            // setting or from a right-click while moving.
            string asked = Plugin.ThemeName == null ? null : Plugin.ThemeName.Value;
            if (_theme == null || !string.Equals(asked, _worn, StringComparison.Ordinal))
            {
                if (!Wear(Themes.Find(asked))) { End(); Show(false); return; }
                _worn = asked;
            }
            Show(true);

            Place();

            // Size and place are read every frame, so changing either one in
            // the config takes hold without leaving the game.
            float want = Mathf.Clamp(
                Plugin.Size == null ? 62f : Plugin.Size.Value, 24f, 400f);
            if (Mathf.Abs(want - _across) > 0.01f) Fit(want);
            Vector2 spot = new Vector2(
                Plugin.X == null ? -30f : Plugin.X.Value,
                Plugin.Y == null ? -250f : Plugin.Y.Value);
            if (_root.anchoredPosition != spot) _root.anchoredPosition = spot;

            float now = 0f;
            try { now = env.GetDayFraction(); }
            catch (Exception) { }

            // Turning by the fraction brings this moment round to the mark.
            // The moonglow and the fire turn with the ring, so the fire stays
            // on the light half and the glow on the dark one.
            Vector3 spin = new Vector3(0f, 0f, now * 360f);
            _ringRt.localEulerAngles = spin;
            _moonGlowRt.localEulerAngles = spin;
            _fireRt.localEulerAngles = spin;

            // The sun rides over the middle of the light half and the moon
            // over the middle of the dark one. They aren't children of the
            // ring, or they'd be upside down for half the day.
            float track = _across * Orbit;
            Ride(_sunRt, 0.5f - now, track);
            Ride(_sunGlowRt, 0.5f - now, track);
            Ride(_moonRt, 0f - now, track);

            // Real time, in double precision. The game's own clock stops
            // while single player is paused, and after a few hours a float
            // has too few digits left to carry the flicker smoothly.
            double t = Time.unscaledTimeAsDouble;
            float lit = Daylight(now);
            float glow = _theme.Glow
                * (Plugin.Glow == null ? 1f : Mathf.Max(0f, Plugin.Glow.Value));

            // Three rates that never line up, so the fire flickers instead
            // of pulsing like a heartbeat.
            float fire = 0.58f
                + 0.14f * (float)Math.Sin(Tau * 2.1 * t)
                + 0.08f * (float)Math.Sin(Tau * 3.9 * t + 0.7)
                + 0.045f * (float)Math.Sin(Tau * 7.3 * t + 1.4);
            float breath = 0.80f + 0.20f * (float)Math.Sin(t * 0.8);
            float moonBreath = 0.84f + 0.16f * (float)Math.Sin(t * 0.63 + 1.0);

            // The rays turn slowly clockwise. The ring moves about a third
            // of a degree a second, far too slow to see, so this is what
            // shows the wheel is alive.
            _sunRt.localEulerAngles = new Vector3(0f, 0f, -(float)((t * 7.0) % 360.0));

            // A look repacked for the game's colours (see Themes.Soften) has
            // its soft layers' fades raised to the same power as its pictures.
            bool soft = _theme.Softened;
            Fade(_haloDay, Soft(lit * breath * 0.7f * glow, soft));
            Fade(_haloNight, Soft((1f - lit) * breath * 0.7f * glow, soft));
            Fade(_moonGlow, Soft(moonBreath * (0.48f + 0.52f * (1f - lit)) * glow, soft));
            Fade(_fire, Soft((0.4f + 0.6f * lit) * fire * 1.7f * glow, soft));
            Fade(_sunGlow, Soft((0.22f + 0.78f * lit) * fire * glow, soft));
            Fade(_sun, 0.64f + 0.36f * lit);
            Fade(_moon, 0.76f + 0.24f * (1f - lit));

            if (_label == null) return;
            bool wantDay = Plugin.ShowDay == null || Plugin.ShowDay.Value;
            if (!wantDay)
            {
                if (_label.text.Length > 0) _label.text = string.Empty;
                _lastDay = -1;
                return;
            }

            int day = 0;
            try { day = env.GetDay(); }
            catch (Exception) { }
            if (day == _lastDay) return;
            _lastDay = day;
            _label.text = "<color=" + DimHex + ">Day "
                + day.ToString(CultureInfo.InvariantCulture) + "</color>";
        }

        /// <summary>
        /// Put a look on the wheel. If it can't be read, the first look that
        /// can is used instead, and false comes back only if none can.
        /// </summary>
        private bool Wear(Theme want)
        {
            if (!Themes.Load(want))
            {
                want = null;
                for (int i = 0; i < Themes.All.Length && want == null; i++)
                {
                    if (Themes.Load(Themes.All[i])) want = Themes.All[i];
                }
                if (want == null) return false;
            }

            _haloDay.sprite = want.HaloDay;
            _haloNight.sprite = want.HaloNight;
            _ring.sprite = want.Ring;
            _moonGlow.sprite = want.MoonGlow;
            _fire.sprite = want.Fire;
            _sunGlow.sprite = want.SunGlow;
            _sun.sprite = want.Sun;
            _moon.sprite = want.Moon;
            _mark.sprite = want.Mark;
            _theme = want;
            return true;
        }

        /// <summary>Set how strongly a picture shows. Its colours are its own.</summary>
        private static void Fade(Image img, float a)
        {
            img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        /// <summary>A soft layer's fade, raised to Themes.Soften when its picture was.</summary>
        private static float Soft(float a, bool softened)
        {
            a = Mathf.Clamp01(a);
            return softened ? Mathf.Pow(a, Themes.Soften) : a;
        }

        // ------------------------------------------------------------- move
        //
        // Press the move key and the wheel can be moved during normal play,
        // with the minimap in view. The game's own screens are no use for
        // this: the bag's crafting panel and the big map both cover the
        // corner the wheel lives in.
        //
        // During play the game turns the mouse into camera movement and the
        // buttons into attacks and walking. That's all done by the player's
        // PlayerController, so while the wheel is being moved that one
        // component is switched off. The character stands still until you
        // finish.
        //
        // Dragging goes by how far the mouse moves, not by where the pointer
        // is. During play the pointer can stay pinned to the middle of the
        // screen whatever the cursor settings say, so aiming at the wheel
        // can't be relied on. Mouse movement always arrives, because it's
        // what turns the camera.

        private bool _moving;
        private bool _held;
        private bool _over;
        /// <summary>The player's controls, switched off while moving.</summary>
        private PlayerController _still;
        private readonly Dictionary<int, float> _lastPress = new Dictionary<int, float>();

        /// <summary>
        /// The game hands out mouse movement scaled down to a twentieth, so
        /// it's scaled back up to get screen pixels.
        /// </summary>
        private const float DeltaToPixels = 20f;

        private void Place()
        {
            if (Pressed(MoveKey()))
            {
                if (_moving) End();
                else Begin();
            }
            if (!_moving) return;

            // The bag, the map, a trader or the game menu opening means
            // you've moved on.
            if (GameScreenOpen())
            {
                End();
                return;
            }

            if (Pressed(KeyCode.End)) Turn(1);
            if (Pressed(KeyCode.Home)) Turn(-1);

            float step = Time.unscaledDeltaTime * 260f;
            if (Held(KeyCode.LeftShift) || Held(KeyCode.RightShift)) step *= 3f;
            if (Held(KeyCode.LeftControl) || Held(KeyCode.RightControl)) step *= 0.22f;

            float dx = 0f;
            float dy = 0f;
            if (Held(KeyCode.LeftArrow)) dx -= step;
            if (Held(KeyCode.RightArrow)) dx += step;
            if (Held(KeyCode.UpArrow)) dy += step;
            if (Held(KeyCode.DownArrow)) dy -= step;
            if (dx != 0f && Plugin.X != null)
                Plugin.X.Value = Mathf.Min(0f, Plugin.X.Value + dx);
            if (dy != 0f && Plugin.Y != null)
                Plugin.Y.Value = Mathf.Min(0f, Plugin.Y.Value + dy);

            float grow = 0f;
            if (Held(KeyCode.PageUp) || Held(KeyCode.Equals) || Held(KeyCode.KeypadPlus))
                grow += step * 0.35f;
            if (Held(KeyCode.PageDown) || Held(KeyCode.Minus) || Held(KeyCode.KeypadMinus))
                grow -= step * 0.35f;
            if (grow != 0f && Plugin.Size != null)
                Plugin.Size.Value = Mathf.Clamp(Plugin.Size.Value + grow, 24f, 400f);

            Drag();
        }

        /// <summary>Start moving: stop the character.</summary>
        private void Begin()
        {
            Player me = Player.m_localPlayer;
            if (me == null || GameScreenOpen()) return;

            // Zero the controls first, or the character carries on with
            // whatever it was last told, running or swinging.
            try
            {
                me.SetControls(Vector3.zero, false, false, false, false, false,
                    false, false, false, false, false);
            }
            catch (Exception) { }

            _still = null;
            try
            {
                PlayerController pc = me.GetComponent<PlayerController>();
                if (pc != null && pc.enabled)
                {
                    pc.enabled = false;
                    _still = pc;
                }
            }
            catch (Exception) { }

            _moving = true;
            _held = false;
            Plugin.Saving(false);
            Shout();

            if (_still == null && Plugin.Log != null)
                Plugin.Log.Warn("Daywheel: couldn't pause your controls, so your "
                    + "character may move while you move the wheel.");
        }

        /// <summary>Stop moving and give the controls back.</summary>
        private void End()
        {
            if (!_moving) return;
            _moving = false;
            _held = false;
            if (_over) Hover(false);

            try { if (_still != null) _still.enabled = true; }
            catch (Exception) { }
            _still = null;

            Plugin.Saving(true);
            Shout();

            if (Plugin.Log != null)
                Plugin.Log.Info("Daywheel: the wheel is now at X " + Num(Plugin.X)
                    + ", Y " + Num(Plugin.Y) + ", size " + Num(Plugin.Size) + ".");
        }

        /// <summary>
        /// Hold the left button and move the mouse to drag the wheel, from
        /// anywhere on screen. A right-click anywhere is the next look.
        /// </summary>
        private void Drag()
        {
            if (Clicked(1)) Turn(1);
            if (Clicked(0)) _held = true;
            if (_held && !Down(0)) _held = false;
            if (_held != _over) Hover(_held);
            if (!_held) return;

            Vector2 px = Moved();
            if (px.x == 0f && px.y == 0f) return;

            // Screen pixels into the units the wheel is placed in. The hud's
            // scale on screen includes the game's own interface scaling.
            float scale = _under == null ? 1f : _under.lossyScale.x;
            if (scale < 0.01f) scale = 1f;
            if (Plugin.X != null)
                Plugin.X.Value = Mathf.Min(0f, Plugin.X.Value + px.x / scale);
            if (Plugin.Y != null)
                Plugin.Y.Value = Mathf.Min(0f, Plugin.Y.Value + px.y / scale);
        }

        /// <summary>This frame's mouse movement, in screen pixels.</summary>
        private static Vector2 Moved()
        {
            try { return ZInput.GetMouseDelta() * DeltaToPixels; }
            catch (Exception) { return Vector2.zero; }
        }

        private static string Num(BepInEx.Configuration.ConfigEntry<float> e)
        {
            return e == null ? "?" : e.Value.ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>Step to the next look, or back one. The setting keeps it.</summary>
        private void Turn(int by)
        {
            if (Plugin.ThemeName == null) return;
            int n = Themes.All.Length;
            int at = (Themes.IndexOf(_theme) + by + n) % n;
            Plugin.ThemeName.Value = Themes.All[at].Name;
            if (Plugin.Log != null)
                Plugin.Log.Info("Daywheel: look is now " + Themes.All[at].Name + ".");
        }

        /// <summary>Lift the mark while the wheel is held, so you can see it's picked up.</summary>
        private void Hover(bool on)
        {
            _over = on;
            if (_markRt != null)
                _markRt.localScale = on ? new Vector3(1.1f, 1.1f, 1f) : Vector3.one;
        }

        /// <summary>What to do, shown only while moving.</summary>
        private void Shout()
        {
            if (_hint == null) return;
            _hint.gameObject.SetActive(_moving);
            if (!_moving) return;
            _hint.text = "<color=" + DimHex + "><size=90%>hold left mouse and move to drag  ·  "
                + "arrows nudge  ·  Page Up/Down resize  ·  right-click for the next look  ·  "
                + MoveKey() + " when done</size></color>";
        }

        /// <summary>Is one of the game's own screens open?</summary>
        private static bool GameScreenOpen()
        {
            try
            {
                return InventoryGui.IsVisible() || StoreGui.IsVisible()
                    || Menu.IsVisible() || Minimap.IsOpen();
            }
            catch (Exception) { return false; }
        }

        // ------------------------------------------------------------ input
        //
        // Every key and click is asked of both Unity's own input and the
        // game's, so it gets through whichever one sees it.

        /// <summary>A key pressed this frame.</summary>
        private bool Pressed(KeyCode k)
        {
            bool unity = false;
            bool game = false;
            try { unity = Input.GetKeyDown(k); }
            catch (Exception) { }
            try { game = ZInput.GetKeyDown(k, false); }
            catch (Exception) { }
            return (unity || game) && Fresh((int)k);
        }

        /// <summary>A key being held.</summary>
        private static bool Held(KeyCode k)
        {
            try { if (Input.GetKey(k)) return true; }
            catch (Exception) { }
            try { return ZInput.GetKey(k, false); }
            catch (Exception) { return false; }
        }

        /// <summary>A mouse button pressed this frame.</summary>
        private bool Clicked(int button)
        {
            bool hit = false;
            try { hit = ZInput.GetMouseButtonDown(button); }
            catch (Exception) { }
            if (!hit)
            {
                try { hit = Input.GetMouseButtonDown(button); }
                catch (Exception) { }
            }
            return hit && Fresh(-1 - button);
        }

        /// <summary>A mouse button being held.</summary>
        private static bool Down(int button)
        {
            try { if (ZInput.GetMouseButton(button)) return true; }
            catch (Exception) { }
            try { return Input.GetMouseButton(button); }
            catch (Exception) { return false; }
        }

        /// <summary>False for a second report of a press already acted on.</summary>
        private bool Fresh(int key)
        {
            float now = Time.unscaledTime;
            float last;
            if (_lastPress.TryGetValue(key, out last) && now - last < SamePress) return false;
            _lastPress[key] = now;
            return true;
        }

        /// <summary>The last MoveKey setting that wasn't a key, so it's only reported once.</summary>
        private static string _badKey;

        private static KeyCode MoveKey()
        {
            if (Plugin.MoveKey == null) return KeyCode.F8;
            string name = Plugin.MoveKey.Value;
            KeyCode k;
            if (Enum.TryParse(name, true, out k)) return k;
            if (!string.Equals(name, _badKey, StringComparison.Ordinal))
            {
                _badKey = name;
                if (Plugin.Log != null)
                    Plugin.Log.Warn("Daywheel: MoveKey \"" + name + "\" isn't a key "
                        + "name Unity knows, so F8 moves the wheel.");
            }
            return KeyCode.F8;
        }

        // ------------------------------------------------------------ shape

        /// <summary>
        /// Put a piece at a fraction round the middle, measured clockwise
        /// from the top, without turning the piece itself.
        /// </summary>
        private static void Ride(RectTransform rt, float part, float track)
        {
            if (rt == null) return;
            float turn = part * Mathf.PI * 2f;
            rt.anchoredPosition = new Vector2(
                Mathf.Sin(turn) * track, Mathf.Cos(turn) * track);
        }

        /// <summary>
        /// One by day, nought by night, easing across dawn and dusk. The
        /// pictures were drawn to this same curve.
        /// </summary>
        private static float Daylight(float now)
        {
            float toDawn = Gap(now, Dawn);
            if (Mathf.Abs(toDawn) < Twilight)
                return Mathf.SmoothStep(0f, 1f, 0.5f + toDawn / (Twilight * 2f));
            float toDusk = Gap(now, Dusk);
            if (Mathf.Abs(toDusk) < Twilight)
                return Mathf.SmoothStep(0f, 1f, 0.5f - toDusk / (Twilight * 2f));
            return (now > Dawn && now < Dusk) ? 1f : 0f;
        }

        /// <summary>Signed distance round a circle, the short way.</summary>
        private static float Gap(float a, float b)
        {
            float d = a - b;
            if (d > 0.5f) d -= 1f;
            if (d < -0.5f) d += 1f;
            return d;
        }

        private static bool Hidden()
        {
            try { return Hud.IsUserHidden(); }
            catch (Exception) { return false; }
        }

        // ------------------------------------------------------------ build

        /// <summary>
        /// Returns false while the hud isn't up yet, which it isn't the
        /// moment a world loads. The order here is the drawing order. The
        /// pictures go on afterwards, from whichever look is chosen.
        /// </summary>
        private bool Build()
        {
            MessageHud hud = MessageHud.instance;
            if (hud == null || hud.m_messageText == null) return false;
            GameObject under = Hud.instance.m_rootObject;
            if (under == null) return false;

            float across = Plugin.Size == null ? 62f : Plugin.Size.Value;
            float x = Plugin.X == null ? -30f : Plugin.X.Value;
            float y = Plugin.Y == null ? -250f : Plugin.Y.Value;

            _under = under.transform as RectTransform;
            if (_under == null) return false;

            // The hud goes when the world unloads, and the last wheel went
            // with it. Nothing of that one carries over: the new pictures
            // need a look put on them and the day count needs writing.
            _pieces.Clear();
            _theme = null;
            _worn = null;
            _lastDay = -1;
            _held = false;
            _over = false;

            GameObject go = new GameObject("Daywheel", typeof(RectTransform));
            go.transform.SetParent(under.transform, false);
            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(1f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 1f);
            _root.anchoredPosition = new Vector2(x, y);
            _root.sizeDelta = new Vector2(across, across);

            // Widths as a share of the whole wheel. The ring and everything
            // that turns with it fill it. The sun, its glow and the moon are
            // the sizes they were drawn to sit at.
            _haloDay = Add("HaloDay", 1f).Item2;
            _haloNight = Add("HaloNight", 1f).Item2;

            Tuple<RectTransform, Image> ring = Add("Ring", 1f);
            _ringRt = ring.Item1;
            _ring = ring.Item2;

            Tuple<RectTransform, Image> moonGlow = Add("Moonglow", 1f);
            _moonGlowRt = moonGlow.Item1;
            _moonGlow = moonGlow.Item2;

            Tuple<RectTransform, Image> fire = Add("Fire", 1f);
            _fireRt = fire.Item1;
            _fire = fire.Item2;

            Tuple<RectTransform, Image> sunGlow = Add("SunGlow", 0.47f);
            _sunGlowRt = sunGlow.Item1;
            _sunGlow = sunGlow.Item2;

            Tuple<RectTransform, Image> sun = Add("Sun", 0.258f);
            _sunRt = sun.Item1;
            _sun = sun.Item2;

            Tuple<RectTransform, Image> moon = Add("Moon", 0.222f);
            _moonRt = moon.Item1;
            _moon = moon.Item2;

            // The mark, which doesn't move. Its picture covers the whole
            // wheel and already has the mark at the top.
            Tuple<RectTransform, Image> mark = Add("Now", 1f);
            _markRt = mark.Item1;
            _mark = mark.Item2;

            // The day count, which doesn't move either.
            GameObject text = new GameObject("Day", typeof(RectTransform));
            // Kept switched off until it has its font. Text looks for a
            // default font the moment it wakes, the game doesn't ship one,
            // and it complains to the log.
            text.SetActive(false);
            text.transform.SetParent(_root, false);
            RectTransform textRt = text.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0f);
            textRt.anchorMax = new Vector2(0.5f, 0f);
            textRt.pivot = new Vector2(0.5f, 1f);
            textRt.anchoredPosition = new Vector2(0f, -2f);
            textRt.sizeDelta = new Vector2(120f, 20f);
            TextMeshProUGUI t = text.AddComponent<TextMeshProUGUI>();
            t.font = hud.m_messageText.font;
            t.fontSharedMaterial = hud.m_messageText.fontSharedMaterial;
            t.fontSize = Mathf.Max(12f, hud.m_messageText.fontSize * 0.62f);
            t.alignment = TextAlignmentOptions.Top;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.richText = true;
            text.SetActive(true);
            _label = t;

            // What to do, shown only while moving.
            GameObject tip = new GameObject("Hint", typeof(RectTransform));
            tip.SetActive(false);
            tip.transform.SetParent(_root, false);
            RectTransform tipRt = tip.GetComponent<RectTransform>();
            tipRt.anchorMin = new Vector2(1f, 0f);
            tipRt.anchorMax = new Vector2(1f, 0f);
            tipRt.pivot = new Vector2(1f, 1f);
            tipRt.anchoredPosition = new Vector2(0f, -24f);
            tipRt.sizeDelta = new Vector2(460f, 22f);
            TextMeshProUGUI tt = tip.AddComponent<TextMeshProUGUI>();
            tt.font = hud.m_messageText.font;
            tt.fontSharedMaterial = hud.m_messageText.fontSharedMaterial;
            tt.fontSize = 14f;
            tt.alignment = TextAlignmentOptions.TopRight;
            tt.textWrappingMode = TextWrappingModes.NoWrap;
            tt.raycastTarget = false;
            tt.richText = true;
            _hint = tt;

            Fit(across);
            _shown = true;
            Shout();
            return true;
        }

        /// <summary>
        /// Set every piece to its share of a new width. Each one was added
        /// with the share it keeps, so this is all a new size needs.
        /// </summary>
        private void Fit(float across)
        {
            _across = across;
            if (_root != null) _root.sizeDelta = new Vector2(across, across);
            for (int i = 0; i < _pieces.Count; i++)
            {
                RectTransform rt = _pieces[i].Key;
                if (rt == null) continue;
                float w = across * _pieces[i].Value;
                rt.sizeDelta = new Vector2(w, w);
            }
            if (_label != null)
                _label.fontSize = Mathf.Clamp(across * 0.20f, 9f, 28f);
        }

        /// <summary>
        /// One centred picture, added in drawing order and left empty until
        /// a look is put on. `share` is its width as a share of the whole,
        /// so it can be resized later without being rebuilt.
        /// </summary>
        private Tuple<RectTransform, Image> Add(string name, float share)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            _pieces.Add(new KeyValuePair<RectTransform, float>(rt, share));
            return new Tuple<RectTransform, Image>(rt, img);
        }

        private void Show(bool on)
        {
            if (_root == null || _shown == on) return;
            _shown = on;
            _root.gameObject.SetActive(on);
        }

        private void Clear()
        {
            End();
            if (_root == null) return;
            try { Destroy(_root.gameObject); }
            catch (Exception) { }
            _root = null;
            _ringRt = null;
            _moonGlowRt = null;
            _fireRt = null;
            _sunGlowRt = null;
            _sunRt = null;
            _moonRt = null;
            _markRt = null;
            _haloDay = null;
            _haloNight = null;
            _ring = null;
            _moonGlow = null;
            _fire = null;
            _sunGlow = null;
            _sun = null;
            _moon = null;
            _mark = null;
            _label = null;
            _hint = null;
            _theme = null;
            _worn = null;
            _under = null;
            _held = false;
            _over = false;
            _pieces.Clear();
        }
    }
}
