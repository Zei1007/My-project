using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Generates the pixel-art cast: character body parts per archetype, weapons, FX and pickups.
    ///
    /// Everything is authored at 32 pixels per unit with point filtering - the Soul-Knight-style
    /// look depends on hard pixel edges, so no sprite here is ever filtered or compressed.
    /// Files are named by archetype, so real pixel art can overwrite them in place.
    /// </summary>
    public static class PixelArtGenerator
    {
        public const string CharacterDir = "Assets/Art/Characters";
        public const string WeaponDir = "Assets/Art/Weapons";
        public const string FxDir = "Assets/Art/FX";

        public const float PixelsPerUnit = 32f;

        public static readonly Color32 Outline = PixelCanvas.Hex("#181321");

        public struct Palette
        {
            public Color32 skin, skinShade, hair, shirt, shirtShade, pants, boots, eye;
            public bool glowEyes;
        }

        public static Dictionary<string, Palette> Palettes()
        {
            var map = new Dictionary<string, Palette>();

            map["player"] = new Palette
            {
                skin = PixelCanvas.Hex("#F2C08A"), skinShade = PixelCanvas.Hex("#C9945F"),
                hair = PixelCanvas.Hex("#453C63"), shirt = PixelCanvas.Hex("#4FA36B"),
                shirtShade = PixelCanvas.Hex("#357A4C"), pants = PixelCanvas.Hex("#3B5F85"),
                boots = PixelCanvas.Hex("#2B3142"), eye = PixelCanvas.Hex("#221E30"),
            };
            map["walker"] = new Palette
            {
                skin = PixelCanvas.Hex("#7FA05E"), skinShade = PixelCanvas.Hex("#5A7742"),
                hair = PixelCanvas.Hex("#3C4A33"), shirt = PixelCanvas.Hex("#4E5C46"),
                shirtShade = PixelCanvas.Hex("#3A4534"), pants = PixelCanvas.Hex("#39432F"),
                boots = PixelCanvas.Hex("#242A1D"), eye = PixelCanvas.Hex("#D8FF7A"), glowEyes = true,
            };
            map["runner"] = new Palette
            {
                skin = PixelCanvas.Hex("#C7B15A"), skinShade = PixelCanvas.Hex("#9A8640"),
                hair = PixelCanvas.Hex("#6A5A28"), shirt = PixelCanvas.Hex("#8A7736"),
                shirtShade = PixelCanvas.Hex("#6A5A28"), pants = PixelCanvas.Hex("#57491F"),
                boots = PixelCanvas.Hex("#2E2714"), eye = PixelCanvas.Hex("#FFE066"), glowEyes = true,
            };
            map["brute"] = new Palette
            {
                skin = PixelCanvas.Hex("#78909E"), skinShade = PixelCanvas.Hex("#556975"),
                hair = PixelCanvas.Hex("#3A4750"), shirt = PixelCanvas.Hex("#46545F"),
                shirtShade = PixelCanvas.Hex("#333E47"), pants = PixelCanvas.Hex("#333E47"),
                boots = PixelCanvas.Hex("#1F272D"), eye = PixelCanvas.Hex("#9EE8FF"), glowEyes = true,
            };
            map["elite_charger"] = new Palette
            {
                skin = PixelCanvas.Hex("#E8C24B"), skinShade = PixelCanvas.Hex("#B8922C"),
                hair = PixelCanvas.Hex("#8A6A14"), shirt = PixelCanvas.Hex("#C0951F"),
                shirtShade = PixelCanvas.Hex("#8A6A14"), pants = PixelCanvas.Hex("#7A5C10"),
                boots = PixelCanvas.Hex("#3E2E08"), eye = PixelCanvas.Hex("#FFF4B0"), glowEyes = true,
            };
            map["bloater"] = new Palette
            {
                skin = PixelCanvas.Hex("#E07A50"), skinShade = PixelCanvas.Hex("#B05835"),
                hair = PixelCanvas.Hex("#8A4526"), shirt = PixelCanvas.Hex("#B05835"),
                shirtShade = PixelCanvas.Hex("#8A4526"), pants = PixelCanvas.Hex("#7A3C20"),
                boots = PixelCanvas.Hex("#3A1C10"), eye = PixelCanvas.Hex("#FFD08A"), glowEyes = true,
            };
            map["miniboss"] = new Palette
            {
                skin = PixelCanvas.Hex("#B83A4E"), skinShade = PixelCanvas.Hex("#8A2638"),
                hair = PixelCanvas.Hex("#651A28"), shirt = PixelCanvas.Hex("#8A2638"),
                shirtShade = PixelCanvas.Hex("#651A28"), pants = PixelCanvas.Hex("#551420"),
                boots = PixelCanvas.Hex("#2A0A10"), eye = PixelCanvas.Hex("#FFE8B0"), glowEyes = true,
            };
            map["spitter"] = new Palette
            {
                skin = PixelCanvas.Hex("#A6C85A"), skinShade = PixelCanvas.Hex("#7C9A3C"),
                hair = PixelCanvas.Hex("#4E3A5C"), shirt = PixelCanvas.Hex("#5E4F78"),
                shirtShade = PixelCanvas.Hex("#43385A"), pants = PixelCanvas.Hex("#3A3050"),
                boots = PixelCanvas.Hex("#1E1A2A"), eye = PixelCanvas.Hex("#FF6A4D"), glowEyes = true,
            };
            map["boss"] = new Palette
            {
                skin = PixelCanvas.Hex("#9558C4"), skinShade = PixelCanvas.Hex("#6E3B96"),
                hair = PixelCanvas.Hex("#3D1E58"), shirt = PixelCanvas.Hex("#5E2E85"),
                shirtShade = PixelCanvas.Hex("#3D1E58"), pants = PixelCanvas.Hex("#341A4C"),
                boots = PixelCanvas.Hex("#1C0C28"), eye = PixelCanvas.Hex("#8CFFE4"), glowEyes = true,
            };
            return map;
        }

        [MenuItem("Tools/Zombie Shooter/Generate Pixel Art (Characters + FX)")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(CharacterDir);
            Directory.CreateDirectory(WeaponDir);
            Directory.CreateDirectory(FxDir);

            var written = new List<string>();
            int seed = 1;

            foreach (var kv in Palettes())
            {
                written.Add(Head(kv.Key, kv.Value, seed++));
                written.Add(Torso(kv.Key, kv.Value, seed++));
                written.Add(Arm(kv.Key, kv.Value, seed++));
                written.Add(Leg(kv.Key, kv.Value, seed++));
            }

            written.Add(Shadow());
            written.AddRange(Weapons());
            written.AddRange(Fx());
            written.AddRange(Pickups());
            written.AddRange(Projectiles());
            written.AddRange(LootIcons());

            AssetDatabase.Refresh();
            foreach (var path in written) PixelImport.Apply(path, PixelsPerUnit);
            AssetDatabase.SaveAssets();

            Debug.Log("[PixelArtGenerator] wrote " + written.Count + " pixel sprites.");
        }

        // --- characters --------------------------------------------------------

        /// <summary>18x16 head with hair, eyes and a 1px outline (canvas padded for the outline).</summary>
        static string Head(string id, Palette p, int seed)
        {
            var c = new PixelCanvas(20, 20, seed);

            // Skull: a rounded block, 1px inset at the corners.
            c.FillRect(2, 3, 16, 12, p.skin);
            c.SetRaw(2, 3, new Color32(0, 0, 0, 0)); c.SetRaw(17, 3, new Color32(0, 0, 0, 0));
            c.SetRaw(2, 14, new Color32(0, 0, 0, 0)); c.SetRaw(17, 14, new Color32(0, 0, 0, 0));

            // Jaw shading.
            c.FillRect(3, 3, 14, 2, p.skinShade);

            // Hair cap over the top third.
            c.FillRect(2, 11, 16, 4, p.hair);
            c.SetRaw(2, 14, new Color32(0, 0, 0, 0)); c.SetRaw(17, 14, new Color32(0, 0, 0, 0));
            c.FillRect(3, 10, 3, 1, p.hair);
            c.FillRect(14, 10, 3, 1, p.hair);

            // Eyes: 2x2, glowing for the undead.
            c.FillRect(6, 7, 2, 3, p.eye);
            c.FillRect(12, 7, 2, 3, p.eye);
            if (p.glowEyes)
            {
                c.Set(6, 9, PixelCanvas.Shade(p.eye, 0.5f));
                c.Set(12, 9, PixelCanvas.Shade(p.eye, 0.5f));
            }

            c.Outline(Outline);
            return Write(c, CharacterDir + "/CHR_" + id + "_Head.png");
        }

        static string Torso(string id, Palette p, int seed)
        {
            var c = new PixelCanvas(18, 16, seed);

            c.FillRect(2, 2, 14, 11, p.shirt);
            c.SetRaw(2, 2, new Color32(0, 0, 0, 0)); c.SetRaw(15, 2, new Color32(0, 0, 0, 0));

            // Shoulders catch the light, waist falls into shadow.
            c.FillRect(3, 11, 12, 2, PixelCanvas.Shade(p.shirt, 0.18f));
            c.FillRect(3, 2, 12, 2, p.shirtShade);

            // Belt.
            c.FillRect(2, 4, 14, 2, p.pants);

            c.Outline(Outline);
            return Write(c, CharacterDir + "/CHR_" + id + "_Torso.png");
        }

        static string Arm(string id, Palette p, int seed)
        {
            var c = new PixelCanvas(9, 14, seed);

            c.FillRect(2, 4, 5, 8, p.shirt);                 // sleeve
            c.FillRect(2, 10, 5, 2, PixelCanvas.Shade(p.shirt, 0.15f));
            c.FillRect(2, 2, 5, 3, p.skin);                  // hand
            c.FillRect(2, 2, 5, 1, p.skinShade);

            c.Outline(Outline);
            return Write(c, CharacterDir + "/CHR_" + id + "_Arm.png");
        }

        static string Leg(string id, Palette p, int seed)
        {
            var c = new PixelCanvas(9, 13, seed);

            c.FillRect(2, 4, 5, 7, p.pants);
            c.FillRect(2, 2, 5, 3, p.boots);                 // boot
            c.FillRect(2, 2, 6, 1, p.boots);                 // toe sticks out

            c.Outline(Outline);
            return Write(c, CharacterDir + "/CHR_" + id + "_Leg.png");
        }

        static string Shadow()
        {
            var c = new PixelCanvas(20, 8, 99);
            c.FillEllipse(10, 4, 8f, 3f, new Color32(0, 0, 0, 90));
            return Write(c, CharacterDir + "/CHR_Shadow.png");
        }

        // --- weapons -----------------------------------------------------------

        static List<string> Weapons()
        {
            var steel = PixelCanvas.Hex("#6E7A8A");
            var steelLight = PixelCanvas.Hex("#9AA6B5");
            var steelDark = PixelCanvas.Hex("#454F5C");
            var wood = PixelCanvas.Hex("#8A5A34");
            var woodDark = PixelCanvas.Hex("#5F3C22");
            var blade = PixelCanvas.Hex("#C8D6E8");
            var gold = PixelCanvas.Hex("#E8C24B");

            var list = new List<string>();

            // Pistol
            {
                var c = new PixelCanvas(16, 12, 201);
                c.FillRect(2, 6, 11, 3, steel);
                c.FillRect(2, 8, 9, 1, steelLight);
                c.FillRect(3, 2, 3, 4, woodDark);      // grip
                c.FillRect(11, 6, 2, 2, steelDark);    // muzzle
                c.Outline(Outline);
                list.Add(Write(c, WeaponDir + "/WPN_Pistol.png"));
            }
            // SMG
            {
                var c = new PixelCanvas(22, 12, 202);
                c.FillRect(2, 6, 17, 3, steel);
                c.FillRect(2, 8, 15, 1, steelLight);
                c.FillRect(5, 2, 3, 4, steelDark);     // grip
                c.FillRect(9, 3, 2, 3, steelDark);     // magazine
                c.FillRect(17, 6, 3, 2, steelDark);    // barrel
                c.Outline(Outline);
                list.Add(Write(c, WeaponDir + "/WPN_SMG.png"));
            }
            // Shotgun
            {
                var c = new PixelCanvas(26, 11, 203);
                c.FillRect(2, 5, 21, 3, steel);
                c.FillRect(2, 7, 19, 1, steelLight);
                c.FillRect(2, 2, 6, 3, wood);          // stock
                c.FillRect(12, 4, 7, 1, woodDark);     // pump
                c.Outline(Outline);
                list.Add(Write(c, WeaponDir + "/WPN_Shotgun.png"));
            }
            // Rifle - long barrel and a scope, so "long range" reads at a glance.
            {
                var c = new PixelCanvas(32, 12, 205);
                c.FillRect(2, 5, 27, 3, steel);
                c.FillRect(2, 7, 25, 1, steelLight);
                c.FillRect(2, 2, 7, 4, wood);          // stock
                c.FillRect(12, 8, 7, 2, steelDark);    // scope
                c.FillRect(13, 10, 5, 1, steelLight);
                c.FillRect(26, 5, 4, 2, steelDark);    // muzzle
                c.Outline(Outline);
                list.Add(Write(c, WeaponDir + "/WPN_Rifle.png"));
            }
            // Machete
            {
                var c = new PixelCanvas(20, 10, 204);
                c.FillRect(6, 4, 12, 3, blade);
                c.FillRect(6, 6, 12, 1, PixelCanvas.Shade(blade, 0.25f));
                c.FillRect(2, 4, 4, 3, woodDark);      // handle
                c.FillRect(5, 3, 2, 5, gold);          // guard
                c.Outline(Outline);
                list.Add(Write(c, WeaponDir + "/WPN_Machete.png"));
            }
            return list;
        }

        // --- fx ----------------------------------------------------------------

        static List<string> Fx()
        {
            var list = new List<string>();

            // Muzzle flash: a chunky pixel star.
            {
                var c = new PixelCanvas(16, 16, 301);
                var core = PixelCanvas.Hex("#FFF6C8");
                var mid = PixelCanvas.Hex("#FFD24A");
                c.FillRect(2, 7, 12, 2, mid);
                c.FillRect(7, 2, 2, 12, mid);
                c.FillRect(5, 5, 6, 6, mid);
                c.FillRect(6, 6, 4, 4, core);
                list.Add(Write(c, FxDir + "/SPR_Muzzle.png"));
            }

            // Hollow ring for telegraphs.
            {
                var c = new PixelCanvas(64, 64, 302);
                c.FillCircle(32, 32, 31f, Color.white);
                c.FillCircle(32, 32, 27f, new Color32(0, 0, 0, 0));
                list.Add(Write(c, FxDir + "/SPR_Ring.png"));
            }

            // Solid disc for AoE fills and glows.
            {
                var c = new PixelCanvas(64, 64, 303);
                c.FillCircle(32, 32, 31f, Color.white);
                list.Add(Write(c, FxDir + "/SPR_Disc128.png"));
            }

            // Soft radial glow - used behind orbs and under torches.
            {
                const int size = 64;
                var c = new PixelCanvas(size, size, 304);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f));
                        float a = Mathf.Clamp01(1f - d / (size / 2f));
                        a = a * a;
                        if (a > 0.004f) c.SetRaw(x, y, new Color32(255, 255, 255, (byte)(a * 255)));
                    }
                }
                list.Add(Write(c, FxDir + "/SPR_Glow.png"));
            }

            // 1x1 white - stretched into beams and bars.
            {
                var c = new PixelCanvas(4, 4, 305);
                c.FillRect(0, 0, 4, 4, Color.white);
                list.Add(Write(c, FxDir + "/SPR_Square.png"));
            }

            // Slash arc for the melee weapon.
            {
                var c = new PixelCanvas(32, 32, 306);
                for (int i = 0; i < 360; i++)
                {
                    float a = i * Mathf.Deg2Rad;
                    for (float r = 12f; r < 15f; r += 0.5f)
                    {
                        int x = 16 + Mathf.RoundToInt(Mathf.Cos(a) * r);
                        int y = 16 + Mathf.RoundToInt(Mathf.Sin(a) * r);
                        c.Set(x, y, Color.white);
                    }
                }
                list.Add(Write(c, FxDir + "/SPR_Slash.png"));
            }
            return list;
        }

        // --- pickups -----------------------------------------------------------

        static List<string> Pickups()
        {
            var list = new List<string>();

            // XP soul gem: a cut crystal, bright core, dark facet, rim light.
            {
                var c = new PixelCanvas(14, 18, 401);
                var deep = PixelCanvas.Hex("#1B7FA8");
                var mid = PixelCanvas.Hex("#3FC9F0");
                var light = PixelCanvas.Hex("#A9F1FF");

                // Diamond silhouette.
                c.FillTriangle(7, 16, 2, 9, 12, 9, mid);
                c.FillTriangle(7, 2, 2, 9, 12, 9, mid);
                // Left facets darker, right rim bright.
                c.FillTriangle(7, 15, 3, 9, 7, 9, deep);
                c.FillTriangle(7, 3, 3, 9, 7, 9, deep);
                c.FillRect(8, 8, 2, 3, light);
                c.Set(9, 11, light);

                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/SPR_XPGem.png"));
            }

            // Gold coin, for the meta-currency drop when it lands.
            {
                var c = new PixelCanvas(12, 12, 402);
                var gold = PixelCanvas.Hex("#E8C24B");
                var goldDark = PixelCanvas.Hex("#A8842A");
                var goldLight = PixelCanvas.Hex("#FFF0A8");
                c.FillCircle(6, 6, 4.4f, gold);
                c.FillCircle(6, 5, 3.6f, goldDark);
                c.FillCircle(6, 6, 3.2f, gold);
                c.FillRect(4, 7, 2, 2, goldLight);
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/SPR_Coin.png"));
            }
            return list;
        }

        // --- projectiles --------------------------------------------------------

        /// <summary>
        /// Bullets are drawn white with a dark outline so the weapon's tint colours them - one
        /// sprite serves every gun, and the outline keeps them readable against the teal floor.
        /// </summary>
        static List<string> Projectiles()
        {
            var list = new List<string>();
            var hot = PixelCanvas.Hex("#FFF8E0");

            // Bullet: a streak pointing along +X, which is the direction projectiles travel.
            {
                var c = new PixelCanvas(12, 6, 450);
                c.FillRect(2, 2, 7, 2, Color.white);
                c.FillRect(9, 2, 1, 2, hot);
                c.FillRect(2, 2, 2, 2, new Color32(255, 255, 255, 150));   // fading tail
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/SPR_Bullet.png"));
            }

            // Pellet: a round slug for the shotgun.
            {
                var c = new PixelCanvas(7, 7, 451);
                c.FillRect(2, 2, 3, 3, Color.white);
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/SPR_Pellet.png"));
            }

            // Enemy glob: bright core, softer ring. Tinted per mob (acid green, boss purple).
            {
                var c = new PixelCanvas(14, 14, 452);
                c.FillCircle(7, 7, 4.6f, new Color32(210, 210, 210, 255));
                c.FillCircle(7, 7, 3f, Color.white);
                c.Set(5, 9, hot);
                c.Set(6, 9, hot);
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/SPR_EnemyShot.png"));
            }
            return list;
        }

        // --- loot ----------------------------------------------------------------

        static List<string> LootIcons()
        {
            var list = new List<string>();

            // Health potion: round red flask, cork, glass highlight.
            {
                var c = new PixelCanvas(14, 18, 460);
                var red = PixelCanvas.Hex("#E0344A");
                var redDark = PixelCanvas.Hex("#9A1E30");
                c.FillCircle(7, 6, 4.6f, red);
                c.FillRect(3, 2, 8, 3, redDark);
                c.FillRect(5, 10, 4, 4, PixelCanvas.Hex("#C9D6E6"));   // neck
                c.FillRect(5, 14, 4, 2, PixelCanvas.Hex("#8A5A34"));   // cork
                c.FillRect(4, 7, 2, 2, PixelCanvas.Hex("#FFB0B8"));    // shine
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/LOOT_Potion.png"));
            }

            // Shield: blue heater with a silver rim.
            {
                var c = new PixelCanvas(16, 18, 461);
                var blue = PixelCanvas.Hex("#3F7FE0");
                var rim = PixelCanvas.Hex("#C9D6E6");
                c.FillRect(2, 7, 12, 9, rim);
                c.FillTriangle(8, 1, 2, 8, 13, 8, rim);
                c.FillRect(3, 8, 10, 7, blue);
                c.FillTriangle(8, 3, 3, 8, 12, 8, blue);
                c.FillRect(7, 4, 2, 11, PixelCanvas.Hex("#A9C8FF"));    // centre band
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/LOOT_Shield.png"));
            }

            // Blessing: a violet star with a white heart.
            {
                var c = new PixelCanvas(18, 18, 462);
                var violet = PixelCanvas.Hex("#B066FF");
                c.FillTriangle(9, 16, 5, 7, 13, 7, violet);
                c.FillTriangle(9, 3, 2, 11, 16, 11, violet);
                c.FillRect(7, 8, 4, 3, PixelCanvas.Hex("#F2E0FF"));
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/LOOT_Blessing.png"));
            }

            // Frenzy: yellow lightning bolt.
            {
                var c = new PixelCanvas(14, 18, 463);
                var gold = PixelCanvas.Hex("#FFD24A");
                c.FillTriangle(9, 16, 3, 8, 8, 8, gold);
                c.FillTriangle(5, 2, 6, 9, 11, 9, gold);
                c.FillRect(5, 8, 5, 2, gold);
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/LOOT_Frenzy.png"));
            }

            // Magnet: horseshoe, red body with steel tips.
            {
                var c = new PixelCanvas(16, 16, 464);
                var red = PixelCanvas.Hex("#D8343F");
                var steel = PixelCanvas.Hex("#C9D6E6");
                // Built solid rather than by punching holes - Set() ignores transparent pixels.
                c.FillRect(3, 2, 10, 3, red);          // curve at the bottom
                c.FillRect(3, 2, 3, 11, red);          // left arm
                c.FillRect(10, 2, 3, 11, red);         // right arm
                c.FillRect(3, 11, 3, 3, steel);        // tips
                c.FillRect(10, 11, 3, 3, steel);
                c.SetRaw(3, 2, new Color32(0, 0, 0, 0));
                c.SetRaw(12, 2, new Color32(0, 0, 0, 0));
                c.Outline(Outline);
                list.Add(Write(c, FxDir + "/LOOT_Magnet.png"));
            }
            return list;
        }

        // --- io ----------------------------------------------------------------

        static string Write(PixelCanvas canvas, string path)
        {
            var tex = canvas.ToTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }
    }

    /// <summary>Shared import settings. Pixel art must be Point-filtered and uncompressed.</summary>
    public static class PixelImport
    {
        public static void Apply(string path, float pixelsPerUnit, Vector4? border = null)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            if (border.HasValue)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = border.Value;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }

            importer.SaveAndReimport();
        }
    }
}
