using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Creates one sprite atlas per art folder.
    ///
    /// Pixel art is the awkward case for atlasing: the pack must not rotate or tight-pack sprites,
    /// needs real padding so neighbours cannot bleed across a sprite edge, and must stay Point
    /// filtered and uncompressed - block compression would smear a 16-colour sprite into mud.
    /// </summary>
    public static class SpriteAtlasBuilder
    {
        const string AtlasDir = "Assets/Art/Atlases";

        struct AtlasSpec
        {
            public string name;
            public string folder;
            public int maxSize;
        }

        static readonly AtlasSpec[] Specs =
        {
            new AtlasSpec { name = "ATLAS_Characters", folder = "Assets/Art/Characters", maxSize = 1024 },
            new AtlasSpec { name = "ATLAS_Environment", folder = "Assets/Art/Environment", maxSize = 1024 },
            new AtlasSpec { name = "ATLAS_Weapons", folder = "Assets/Art/Weapons", maxSize = 512 },
            new AtlasSpec { name = "ATLAS_FX", folder = "Assets/Art/FX", maxSize = 1024 },
            new AtlasSpec { name = "ATLAS_UI", folder = "Assets/Art/UI", maxSize = 512 },
        };

        [MenuItem("Tools/Zombie Shooter/Build Sprite Atlases")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(AtlasDir);

            // Atlases only actually pack when the project's packer is enabled.
            EnableSpritePacker();

            var built = new List<string>();

            foreach (var spec in Specs)
            {
                if (!Directory.Exists(spec.folder))
                {
                    Debug.LogWarning("[SpriteAtlasBuilder] missing folder " + spec.folder);
                    continue;
                }

                string path = AtlasDir + "/" + spec.name + ".spriteatlas";
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas == null)
                {
                    atlas = new SpriteAtlas();
                    AssetDatabase.CreateAsset(atlas, path);
                }

                // No rotation, no tight packing: both break pixel-perfect sampling. Padding 4
                // leaves room so a neighbouring sprite can never bleed in at a UV edge.
                var packing = new SpriteAtlasPackingSettings
                {
                    enableRotation = false,
                    enableTightPacking = false,
                    padding = 4,
                    blockOffset = 1,
                };
                atlas.SetPackingSettings(packing);

                var texture = new SpriteAtlasTextureSettings
                {
                    filterMode = FilterMode.Point,
                    generateMipMaps = false,
                    sRGB = true,
                    readable = false,
                };
                atlas.SetTextureSettings(texture);

                // Uncompressed everywhere. These atlases are small, and ETC2/ASTC would visibly
                // chew up flat pixel colours.
                var platform = new TextureImporterPlatformSettings
                {
                    name = "DefaultTexturePlatform",
                    maxTextureSize = spec.maxSize,
                    format = TextureImporterFormat.RGBA32,
                    textureCompression = TextureImporterCompression.Uncompressed,
                    overridden = true,
                };
                atlas.SetPlatformSettings(platform);

                var android = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    maxTextureSize = spec.maxSize,
                    format = TextureImporterFormat.RGBA32,
                    textureCompression = TextureImporterCompression.Uncompressed,
                    overridden = true,
                };
                atlas.SetPlatformSettings(android);

                // Pack the folder itself, so newly generated sprites are picked up automatically.
                var folder = AssetDatabase.LoadAssetAtPath<Object>(spec.folder);
                SpriteAtlasExtensions.Remove(atlas, SpriteAtlasExtensions.GetPackables(atlas));
                SpriteAtlasExtensions.Add(atlas, new[] { folder });

                EditorUtility.SetDirty(atlas);
                built.Add(spec.name);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);

            Debug.Log("[SpriteAtlasBuilder] built " + built.Count + " atlases: " + string.Join(", ", built));
        }

        /// <summary>Turns the project's sprite packer on if it is disabled.</summary>
        static void EnableSpritePacker()
        {
            // The enum gained/renamed members across versions, so resolve by name rather than
            // hard-coding a value that may not exist in this Editor.
            var names = System.Enum.GetNames(typeof(SpritePackerMode));
            foreach (var preferred in new[] { "SpriteAtlasV2", "AlwaysOnAtlas", "BuildTimeOnlyAtlas" })
            {
                foreach (var n in names)
                {
                    if (n != preferred) continue;
                    EditorSettings.spritePackerMode = (SpritePackerMode)System.Enum.Parse(typeof(SpritePackerMode), n);
                    Debug.Log("[SpriteAtlasBuilder] sprite packer mode = " + n);
                    return;
                }
            }
            Debug.LogWarning("[SpriteAtlasBuilder] could not resolve a sprite packer mode; modes: " + string.Join(", ", names));
        }

        [MenuItem("Tools/Zombie Shooter/Report Atlas Contents")]
        public static void Report()
        {
            foreach (var spec in Specs)
            {
                string path = AtlasDir + "/" + spec.name + ".spriteatlas";
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas == null) { Debug.Log(spec.name + ": not built"); continue; }
                Debug.Log(spec.name + ": " + atlas.spriteCount + " sprites packed");
            }
        }
    }
}
