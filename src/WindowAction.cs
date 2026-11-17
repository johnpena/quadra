using System;
using System.Drawing;
using System.Text;

namespace Quadra
{
    internal enum WindowAction
    {
        LeftHalf,
        RightHalf,
        CenterHalf,
        TopHalf,
        BottomHalf,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        FirstThird,
        CenterThird,
        LastThird,
        FirstTwoThirds,
        CenterTwoThirds,
        LastTwoThirds,
        Maximize,
        AlmostMaximize,
        MaximizeHeight,
        Larger,
        Smaller,
        Center,
        Restore,
        NextDisplay,
        PreviousDisplay
    }

    internal static class ActionNames
    {
        /// <summary>"FirstTwoThirds" -> "First Two Thirds".</summary>
        public static string Display(WindowAction action)
        {
            string s = action.ToString();
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>Pure geometry: maps actions to fractions of a work area, then to pixels.</summary>
    internal static class Layout
    {
        // Repeating a half action cycles through these sizes, like Rectangle.
        private static readonly float[] CycleFractions = { 1f / 2f, 2f / 3f, 1f / 3f };
        public const int CycleLength = 3;

        public static bool IsCycling(WindowAction a)
        {
            return a == WindowAction.LeftHalf || a == WindowAction.RightHalf
                || a == WindowAction.TopHalf || a == WindowAction.BottomHalf;
        }

        /// <summary>Unit-square rectangle for fractional layouts; null for actions that depend on the window.</summary>
        public static RectangleF? Fraction(WindowAction a, bool landscape, int cycle)
        {
            float c = CycleFractions[cycle % CycleLength];
            switch (a)
            {
                case WindowAction.LeftHalf: return new RectangleF(0, 0, c, 1);
                case WindowAction.RightHalf: return new RectangleF(1 - c, 0, c, 1);
                case WindowAction.TopHalf: return new RectangleF(0, 0, 1, c);
                case WindowAction.BottomHalf: return new RectangleF(0, 1 - c, 1, c);
                case WindowAction.CenterHalf:
                    return landscape ? new RectangleF(0.25f, 0, 0.5f, 1) : new RectangleF(0, 0.25f, 1, 0.5f);
                case WindowAction.TopLeft: return new RectangleF(0, 0, 0.5f, 0.5f);
                case WindowAction.TopRight: return new RectangleF(0.5f, 0, 0.5f, 0.5f);
                case WindowAction.BottomLeft: return new RectangleF(0, 0.5f, 0.5f, 0.5f);
                case WindowAction.BottomRight: return new RectangleF(0.5f, 0.5f, 0.5f, 0.5f);
                case WindowAction.FirstThird: return Thirds(0, 1, landscape);
                case WindowAction.CenterThird: return Thirds(1, 1, landscape);
                case WindowAction.LastThird: return Thirds(2, 1, landscape);
                case WindowAction.FirstTwoThirds: return Thirds(0, 2, landscape);
                case WindowAction.CenterTwoThirds: return Thirds(0.5f, 2, landscape);
                case WindowAction.LastTwoThirds: return Thirds(1, 2, landscape);
                case WindowAction.Maximize: return new RectangleF(0, 0, 1, 1);
                case WindowAction.AlmostMaximize: return new RectangleF(0.05f, 0.05f, 0.9f, 0.9f);
                default: return null;
            }
        }

        // Thirds run left-to-right on landscape screens and top-to-bottom on portrait ones.
        private static RectangleF Thirds(float start, float span, bool landscape)
        {
            float s = start / 3f, w = span / 3f;
            return landscape ? new RectangleF(s, 0, w, 1) : new RectangleF(0, s, 1, w);
        }

        /// <summary>
        /// Converts a unit rectangle to pixels. Edges on the work-area boundary get a full gap,
        /// interior edges get half, so two adjacent windows end up exactly one gap apart.
        /// </summary>
        public static Rectangle ToPixels(RectangleF f, Rectangle wa, int gap)
        {
            int left = wa.Left + (int)Math.Round(f.Left * wa.Width);
            int right = wa.Left + (int)Math.Round(f.Right * wa.Width);
            int top = wa.Top + (int)Math.Round(f.Top * wa.Height);
            int bottom = wa.Top + (int)Math.Round(f.Bottom * wa.Height);

            if (gap > 0)
            {
                int innerLo = gap - gap / 2, innerHi = gap / 2;
                left += left <= wa.Left ? gap : innerLo;
                top += top <= wa.Top ? gap : innerLo;
                right -= right >= wa.Right ? gap : innerHi;
                bottom -= bottom >= wa.Bottom ? gap : innerHi;
            }
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        /// <summary>
        /// Grows (delta &gt; 0) or shrinks a window. Edges stuck to the bounds stay put and the
        /// opposite edge moves twice as far, so a left-half window grows to the right.
        /// </summary>
        public static Rectangle Resize(Rectangle r, Rectangle bounds, int delta, Size min)
        {
            const int tol = 2;
            int left = r.Left, right = r.Right, top = r.Top, bottom = r.Bottom;
            ResizeAxis(ref left, ref right, Math.Abs(r.Left - bounds.Left) <= tol,
                Math.Abs(r.Right - bounds.Right) <= tol, delta, min.Width);
            ResizeAxis(ref top, ref bottom, Math.Abs(r.Top - bounds.Top) <= tol,
                Math.Abs(r.Bottom - bounds.Bottom) <= tol, delta, min.Height);

            Rectangle result = Rectangle.FromLTRB(left, top, right, bottom);
            if (delta > 0)
            {
                Rectangle clipped = Rectangle.Intersect(result, bounds);
                if (clipped.Width > 0 && clipped.Height > 0) result = clipped;
            }
            return result;
        }

        private static void ResizeAxis(ref int lo, ref int hi, bool loAnchored, bool hiAnchored, int d, int min)
        {
            int newLo = lo, newHi = hi;
            if (loAnchored && hiAnchored)
            {
                if (d > 0) return; // already spans the whole axis
                newLo -= d; newHi += d;
            }
            else if (loAnchored) newHi += 2 * d;
            else if (hiAnchored) newLo -= 2 * d;
            else { newLo -= d; newHi += d; }

            if (newHi - newLo < min) return;
            lo = newLo; hi = newHi;
        }

        /// <summary>Maps a rectangle proportionally from one work area to another.</summary>
        public static Rectangle MapBetween(Rectangle r, Rectangle from, Rectangle to)
        {
            double sx = (double)to.Width / from.Width, sy = (double)to.Height / from.Height;
            int x = to.X + (int)Math.Round((r.X - from.X) * sx);
            int y = to.Y + (int)Math.Round((r.Y - from.Y) * sy);
            int w = Math.Min(to.Width, (int)Math.Round(r.Width * sx));
            int h = Math.Min(to.Height, (int)Math.Round(r.Height * sy));
            x = Math.Max(to.Left, Math.Min(x, to.Right - w));
            y = Math.Max(to.Top, Math.Min(y, to.Bottom - h));
            return new Rectangle(x, y, w, h);
        }
    }
}
