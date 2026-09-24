using UnityEngine;
using UnityEngine.UI;

namespace Daywheel
{
    /// <summary>
    /// The colour field shown while moving the wheel: hue from left to right,
    /// dark at the bottom and light at the top, at one strength.
    ///
    /// It's drawn with vertex colours, the same way the day count's text is
    /// coloured, so the field and the text agree whichever colour space the
    /// game draws in. A picture wouldn't promise that.
    /// </summary>
    internal sealed class ColourField : MaskableGraphic
    {
        private const int Across = 24;
        private const int Up = 8;
        private float _strength = 1f;

        /// <summary>How strong every colour on the field is, 0 (grey) to 1.</summary>
        internal float Strength
        {
            get { return _strength; }
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - _strength) < 0.002f) return;
                _strength = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            for (int y = 0; y <= Up; y++)
            {
                float light = y / (float)Up;
                for (int x = 0; x <= Across; x++)
                {
                    float hue = x / (float)Across;
                    Vector3 at = new Vector3(r.xMin + hue * r.width, r.yMin + light * r.height, 0f);
                    vh.AddVert(at, Wheel.FromHsl(hue, _strength, light), Vector2.zero);
                }
            }

            int row = Across + 1;
            for (int y = 0; y < Up; y++)
            {
                for (int x = 0; x < Across; x++)
                {
                    int i = y * row + x;
                    vh.AddTriangle(i, i + row, i + row + 1);
                    vh.AddTriangle(i, i + row + 1, i + 1);
                }
            }
        }
    }
}
