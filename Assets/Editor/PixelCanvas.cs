using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// A plain indexed pixel buffer. Deliberately has no antialiasing: pixel art needs hard edges,
    /// and every shape here snaps to whole pixels. Uses a seeded RNG so regenerating the art twice
    /// produces identical files rather than churning the repo.
    /// </summary>
    public class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;

        readonly Color32[] _pixels;
        System.Random _rng;

        public PixelCanvas(int width, int height, int seed = 12345)
        {
            Width = width;
            Height = height;
            _pixels = new Color32[width * height];
            _rng = new System.Random(seed);
        }

        public void Seed(int seed) { _rng = new System.Random(seed); }
        public int RandomRange(int minInclusive, int maxExclusive) { return _rng.Next(minInclusive, maxExclusive); }
        public float Random01() { return (float)_rng.NextDouble(); }
        public bool Chance(float probability) { return _rng.NextDouble() < probability; }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public Color32 Get(int x, int y)
        {
            return InBounds(x, y) ? _pixels[y * Width + x] : new Color32(0, 0, 0, 0);
        }

        public void Set(int x, int y, Color32 c)
        {
            if (!InBounds(x, y)) return;
            if (c.a == 0) return;
            _pixels[y * Width + x] = c;
        }

        /// <summary>Writes even when transparent - used to punch holes.</summary>
        public void SetRaw(int x, int y, Color32 c)
        {
            if (!InBounds(x, y)) return;
            _pixels[y * Width + x] = c;
        }

        public void Clear(Color32 c)
        {
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = c;
        }

        public void FillRect(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Set(xx, yy, c);
        }

        public void RectOutline(int x, int y, int w, int h, Color32 c)
        {
            for (int xx = x; xx < x + w; xx++) { Set(xx, y, c); Set(xx, y + h - 1, c); }
            for (int yy = y; yy < y + h; yy++) { Set(x, yy, c); Set(x + w - 1, yy, c); }
        }

        public void FillCircle(int cx, int cy, float radius, Color32 c)
        {
            int r = Mathf.CeilToInt(radius);
            for (int yy = -r; yy <= r; yy++)
                for (int xx = -r; xx <= r; xx++)
                    if (xx * xx + yy * yy <= radius * radius)
                        Set(cx + xx, cy + yy, c);
        }

        public void FillEllipse(int cx, int cy, float rx, float ry, Color32 c)
        {
            int ix = Mathf.CeilToInt(rx), iy = Mathf.CeilToInt(ry);
            for (int yy = -iy; yy <= iy; yy++)
                for (int xx = -ix; xx <= ix; xx++)
                {
                    float nx = xx / Mathf.Max(0.01f, rx);
                    float ny = yy / Mathf.Max(0.01f, ry);
                    if (nx * nx + ny * ny <= 1f) Set(cx + xx, cy + yy, c);
                }
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Filled triangle - the base shape for pine trees and spikes.</summary>
        public void FillTriangle(int x0, int y0, int x1, int y1, int x2, int y2, Color32 c)
        {
            int minX = Mathf.Min(x0, Mathf.Min(x1, x2));
            int maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
            int minY = Mathf.Min(y0, Mathf.Min(y1, y2));
            int maxY = Mathf.Max(y0, Mathf.Max(y1, y2));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int d1 = Sign(x, y, x0, y0, x1, y1);
                    int d2 = Sign(x, y, x1, y1, x2, y2);
                    int d3 = Sign(x, y, x2, y2, x0, y0);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0;
                    bool pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Set(x, y, c);
                }
            }
        }

        static int Sign(int px, int py, int ax, int ay, int bx, int by)
        {
            return (px - bx) * (ay - by) - (ax - bx) * (py - by);
        }

        /// <summary>Scatters single pixels inside a rect - moss, grit, stars.</summary>
        public void Speckle(int x, int y, int w, int h, Color32 c, float density)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    if (Chance(density)) Set(xx, yy, c);
        }

        /// <summary>Adds a 1px outline around every opaque pixel. The pixel-art readability trick.</summary>
        public void Outline(Color32 outline, bool diagonals = false)
        {
            var copy = (Color32[])_pixels.Clone();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (copy[y * Width + x].a != 0) continue;
                    if (HasOpaqueNeighbour(copy, x, y, diagonals))
                        _pixels[y * Width + x] = outline;
                }
            }
        }

        bool HasOpaqueNeighbour(Color32[] buffer, int x, int y, bool diagonals)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!diagonals && dx != 0 && dy != 0) continue;

                    int nx = x + dx, ny = y + dy;
                    if (!InBounds(nx, ny)) continue;
                    if (buffer[ny * Width + nx].a != 0) return true;
                }
            }
            return false;
        }

        /// <summary>Lightens or darkens every opaque pixel in a horizontal band - cheap shading.</summary>
        public void ShadeBand(int y, int h, float amount)
        {
            for (int yy = y; yy < y + h; yy++)
            {
                for (int xx = 0; xx < Width; xx++)
                {
                    var c = Get(xx, yy);
                    if (c.a == 0) continue;
                    _pixels[yy * Width + xx] = Shade(c, amount);
                }
            }
        }

        public static Color32 Shade(Color32 c, float amount)
        {
            return new Color32(
                (byte)Mathf.Clamp(c.r + c.r * amount, 0, 255),
                (byte)Mathf.Clamp(c.g + c.g * amount, 0, 255),
                (byte)Mathf.Clamp(c.b + c.b * amount, 0, 255),
                c.a);
        }

        public static Color32 Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels32(_pixels);
            tex.Apply();
            return tex;
        }
    }
}
