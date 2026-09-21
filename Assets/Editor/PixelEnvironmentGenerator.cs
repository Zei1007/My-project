using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Generates the dungeon tileset, props and the fantasy UI frames.
    ///
    /// Floor tiles come in variants so a large arena does not read as one repeated stamp - the
    /// tiler picks between them, which is what keeps a flat grid from looking like wallpaper.
    /// UI frames are authored with 9-slice borders so they scale to any panel size.
    /// </summary>
    public static class PixelEnvironmentGenerator
    {
        public const string EnvDir = "Assets/Art/Environment";
        public const string UiDir = "Assets/Art/UI";

        public const int Tile = 32;
        public const float PixelsPerUnit = 32f;

        static readonly Color32 Outline = PixelCanvas.Hex("#181321");

        // Dungeon palette, pulled toward the teal/green of the reference.
        static readonly Color32 FloorBase = PixelCanvas.Hex("#22434C");
        static readonly Color32 FloorLight = PixelCanvas.Hex("#2B535D");
        static readonly Color32 FloorDark = PixelCanvas.Hex("#1A353D");
        static readonly Color32 Grout = PixelCanvas.Hex("#14282F");
        static readonly Color32 Moss = PixelCanvas.Hex("#356B4A");

        static readonly Color32 Stone = PixelCanvas.Hex("#4B5A66");
        static readonly Color32 StoneLight = PixelCanvas.Hex("#5F707C");
        static readonly Color32 StoneDark = PixelCanvas.Hex("#36434C");

        static readonly Color32 Grass = PixelCanvas.Hex("#4E8F3F");
        static readonly Color32 GrassLight = PixelCanvas.Hex("#69AB50");
        static readonly Color32 GrassDark = PixelCanvas.Hex("#356B2C");

        static readonly Color32 Wood = PixelCanvas.Hex("#8A5A34");
        static readonly Color32 WoodLight = PixelCanvas.Hex("#A97244");
        static readonly Color32 WoodDark = PixelCanvas.Hex("#5F3C22");

        [MenuItem("Tools/Zombie Shooter/Generate Pixel Art (Environment + UI)")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(EnvDir);
            Directory.CreateDirectory(UiDir);

            var plain = new List<string>();
            var sliced = new List<KeyValuePair<string, Vector4>>();

            plain.AddRange(FloorTiles());
            plain.AddRange(Walls());
            plain.AddRange(Props());
            plain.AddRange(TorchFrames());

            sliced.AddRange(UiFrames());

            AssetDatabase.Refresh();

            foreach (var path in plain) PixelImport.Apply(path, PixelsPerUnit);
            foreach (var kv in sliced) PixelImport.Apply(kv.Key, PixelsPerUnit, kv.Value);

            AssetDatabase.SaveAssets();
            Debug.Log("[PixelEnvironmentGenerator] wrote " + (plain.Count + sliced.Count) + " sprites.");
        }

        // --- floor -------------------------------------------------------------

        static List<string> FloorTiles()
        {
            var list = new List<string>();

            for (int variant = 0; variant < 4; variant++)
            {
                var c = new PixelCanvas(Tile, Tile, 500 + variant);
                c.FillRect(0, 0, Tile, Tile, FloorBase);

                // Flagstone seams around the edge, with a lit top-left bevel.
                c.FillRect(0, 0, Tile, 1, Grout);
                c.FillRect(0, 0, 1, Tile, Grout);
                c.FillRect(0, Tile - 1, Tile, 1, FloorLight);
                c.FillRect(Tile - 1, 0, 1, Tile, FloorDark);

                // Grit, so no two tiles are identical.
                c.Speckle(2, 2, Tile - 4, Tile - 4, FloorDark, 0.05f);
                c.Speckle(2, 2, Tile - 4, Tile - 4, FloorLight, 0.03f);

                if (variant == 1)
                {
                    // Cracked slab.
                    c.Line(6, 24, 14, 14, FloorDark);
                    c.Line(14, 14, 11, 6, FloorDark);
                }
                else if (variant == 2)
                {
                    // Moss patch in a corner.
                    c.FillEllipse(24, 8, 5f, 3f, Moss);
                    c.Speckle(18, 4, 12, 8, PixelCanvas.Shade(Moss, 0.2f), 0.18f);
                }
                else if (variant == 3)
                {
                    // Quartered slab.
                    c.FillRect(0, Tile / 2, Tile, 1, Grout);
                    c.FillRect(Tile / 2, 0, 1, Tile, Grout);
                    c.FillRect(0, Tile / 2 + 1, Tile, 1, FloorLight);
                }

                list.Add(Write(c, EnvDir + "/TILE_Floor_" + variant + ".png"));
            }
            return list;
        }

        // --- walls -------------------------------------------------------------

        static List<string> Walls()
        {
            var list = new List<string>();

            // Wall block with a grass cap - the signature of the reference's dungeon edges.
            {
                var c = new PixelCanvas(Tile, Tile, 520);
                c.FillRect(0, 0, Tile, 24, Stone);

                // Brick courses.
                for (int y = 4; y < 24; y += 7)
                {
                    c.FillRect(0, y, Tile, 1, StoneDark);
                    int offset = ((y / 7) % 2) * 16;
                    c.FillRect(offset, y, 1, 7, StoneDark);
                    c.FillRect((offset + 16) % Tile, y, 1, 7, StoneDark);
                }
                c.Speckle(1, 1, Tile - 2, 22, StoneLight, 0.04f);

                // Grass cap with a ragged lower edge.
                c.FillRect(0, 24, Tile, 8, Grass);
                c.FillRect(0, 30, Tile, 2, GrassLight);
                for (int x = 0; x < Tile; x++)
                    if (c.Chance(0.4f)) c.Set(x, 23, GrassDark);
                c.FillRect(0, 24, Tile, 1, GrassDark);

                list.Add(Write(c, EnvDir + "/TILE_Wall.png"));
            }

            // Plain stone block for interior pillars.
            {
                var c = new PixelCanvas(Tile, Tile, 521);
                c.FillRect(0, 0, Tile, Tile, Stone);
                c.RectOutline(0, 0, Tile, Tile, StoneDark);
                c.FillRect(1, Tile - 3, Tile - 2, 2, StoneLight);
                c.Speckle(2, 2, Tile - 4, Tile - 4, StoneLight, 0.05f);
                c.Speckle(2, 2, Tile - 4, Tile - 4, StoneDark, 0.05f);
                list.Add(Write(c, EnvDir + "/TILE_Stone.png"));
            }
            return list;
        }

        // --- props -------------------------------------------------------------

        static List<string> Props()
        {
            var list = new List<string>();

            // Pine tree: three stacked canopies over a trunk.
            {
                var c = new PixelCanvas(30, 42, 540);
                c.FillRect(13, 2, 4, 8, WoodDark);
                c.FillTriangle(15, 20, 3, 8, 27, 8, GrassDark);
                c.FillTriangle(15, 29, 5, 17, 25, 17, Grass);
                c.FillTriangle(15, 39, 8, 26, 22, 26, GrassLight);
                c.Speckle(6, 10, 18, 24, PixelCanvas.Shade(Grass, -0.25f), 0.06f);
                c.Outline(Outline);
                list.Add(Write(c, EnvDir + "/PROP_Tree.png"));
            }

            // Bush.
            {
                var c = new PixelCanvas(24, 18, 541);
                c.FillEllipse(8, 7, 6f, 5f, Grass);
                c.FillEllipse(15, 8, 7f, 6f, Grass);
                c.FillEllipse(12, 11, 6f, 4f, GrassLight);
                c.Speckle(3, 3, 18, 12, GrassDark, 0.08f);
                c.Outline(Outline);
                list.Add(Write(c, EnvDir + "/PROP_Bush.png"));
            }

            // Rock.
            {
                var c = new PixelCanvas(22, 16, 542);
                var rock = PixelCanvas.Hex("#6B7480");
                var rockLight = PixelCanvas.Hex("#8A94A1");
                var rockDark = PixelCanvas.Hex("#4A525C");
                c.FillEllipse(11, 7, 8f, 5f, rock);
                c.FillEllipse(9, 9, 5f, 3f, rockLight);
                c.FillRect(3, 3, 16, 2, rockDark);
                c.Outline(Outline);
                list.Add(Write(c, EnvDir + "/PROP_Rock.png"));
            }

            // Crate.
            {
                var c = new PixelCanvas(26, 26, 543);
                c.FillRect(2, 2, 22, 22, Wood);
                c.RectOutline(2, 2, 22, 22, WoodDark);
                c.FillRect(2, 20, 22, 2, WoodLight);
                c.Line(3, 3, 22, 22, WoodDark);
                c.Line(22, 3, 3, 22, WoodDark);
                c.FillRect(2, 11, 22, 2, WoodDark);
                c.Outline(Outline);
                list.Add(Write(c, EnvDir + "/PROP_Crate.png"));
            }

            // Fence section.
            {
                var c = new PixelCanvas(Tile, 22, 544);
                for (int x = 2; x < Tile - 2; x += 7)
                {
                    c.FillRect(x, 2, 4, 16, Wood);
                    c.FillRect(x, 16, 4, 2, WoodLight);
                    c.Set(x + 1, 18, Wood);
                    c.Set(x + 2, 18, Wood);
                }
                c.FillRect(1, 12, Tile - 2, 2, WoodDark);
                c.FillRect(1, 6, Tile - 2, 2, WoodDark);
                c.Outline(Outline);
                list.Add(Write(c, EnvDir + "/PROP_Fence.png"));
            }
            return list;
        }

        /// <summary>Torch with three flame frames - animated by TorchFlicker at runtime.</summary>
        static List<string> TorchFrames()
        {
            var list = new List<string>();
            var flameCore = PixelCanvas.Hex("#FFF2A8");
            var flameMid = PixelCanvas.Hex("#FFB13A");
            var flameOuter = PixelCanvas.Hex("#F0592B");

            for (int frame = 0; frame < 3; frame++)
            {
                var c = new PixelCanvas(16, 30, 560 + frame);

                // Bracket and post.
                c.FillRect(6, 2, 4, 12, WoodDark);
                c.FillRect(6, 12, 4, 2, Wood);
                c.FillRect(4, 13, 8, 3, PixelCanvas.Hex("#5A626E"));

                // Flame: same silhouette, jittered per frame so it reads as flicker.
                int lift = frame;
                int wobble = frame == 1 ? 1 : (frame == 2 ? -1 : 0);

                c.FillEllipse(8 + wobble, 20 + lift, 4f, 6f, flameOuter);
                c.FillEllipse(8 + wobble, 20 + lift, 2.6f, 4.4f, flameMid);
                c.FillEllipse(8 + wobble, 19 + lift, 1.4f, 2.6f, flameCore);
                c.Set(8 + wobble, 26 + lift, flameMid);

                c.Outline(Outline);
                list.Add(Write(c, EnvDir + "/PROP_Torch_" + frame + ".png"));
            }
            return list;
        }

        // --- UI ----------------------------------------------------------------

        /// <summary>
        /// Ornate 9-sliced frames. The border is returned alongside the path so the importer can
        /// set it - without a border these stretch into mush at panel size.
        /// </summary>
        static List<KeyValuePair<string, Vector4>> UiFrames()
        {
            var list = new List<KeyValuePair<string, Vector4>>();

            var arcaneDeep = PixelCanvas.Hex("#231B3E");
            var arcaneMid = PixelCanvas.Hex("#33275A");
            var goldRim = PixelCanvas.Hex("#D9B55C");
            var goldLight = PixelCanvas.Hex("#F5E3A8");
            var goldDark = PixelCanvas.Hex("#8A6C2A");

            // Upgrade card frame: dark arcane fill, gold rim, corner studs.
            {
                const int s = 32;
                var c = new PixelCanvas(s, s, 600);
                c.FillRect(0, 0, s, s, arcaneDeep);
                c.FillRect(3, 3, s - 6, s - 6, arcaneMid);
                c.FillRect(4, 4, s - 8, s - 8, arcaneDeep);

                c.RectOutline(0, 0, s, s, goldDark);
                c.RectOutline(1, 1, s - 2, s - 2, goldRim);
                c.RectOutline(2, 2, s - 4, s - 4, goldDark);

                // Corner studs.
                foreach (var p in new[] { new Vector2Int(2, 2), new Vector2Int(s - 6, 2),
                                          new Vector2Int(2, s - 6), new Vector2Int(s - 6, s - 6) })
                {
                    c.FillRect(p.x, p.y, 4, 4, goldRim);
                    c.FillRect(p.x + 1, p.y + 1, 2, 2, goldLight);
                }

                list.Add(new KeyValuePair<string, Vector4>(
                    Write(c, UiDir + "/UI_CardFrame.png"), new Vector4(8, 8, 8, 8)));
            }

            // Plain panel - HUD backings and the pause sheet.
            {
                const int s = 24;
                var c = new PixelCanvas(s, s, 601);
                c.FillRect(0, 0, s, s, new Color32(arcaneDeep.r, arcaneDeep.g, arcaneDeep.b, 225));
                c.RectOutline(0, 0, s, s, goldDark);
                c.RectOutline(1, 1, s - 2, s - 2, PixelCanvas.Hex("#4A3C74"));
                list.Add(new KeyValuePair<string, Vector4>(
                    Write(c, UiDir + "/UI_Panel.png"), new Vector4(6, 6, 6, 6)));
            }

            // Bar frame - health/XP tracks get a metal surround.
            {
                const int w = 16, h = 12;
                var c = new PixelCanvas(w, h, 602);
                c.FillRect(0, 0, w, h, new Color32(20, 16, 30, 235));
                c.RectOutline(0, 0, w, h, goldDark);
                c.FillRect(1, h - 2, w - 2, 1, PixelCanvas.Hex("#4A3C74"));
                list.Add(new KeyValuePair<string, Vector4>(
                    Write(c, UiDir + "/UI_BarFrame.png"), new Vector4(4, 4, 4, 4)));
            }

            // Round button - pause, and the mobile stick base.
            {
                const int s = 32;
                var c = new PixelCanvas(s, s, 603);
                c.FillCircle(16, 16, 15f, new Color32(arcaneDeep.r, arcaneDeep.g, arcaneDeep.b, 210));
                c.FillCircle(16, 16, 15f, new Color32(0, 0, 0, 0));
                c.FillCircle(16, 16, 15f, new Color32(arcaneDeep.r, arcaneDeep.g, arcaneDeep.b, 210));
                c.FillCircle(16, 16, 12f, new Color32(arcaneMid.r, arcaneMid.g, arcaneMid.b, 190));
                // Gold rim.
                for (int i = 0; i < 360; i++)
                {
                    float a = i * Mathf.Deg2Rad;
                    c.Set(16 + Mathf.RoundToInt(Mathf.Cos(a) * 15f),
                          16 + Mathf.RoundToInt(Mathf.Sin(a) * 15f), goldRim);
                }
                list.Add(new KeyValuePair<string, Vector4>(
                    Write(c, UiDir + "/UI_RoundButton.png"), Vector4.zero));
            }

            // Rarity gem, used as the upgrade-card icon backing.
            {
                var c = new PixelCanvas(16, 16, 604);
                c.FillTriangle(8, 15, 1, 8, 15, 8, Color.white);
                c.FillTriangle(8, 1, 1, 8, 15, 8, Color.white);
                list.Add(new KeyValuePair<string, Vector4>(
                    Write(c, UiDir + "/UI_Gem.png"), Vector4.zero));
            }

            return list;
        }

        static string Write(PixelCanvas canvas, string path)
        {
            var tex = canvas.ToTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }
    }
}
