using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Builds Nyx's 20 hero sheets (same layout as Mira/Kai: 32x32 cells; walk and roll 4 frames, slash 5)
// from her hand-drawn 4-direction walk sheet. Drawn frames are kept as-is; the rest are derived:
// diagonal walks from the side views, rolls by rotating
// a tucked pose, and a placeholder "gadget" attack. Re-run after editing the source sheet.
public static class NyxSheetBuilder
{
    private const string Dir = "Assets/Resources/Heroes/Nyx", Source = Dir + "/pixil-frame-0.png", Template = "Assets/Resources/Heroes/Kai/Character_";
    private const int Cell = 32, CellTop = 3;

    private static readonly int[] Palette = {
        0xF57C00, 0xE65100, 0xEF6C00, 0x000000, 0xF8D0B8, 0x2D7044, 0x945B50, 0x212121, 0xDBA57E, 0xB5B5B5,
        0xD50000, 0xFFFFFF, 0x225232, 0xF44336, 0xF5F5F5, 0xBB8367, 0xD6D6D6, 0xE3E3E3 };
    private static readonly Color32 Glow = Hex(0x00E5FF), GlowCore = Hex(0xE0FFFF);

    // A frame, top-origin like a paint program.
    private class Img
    {
        public readonly int w, h;
        public readonly Color32[] px;
        public Img(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }
        public Color32 this[int x, int y] { get => px[x + y * w]; set => px[x + y * w] = value; }
        public bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && this[x, y].a > 0;
    }

    private struct Frame { public Img img; public int top; }

    [MenuItem("Tools/Build Nyx Sheets")]
    public static void Build()
    {
        var src = new Texture2D(2, 2);
        src.LoadImage(File.ReadAllBytes(Source));
        int H = src.height;
        Color32 Get(int x, int y) => src.GetPixel(x, H - 1 - y);

        // Frame rects from the importer (x, unityY, w, h): rows are down, up, left, right.
        int[][] rects = {
            new[]{0,78,19,25}, new[]{22,76,19,27}, new[]{44,78,19,25}, new[]{66,76,19,27},
            new[]{0,52,19,25}, new[]{22,51,19,26}, new[]{44,52,19,25}, new[]{66,51,19,26},
            new[]{1,27,17,25}, new[]{24,26,17,26}, new[]{45,27,17,25}, new[]{67,26,17,26},
            new[]{1,0,17,25},  new[]{23,0,17,26},  new[]{45,0,17,25},  new[]{67,0,17,26},
        };
        var rows = new Dictionary<string, Frame[]>();
        string[] facing = { "Down", "Up", "Left", "Right" };
        for (int r = 0; r < 4; r++)
        {
            int bandTop = int.MaxValue;
            for (int i = 0; i < 4; i++) bandTop = Math.Min(bandTop, H - (rects[r * 4 + i][1] + rects[r * 4 + i][3]));
            var frames = new Frame[4];
            for (int i = 0; i < 4; i++)
            {
                int[] q = rects[r * 4 + i];
                int top = H - (q[1] + q[3]);
                var img = new Img(q[2], q[3]);
                for (int y = 0; y < q[3]; y++)
                    for (int x = 0; x < q[2]; x++)
                    {
                        Color32 c = Get(q[0] + x, top + y);
                        img[x, y] = c.a < 128 ? new Color32(0, 0, 0, 0) : Snap(c);
                    }
                frames[i] = new Frame { img = img, top = top - bandTop }; // keeps the walk bob
            }
            rows[facing[r]] = frames;
        }
        UnityEngine.Object.DestroyImmediate(src);

        // Diagonals use the side views with the face showing (like Kai's up-left, which shows his eye and cheek).
        rows["DownLeft"] = rows["UpLeft"] = rows["Left"];
        rows["DownRight"] = rows["UpRight"] = rows["Right"];
        string[] eight = { "Down", "DownLeft", "DownRight", "Left", "Right", "Up", "UpLeft", "UpRight" };

        foreach (string d in eight)
        {
            var sheet = new Img(Cell * 4, Cell);
            for (int i = 0; i < 4; i++) Blit(sheet, i, rows[d][i].img, 0, rows[d][i].top);
            Save(sheet, d);
        }

        // Rolls: tuck, tumble round, land upright.
        foreach (string d in eight)
        {
            bool cw = !d.Contains("Left");
            Img r1 = Rot90(Squash(rows[d][0].img), cw), r2 = Rot90(r1, cw), r3 = Rot90(r2, cw);
            var sheet = new Img(Cell * 4, Cell);
            Blit(sheet, 0, r1, 0, 8);
            Blit(sheet, 1, r2, 0, 6);
            Blit(sheet, 2, r3, 0, 8);
            Blit(sheet, 3, rows[d][0].img, 0, rows[d][0].top);
            Save(sheet, "Roll" + d);
        }

        // Attack (5 frames): lean back, push forward with a glowing gadget, settle.
        foreach (string d in new[] { "DownLeft", "DownRight", "UpLeft", "UpRight" })
        {
            Frame f = rows[d][0];
            int side = d.Contains("Left") ? -1 : 1;
            int handX = (Cell - f.img.w) / 2 + (side > 0 ? f.img.w : -1), handY = CellTop + f.top + f.img.h * 11 / 20;
            var sheet = new Img(Cell * 5, Cell);
            Blit(sheet, 0, f.img, 0, f.top);
            Blit(sheet, 1, f.img, -side, f.top + 1);
            Blit(sheet, 2, f.img, side, f.top); Spark(sheet, 2, handX + side * 2, handY, 1);
            Blit(sheet, 3, f.img, side, f.top); Spark(sheet, 3, handX + side * 3, handY, 2);
            Blit(sheet, 4, f.img, 0, f.top);
            Save(sheet, "Slash" + d);
        }
        AssetDatabase.Refresh();
        Debug.Log("Nyx sheets built.");
    }

    private static Img Rot90(Img f, bool clockwise)
    {
        var o = new Img(f.h, f.w);
        for (int y = 0; y < f.h; y++)
            for (int x = 0; x < f.w; x++)
                if (clockwise) o[f.h - 1 - y, x] = f[x, y]; else o[y, f.w - 1 - x] = f[x, y];
        return o;
    }

    private static Img Squash(Img f)
    {
        var o = new Img(f.w, Math.Max(1, f.h * 4 / 5));
        for (int y = 0; y < o.h; y++) for (int x = 0; x < f.w; x++) o[x, y] = f[x, y * f.h / o.h];
        return o;
    }

    // Frame centred in its cell (+dx), top at CellTop (+dy).
    private static void Blit(Img sheet, int cell, Img f, int dx, int dy)
    {
        int ox = cell * Cell + (Cell - f.w) / 2 + dx, oy = CellTop + dy;
        for (int y = 0; y < f.h; y++)
            for (int x = 0; x < f.w; x++)
            {
                int px = ox + x, py = oy + y;
                if (f[x, y].a > 0 && px >= cell * Cell && px < (cell + 1) * Cell && py >= 0 && py < Cell) sheet[px, py] = f[x, y];
            }
    }

    private static void Spark(Img sheet, int cell, int x, int y, int size)
    {
        for (int j = -size; j <= size; j++)
            for (int i = -size; i <= size; i++)
            {
                int px = cell * Cell + x + i, py = y + j;
                if (Math.Abs(i) + Math.Abs(j) <= size && px >= cell * Cell && px < (cell + 1) * Cell && py >= 0 && py < Cell)
                    sheet[px, py] = i == 0 && j == 0 ? GlowCore : Glow;
            }
    }

    // Writes Nyx_<name>.png, and on first build a .meta copied from Kai's matching sheet (same slicing, new names/guid).
    private static void Save(Img sheet, string name)
    {
        var tex = new Texture2D(sheet.w, sheet.h, TextureFormat.RGBA32, false);
        for (int y = 0; y < sheet.h; y++) for (int x = 0; x < sheet.w; x++) tex.SetPixel(x, sheet.h - 1 - y, sheet[x, y]);
        string path = $"{Dir}/Nyx_{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        if (!File.Exists(path + ".meta"))
        {
            string meta = File.ReadAllText(Template + name + ".png.meta").Replace("Character_", "Nyx_");
            int g = meta.IndexOf("guid: ") + 6;
            meta = meta.Substring(0, g) + Guid.NewGuid().ToString("N") + meta.Substring(g + 32);
            File.WriteAllText(path + ".meta", meta);
        }
    }

    private static Color32 Snap(Color32 c)
    {
        int best = 0, bestD = int.MaxValue;
        foreach (int p in Palette)
        {
            int dr = ((p >> 16) & 255) - c.r, dg = ((p >> 8) & 255) - c.g, db = (p & 255) - c.b, dist = dr * dr + dg * dg + db * db;
            if (dist < bestD) { bestD = dist; best = p; }
        }
        return Hex(best);
    }

    private static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    private static int Rgb(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;
}
