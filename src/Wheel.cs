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
        /// <summary>The day count's colour, unless the DayColor setting says otherwise.</summary>
        internal static readonly Color DayDefault = new Color32(0x8B, 0x84, 0x77, 0xFF);
        // Picking the day count's colour while moving: hold the middle button
        // and move the mouse. Across, the marker follows the mouse; up and
        // down it moves at half speed, because the field is short.
        private const float PickHeight = 40f;
        private const float PickGap = 6f;
        private const float PickAcross = 1f;
        private const float PickUp = 0.5f;
        /// <summary>
        /// A press this short, with the mouse moved less than TapTravel (in
        /// hud units), is a tap, which puts back the default colour.
        /// </summary>
        private const float TapSeconds = 0.35f;
        private const float TapTravel = 6f;
        // The help shown while moving: light text on a dark box beside the wheel.
        private const float HintSize = 16f;
        private const float HintPadX = 10f;
        private const float HintPadY = 7f;
        /// <summary>Space between the help box and the wheel.</summary>
        private const float HintGap = 6f;
        /// <summary>Space kept between the help box and the screen's edge.</summary>
        private const float HintEdge = 8f;
        private const string HintKeyHex = "#E6C27A";
        private static readonly Color HintText = new Color32(0xEC, 0xE7, 0xDA, 0xFF);
        /// <summary>
        /// How much of what's behind the help box it hides, as a browser would
        /// mix it. A game that mixes colours as light needs a bigger number
        /// for the same look; see HintBox.
        /// </summary>
        private const float HintShade = 0.85f;
        /// <summary>
        /// A key can arrive through two inputs, sometimes a frame apart.
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
        /// <summary>The canvas the hud is drawn on, to turn screen pixels into hud units.</summary>
        private Canvas _canvas;
        private TMP_Text _label;
        private TMP_Text _hint;
        private RectTransform _hintRt;
        private ColourField _field;
        private RectTransform _fieldRt;
        /// <summary>The marker on the colour field, and its middle, which shows the colour itself.</summary>
        private RectTransform _dotRt;
        private Image _dotFill;
        private Theme _theme;
        /// <summary>The setting as it read when the look was last put on.</summary>
        private string _worn;
        private bool _shown;
        private int _lastDay = -1;
        private Color _dayColor;
        /// <summary>How wide the day count's text is, measured when it's written.</summary>
        private float _dayWidth;
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
            bool wanted = Main.Show == null || Main.Show.Value;

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
            string asked = Main.ThemeName == null ? null : Main.ThemeName.Value;
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
                Main.Size == null ? 62f : Main.Size.Value, 24f, 400f);
            if (Mathf.Abs(want - _across) > 0.01f) Fit(want);
            // A bigger wheel reaches further left and down, so while it's
            // being moved the saved place is held on screen at its new size.
            if (_moving) Move(0f, 0f);
            // Shown on screen even if the setting points past its edge,
            // say after a change to a smaller resolution. The setting itself
            // is only changed by moving the wheel.
            Vector2 spot = Keep(new Vector2(
                Main.X == null ? -30f : Main.X.Value,
                Main.Y == null ? -250f : Main.Y.Value));
            if (_root.anchoredPosition != spot) _root.anchoredPosition = spot;
            if (_moving)
            {
                Aim();
                Swatch();
            }

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
                * (Main.Glow == null ? 1f : Mathf.Max(0f, Main.Glow.Value));

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
            bool wantDay = Main.ShowDay == null || Main.ShowDay.Value;
            if (!wantDay)
            {
                if (_label.text.Length > 0) _label.text = string.Empty;
                _lastDay = -1;
                return;
            }

            int day = 0;
            try { day = env.GetDay(); }
            catch (Exception) { }
            Color colour = Main.DayColor == null ? DayDefault : Main.DayColor.Value;
            if (day == _lastDay && colour == _dayColor) return;
            _lastDay = day;
            _dayColor = colour;
            _label.text = "<color=#" + ColorUtility.ToHtmlStringRGBA(colour) + ">Day "
                + day.ToString(CultureInfo.InvariantCulture) + "</color>";
            _dayWidth = _label.GetPreferredValues(_label.text).x;
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
        // The colour being picked, as hue, strength and lightness, each 0 to 1.
        private bool _picking;
        private float _pickFrom;
        private float _pickTravel;
        private float _pickH;
        private float _pickS = 1f;
        private float _pickL;
        private float _pickAlpha = 1f;
        /// <summary>The pick started from the shipped colour, which is nearly grey.</summary>
        private bool _pickFromDefault;
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
            // you've moved on. So does taking out a building tool: see
            // Building.
            if (GameScreenOpen() || Building(Player.m_localPlayer))
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
            if (dx != 0f || dy != 0f) Move(dx, dy);

            float grow = 0f;
            if (Held(KeyCode.PageUp) || Held(KeyCode.Equals) || Held(KeyCode.KeypadPlus))
                grow += step * 0.35f;
            if (Held(KeyCode.PageDown) || Held(KeyCode.Minus) || Held(KeyCode.KeypadMinus))
                grow -= step * 0.35f;
            if (grow != 0f && Main.Size != null)
                Main.Size.Value = Mathf.Clamp(Main.Size.Value + grow, 24f, 400f);

            Drag();
        }

        /// <summary>
        /// Move the saved place by this much. It's kept on screen as it's
        /// saved, so pushing past an edge doesn't store a place beyond it
        /// that has to be dragged back before the wheel moves again.
        /// </summary>
        private void Move(float dx, float dy)
        {
            if (Main.X == null || Main.Y == null) return;
            Vector2 at = new Vector2(Main.X.Value, Main.Y.Value);
            Vector2 to = Keep(new Vector2(at.x + dx, at.y + dy));
            if (to.x != at.x) Main.X.Value = to.x;
            if (to.y != at.y) Main.Y.Value = to.y;
        }

        /// <summary>Start moving: stop the character.</summary>
        private void Begin()
        {
            Player me = Player.m_localPlayer;
            if (me == null || GameScreenOpen()) return;
            if (Building(me))
            {
                try
                {
                    me.Message(MessageHud.MessageType.Center,
                        "Put your hammer, hoe or cultivator away to move the wheel.");
                }
                catch (Exception) { }
                return;
            }

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
            Main.Saving(false);
            Shout();

            if (_still == null && Main.Log != null)
                Main.Log.Warn("Daywheel: couldn't pause your controls, so your "
                    + "character may move while you move the wheel.");
        }

        /// <summary>Stop moving and give the controls back.</summary>
        private void End()
        {
            if (!_moving) return;
            _moving = false;
            _held = false;
            _picking = false;
            if (_over) Hover(false);

            try { if (_still != null) _still.enabled = true; }
            catch (Exception) { }
            _still = null;

            Main.Saving(true);
            Shout();

            if (Main.Log != null)
                Main.Log.Info("Daywheel: the wheel is now at X " + Num(Main.X)
                    + ", Y " + Num(Main.Y) + ", size " + Num(Main.Size) + ".");
        }

        /// <summary>
        /// Hold the left button and move the mouse to drag the wheel, from
        /// anywhere on screen. A right-click anywhere is the next look. Hold
        /// the middle button and move to pick the day count's colour; a tap
        /// of it puts back the default.
        /// </summary>
        private void Drag()
        {
            if (Clicked(1)) Turn(1);
            if (Clicked(0)) _held = true;
            if (_held && !Down(0)) _held = false;
            if (_held != _over) Hover(_held);
            if (Clicked(2) && !_held) PickStart();
            if (_picking && !Down(2)) PickEnd();

            Vector2 px = Moved();
            if (px.x == 0f && px.y == 0f) return;

            // Screen pixels into the units the wheel is placed in. The hud's
            // scale on screen includes the game's own interface scaling.
            float scale = _under == null ? 1f : _under.lossyScale.x;
            if (scale < 0.01f) scale = 1f;
            if (_held) Move(px.x / scale, px.y / scale);
            else if (_picking) Pick(px / scale);
        }

        /// <summary>This frame's mouse movement, in screen pixels.</summary>
        private static Vector2 Moved()
        {
            try { return ZInput.GetMouseDelta() * DeltaToPixels; }
            catch (Exception) { return Vector2.zero; }
        }

        private static string Num(Keel.Setting<float> e)
        {
            return e == null ? "?" : e.Value.ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>Step to the next look, or back one. The setting keeps it.</summary>
        private void Turn(int by)
        {
            if (Main.ThemeName == null) return;
            int n = Themes.All.Length;
            int at = (Themes.IndexOf(_theme) + by + n) % n;
            Main.ThemeName.Value = Themes.All[at].Name;
            if (Main.Log != null)
                Main.Log.Info("Daywheel: look is now " + Themes.All[at].Name + ".");
        }

        // ---------------------------------------------------------- colour

        /// <summary>The middle button went down: start from the colour the day count has now.</summary>
        private void PickStart()
        {
            _picking = true;
            _pickFrom = Time.unscaledTime;
            _pickTravel = 0f;
            Color c = DayColour();
            float h;
            float s;
            ToHsl(c, out h, out s, out _pickL);
            // Black, white and greys have no hue or strength of their own, so
            // the last ones are kept for them.
            if (s > 0.001f)
            {
                _pickH = h;
                _pickS = s;
            }
            _pickAlpha = c.a;
            _pickFromDefault = c == DayDefault;
        }

        /// <summary>
        /// Move the pick by this much, in hud units: across for the hue, which
        /// wraps round, and up for lighter, or with Shift held, for stronger.
        /// </summary>
        private void Pick(Vector2 by)
        {
            // A tap wobbles a little. Until it's clearly a move, nothing changes.
            _pickTravel += by.magnitude;
            if (_pickTravel < TapTravel) return;

            bool strength = Held(KeyCode.LeftShift) || Held(KeyCode.RightShift);
            // The shipped colour is a nearly grey tan, and at its strength
            // every hue would look grey. So the first move away from it
            // without Shift goes to full colour; Shift softens it again.
            if (_pickFromDefault && !strength) _pickS = 1f;
            _pickFromDefault = false;

            float across = _fieldRt == null ? 200f : Mathf.Max(40f, _fieldRt.rect.width);
            _pickH = Mathf.Repeat(_pickH + by.x * PickAcross / across, 1f);
            float up = by.y * PickUp / PickHeight;
            if (strength) _pickS = Mathf.Clamp01(_pickS + up);
            else _pickL = Mathf.Clamp01(_pickL + up);

            Color c = FromHsl(_pickH, _pickS, _pickL);
            c.a = _pickAlpha;
            if (Main.DayColor != null) Main.DayColor.Value = c;
        }

        /// <summary>The middle button came up. A tap puts back the default colour.</summary>
        private void PickEnd()
        {
            _picking = false;
            bool tap = _pickTravel < TapTravel && Time.unscaledTime - _pickFrom < TapSeconds;
            if (tap && Main.DayColor != null) Main.DayColor.Value = DayDefault;
            if (Main.Log != null)
                Main.Log.Info("Daywheel: the day colour is now "
                    + ColorUtility.ToHtmlStringRGB(DayColour()) + ".");
        }

        private static Color DayColour()
        {
            return Main.DayColor == null ? DayDefault : Main.DayColor.Value;
        }

        /// <summary>
        /// Show on the colour field where the day count's colour sits, and
        /// draw the field at that colour's strength.
        /// </summary>
        private void Swatch()
        {
            if (_field == null || _fieldRt == null || _dotRt == null) return;
            Color c = DayColour();
            float h = _pickH;
            float s = _pickS;
            float l = _pickL;
            if (!_picking)
            {
                float was;
                float strength;
                ToHsl(c, out was, out strength, out l);
                // Black, white and greys keep the last hue and strength, as
                // in PickStart.
                if (strength > 0.001f)
                {
                    h = was;
                    s = strength;
                }
            }
            // From the shipped colour a move goes to full colour (see Pick),
            // so that's how the field is drawn for it.
            bool shipped = _picking ? _pickFromDefault : c == DayDefault;
            _field.Strength = shipped ? 1f : s;
            Rect r = _fieldRt.rect;
            _dotRt.anchoredPosition = new Vector2(h * r.width, l * r.height);
            if (_dotFill != null) _dotFill.color = new Color(c.r, c.g, c.b, 1f);
        }

        /// <summary>A colour from hue, strength and lightness, each 0 to 1.</summary>
        internal static Color FromHsl(float h, float s, float l)
        {
            float chroma = (1f - Mathf.Abs(2f * l - 1f)) * s;
            float part = Mathf.Repeat(h, 1f) * 6f;
            float x = chroma * (1f - Mathf.Abs(part % 2f - 1f));
            float r = 0f;
            float g = 0f;
            float b = 0f;
            if (part < 1f) { r = chroma; g = x; }
            else if (part < 2f) { r = x; g = chroma; }
            else if (part < 3f) { g = chroma; b = x; }
            else if (part < 4f) { g = x; b = chroma; }
            else if (part < 5f) { r = x; b = chroma; }
            else { r = chroma; b = x; }
            float m = l - chroma * 0.5f;
            return new Color(r + m, g + m, b + m, 1f);
        }

        /// <summary>A colour's hue, strength and lightness, each 0 to 1. A grey's hue is 0.</summary>
        private static void ToHsl(Color c, out float h, out float s, out float l)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            l = (max + min) * 0.5f;
            float d = max - min;
            if (d < 0.0001f)
            {
                h = 0f;
                s = 0f;
                return;
            }
            s = Mathf.Clamp01(d / (1f - Mathf.Abs(2f * l - 1f)));
            if (max == c.r) h = (c.g - c.b) / d;
            else if (max == c.g) h = (c.b - c.r) / d + 2f;
            else h = (c.r - c.g) / d + 4f;
            h = Mathf.Repeat(h / 6f, 1f);
        }

        /// <summary>Lift the mark while the wheel is held, so you can see it's picked up.</summary>
        private void Hover(bool on)
        {
            _over = on;
            if (_markRt != null)
                _markRt.localScale = on ? new Vector3(1.1f, 1.1f, 1f) : Vector3.one;
        }

        /// <summary>What to do, shown only while moving, one thing to a line.</summary>
        private void Shout()
        {
            if (_hint == null || _hintRt == null) return;
            _hintRt.gameObject.SetActive(_moving);
            if (!_moving) return;
            _hint.text = Line("Drag", "hold left mouse and move")
                + "\n" + Line("Nudge", "arrow keys (Shift for big steps, Ctrl for fine)")
                + "\n" + Line("Size", "Page Up bigger, Page Down smaller")
                + "\n" + Line("Look", "right-click or End, Home goes back")
                + "\n" + Line("Colour", "hold middle and move (Shift for strength)")
                + "\n" + Line("Default colour", "tap middle")
                + "\n" + Line("Done", MoveKey().ToString());
            // The box fits the text, with the colour field under it, so it's
            // measured whenever the text is set.
            Vector2 text = _hint.GetPreferredValues(_hint.text);
            _hintRt.sizeDelta = new Vector2(
                text.x + HintPadX * 2f,
                text.y + PickGap + PickHeight + HintPadY * 2f);
        }

        private static string Line(string what, string how)
        {
            return "<color=" + HintKeyHex + ">" + what + ":</color> " + how;
        }

        /// <summary>
        /// The help box's colour. Mixed as light, a see-through dark layer
        /// lets through far more than its number says, so the share that
        /// shows through is taken to the same power as the wheel's soft
        /// pictures (Themes.Soften).
        /// </summary>
        private static Color HintBox()
        {
            float a = Themes.Linear()
                ? 1f - Mathf.Pow(1f - HintShade, Themes.Soften)
                : HintShade;
            return new Color(0f, 0f, 0f, a);
        }

        /// <summary>
        /// Put the help box beside the wheel: under it and its day count if
        /// there's room, otherwise above, and level with the wheel's side
        /// nearer the screen's edge, so the box reaches in toward the middle.
        /// Then it's pushed back inside the screen if any of it is out.
        /// </summary>
        private void Aim()
        {
            if (_hintRt == null || _root == null || _under == null) return;
            Rect r = _under.rect;
            Vector2 lo;
            Vector2 hi;
            if (!ScreenBounds(out lo, out hi))
            {
                lo = r.min;
                hi = r.max;
            }

            // The wheel's top right corner, in the hud's units. The box is
            // placed by its own top right corner, measured from there.
            Vector2 corner = new Vector2(r.xMax, r.yMax) + _root.anchoredPosition;
            Vector2 box = _hintRt.sizeDelta;

            float top = corner.y - _across - DayRoom() - HintGap;
            if (top - box.y < lo.y + HintEdge) top = corner.y + HintGap + box.y;

            float middle = corner.x - _across * 0.5f;
            float right = middle > (lo.x + hi.x) * 0.5f
                ? corner.x
                : corner.x - _across + box.x;

            float rightMin = lo.x + HintEdge + box.x;
            float topMin = lo.y + HintEdge + box.y;
            right = Mathf.Clamp(right, rightMin, Mathf.Max(rightMin, hi.x - HintEdge));
            top = Mathf.Clamp(top, topMin, Mathf.Max(topMin, hi.y - HintEdge));

            _hintRt.anchoredPosition = new Vector2(right, top) - corner;
        }

        /// <summary>
        /// A place for the wheel, measured like the X and Y settings from the
        /// hud's top right corner, moved just enough that the wheel and its
        /// day count are on screen.
        /// </summary>
        private Vector2 Keep(Vector2 at)
        {
            Vector2 lo;
            Vector2 hi;
            if (_under == null || !ScreenBounds(out lo, out hi))
                return new Vector2(Mathf.Min(0f, at.x), Mathf.Min(0f, at.y));

            Rect r = _under.rect;
            float spill = DaySpill();
            float left = lo.x - r.xMax + _across + spill;
            float right = hi.x - r.xMax - spill;
            float bottom = lo.y - r.yMax + _across + DayRoom();
            float top = hi.y - r.yMax;
            at.x = Mathf.Clamp(at.x, left, Mathf.Max(left, right));
            at.y = Mathf.Clamp(at.y, bottom, Mathf.Max(bottom, top));
            return at;
        }

        /// <summary>
        /// The height the day count takes up under the wheel: its gap, and
        /// a line of text, which is a little taller than the font size.
        /// </summary>
        private float DayRoom()
        {
            bool wantDay = Main.ShowDay == null || Main.ShowDay.Value;
            return wantDay && _label != null ? 2f + _label.fontSize * 1.3f : 0f;
        }

        /// <summary>
        /// How far the day count reaches past each side of the wheel. Only a
        /// small wheel has one: its text can't shrink below a readable size.
        /// </summary>
        private float DaySpill()
        {
            bool wantDay = Main.ShowDay == null || Main.ShowDay.Value;
            return wantDay && _label != null ? Mathf.Max(0f, (_dayWidth - _across) * 0.5f) : 0f;
        }

        /// <summary>
        /// The screen's bottom left and top right corners, in the hud's own
        /// units. False if they can't be worked out.
        /// </summary>
        private bool ScreenBounds(out Vector2 lo, out Vector2 hi)
        {
            lo = Vector2.zero;
            hi = Vector2.zero;
            if (_under == null) return false;
            try
            {
                Camera eye = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _canvas.worldCamera;
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _under, Vector2.zero, eye, out lo)
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _under, new Vector2(Screen.width, Screen.height), eye, out hi);
            }
            catch (Exception) { return false; }
        }

        /// <summary>Is one of the game's own screens open, the build menu among them?</summary>
        private static bool GameScreenOpen()
        {
            try
            {
                return InventoryGui.IsVisible() || StoreGui.IsVisible()
                    || Menu.IsVisible() || Minimap.IsOpen()
                    || Hud.IsPieceSelectionVisible();
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Is a hammer, hoe or cultivator out? With one out, the game reads
        /// the mouse itself (Player.UpdatePlacement), not through the
        /// controls paused while moving: a left-click places a piece, a
        /// right-click opens the build menu and a middle-click removes one.
        /// So the wheel isn't moved while one is out.
        /// </summary>
        private static bool Building(Player me)
        {
            try { return me != null && me.InPlaceMode(); }
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
            if (Main.MoveKey == null) return KeyCode.F8;
            string name = Main.MoveKey.Value;
            KeyCode k;
            if (Enum.TryParse(name, true, out k)) return k;
            if (!string.Equals(name, _badKey, StringComparison.Ordinal))
            {
                _badKey = name;
                if (Main.Log != null)
                    Main.Log.Warn("Daywheel: MoveKey \"" + name + "\" isn't a key "
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

        /// <summary>
        /// Hidden with the rest of the hud. For a cutscene or sleep the game
        /// doesn't switch the hud off, it moves it far off to the side
        /// (Hud.SetVisible), and Keep would pull the wheel back into view.
        /// </summary>
        private static bool Hidden()
        {
            try
            {
                return Hud.IsUserHidden()
                    || (Hud.instance != null && !Hud.instance.IsVisible());
            }
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

            float across = Main.Size == null ? 62f : Main.Size.Value;
            float x = Main.X == null ? -30f : Main.X.Value;
            float y = Main.Y == null ? -250f : Main.Y.Value;

            _under = under.transform as RectTransform;
            if (_under == null) return false;
            Canvas canvas = _under.GetComponentInParent<Canvas>();
            _canvas = canvas == null ? null : canvas.rootCanvas;

            // The hud goes when the world unloads, and the last wheel went
            // with it. Nothing of that one carries over: the new pictures
            // need a look put on them and the day count needs writing.
            _pieces.Clear();
            _theme = null;
            _worn = null;
            _lastDay = -1;
            _held = false;
            _over = false;
            _picking = false;

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

            // What to do, shown only while moving: light text on a dark box,
            // placed beside the wheel each frame by Aim. It stays switched
            // off until then, so the text has its font long before it wakes.
            GameObject tip = new GameObject("Hint", typeof(RectTransform));
            tip.SetActive(false);
            tip.transform.SetParent(_root, false);
            _hintRt = tip.GetComponent<RectTransform>();
            _hintRt.anchorMin = new Vector2(1f, 1f);
            _hintRt.anchorMax = new Vector2(1f, 1f);
            _hintRt.pivot = new Vector2(1f, 1f);
            Image box = tip.AddComponent<Image>();
            box.color = HintBox();
            box.raycastTarget = false;

            GameObject words = new GameObject("Text", typeof(RectTransform));
            words.SetActive(false);
            words.transform.SetParent(tip.transform, false);
            RectTransform wordsRt = words.GetComponent<RectTransform>();
            wordsRt.anchorMin = Vector2.zero;
            wordsRt.anchorMax = Vector2.one;
            wordsRt.offsetMin = new Vector2(HintPadX, HintPadY + PickHeight + PickGap);
            wordsRt.offsetMax = new Vector2(-HintPadX, -HintPadY);
            TextMeshProUGUI tt = words.AddComponent<TextMeshProUGUI>();
            tt.font = hud.m_messageText.font;
            tt.fontSharedMaterial = hud.m_messageText.fontSharedMaterial;
            tt.fontSize = HintSize;
            tt.color = HintText;
            tt.alignment = TextAlignmentOptions.TopLeft;
            tt.textWrappingMode = TextWrappingModes.NoWrap;
            tt.raycastTarget = false;
            tt.richText = true;
            words.SetActive(true);
            _hint = tt;

            // The colour field under the text, as wide as the box, and its
            // marker: a dark edge, a light ring, and the colour itself.
            GameObject field = new GameObject("Colours", typeof(RectTransform));
            field.transform.SetParent(tip.transform, false);
            _fieldRt = field.GetComponent<RectTransform>();
            _fieldRt.anchorMin = new Vector2(0f, 0f);
            _fieldRt.anchorMax = new Vector2(1f, 0f);
            _fieldRt.pivot = new Vector2(0.5f, 0f);
            _fieldRt.offsetMin = new Vector2(HintPadX, HintPadY);
            _fieldRt.offsetMax = new Vector2(-HintPadX, HintPadY + PickHeight);
            _field = field.AddComponent<ColourField>();
            _field.raycastTarget = false;

            _dotRt = Square(_fieldRt, "Marker", 11f, Color.black, out _);
            _dotRt.anchorMin = Vector2.zero;
            _dotRt.anchorMax = Vector2.zero;
            RectTransform ringRt = Square(_dotRt, "Ring", 9f, Color.white, out _);
            Square(ringRt, "Fill", 5f, DayColour(), out _dotFill);

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
            {
                float size = Mathf.Clamp(across * 0.20f, 9f, 28f);
                if (_label.fontSize != size)
                {
                    _label.fontSize = size;
                    // Written again at the new size, which measures it again.
                    _lastDay = -1;
                }
            }
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

        /// <summary>A plain square of one colour, centred on its parent.</summary>
        private static RectTransform Square(Transform parent, string name, float size,
                                            Color colour, out Image img)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            img = go.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return rt;
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
            _hintRt = null;
            _field = null;
            _fieldRt = null;
            _dotRt = null;
            _dotFill = null;
            _picking = false;
            _theme = null;
            _worn = null;
            _under = null;
            _canvas = null;
            _held = false;
            _over = false;
            _pieces.Clear();
        }
    }
}
