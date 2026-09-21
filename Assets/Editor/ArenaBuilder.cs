using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Builds the dungeon arena: a tilemapped floor with varied slabs, a walled border, scattered
    /// props and torches.
    ///
    /// The floor is a Tilemap rather than hundreds of SpriteRenderers - a 32x20 arena is 640 cells,
    /// and Tilemap batches them into a handful of draw calls where loose sprites would not.
    /// </summary>
    public static class ArenaBuilder
    {
        public const string TileDir = "Assets/Art/Tiles";

        /// <summary>Arena half-extents in units (= tiles). Kept small enough to read on a phone.</summary>
        public static readonly Vector2Int HalfExtents = new Vector2Int(16, 10);

        [MenuItem("Tools/Zombie Shooter/Rebuild Arena")]
        public static void Rebuild()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            RemoveOld("Arena");
            RemoveOld("Arena_Floor");
            RemoveOld("Wall_Top");
            RemoveOld("Wall_Bottom");
            RemoveOld("Wall_Left");
            RemoveOld("Wall_Right");

            var floorTiles = LoadFloorTiles();
            var wallTile = LoadTile("TILE_Wall");

            var arena = new GameObject("Arena");

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(arena.transform, false);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            BuildFloor(gridGo.transform, floorTiles);
            BuildWalls(gridGo.transform, wallTile);
            BuildProps(arena.transform);
            BuildTorches(arena.transform);

            EditorSceneMarkDirty(scene);
            Debug.Log("[ArenaBuilder] arena rebuilt at " + (HalfExtents.x * 2) + "x" + (HalfExtents.y * 2) + " tiles.");
        }

        static void EditorSceneMarkDirty(UnityEngine.SceneManagement.Scene scene)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        static void RemoveOld(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        // --- tiles -------------------------------------------------------------

        /// <summary>Wraps each floor sprite in a Tile asset, creating them on first run.</summary>
        static List<TileBase> LoadFloorTiles()
        {
            var list = new List<TileBase>();
            for (int i = 0; i < 4; i++)
            {
                var tile = LoadTile("TILE_Floor_" + i);
                if (tile != null) list.Add(tile);
            }
            return list;
        }

        static Tile LoadTile(string spriteName)
        {
            Directory.CreateDirectory(TileDir);
            string tilePath = TileDir + "/" + spriteName + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existing != null) return existing;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                PixelEnvironmentGenerator.EnvDir + "/" + spriteName + ".png");
            if (sprite == null)
            {
                Debug.LogWarning("[ArenaBuilder] missing sprite " + spriteName);
                return null;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, tilePath);
            return tile;
        }

        // --- floor / walls -----------------------------------------------------

        static void BuildFloor(Transform parent, List<TileBase> tiles)
        {
            if (tiles.Count == 0) return;

            var go = new GameObject("Tilemap_Floor");
            go.transform.SetParent(parent, false);

            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = SortingBands.Floor;

            var random = new System.Random(4242);

            for (int x = -HalfExtents.x; x < HalfExtents.x; x++)
            {
                for (int y = -HalfExtents.y; y < HalfExtents.y; y++)
                {
                    // Mostly plain slabs, with the detailed variants sprinkled in - a uniform
                    // random mix reads as noise rather than as a floor.
                    int roll = random.Next(100);
                    int index = roll < 68 ? 0 : (roll < 80 ? 1 : (roll < 90 ? 2 : 3));
                    index = Mathf.Min(index, tiles.Count - 1);

                    map.SetTile(new Vector3Int(x, y, 0), tiles[index]);
                }
            }
        }

        static void BuildWalls(Transform parent, TileBase wallTile)
        {
            if (wallTile == null) return;

            var go = new GameObject("Tilemap_Walls");
            go.transform.SetParent(parent, false);

            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = SortingBands.Wall;

            // Two courses deep so the border reads as mass, not a painted line.
            for (int x = -HalfExtents.x - 2; x < HalfExtents.x + 2; x++)
            {
                for (int d = 0; d < 2; d++)
                {
                    map.SetTile(new Vector3Int(x, HalfExtents.y + d, 0), wallTile);
                    map.SetTile(new Vector3Int(x, -HalfExtents.y - 1 - d, 0), wallTile);
                }
            }
            for (int y = -HalfExtents.y - 2; y < HalfExtents.y + 2; y++)
            {
                for (int d = 0; d < 2; d++)
                {
                    map.SetTile(new Vector3Int(HalfExtents.x + d, y, 0), wallTile);
                    map.SetTile(new Vector3Int(-HalfExtents.x - 1 - d, y, 0), wallTile);
                }
            }

            // One collider for the whole border, so mobs cannot leave the room.
            var collider = go.AddComponent<TilemapCollider2D>();
            var composite = go.AddComponent<CompositeCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        // --- props -------------------------------------------------------------

        static void BuildProps(Transform parent)
        {
            var root = new GameObject("Props");
            root.transform.SetParent(parent, false);

            var trees = LoadSprite("PROP_Tree");
            var bush = LoadSprite("PROP_Bush");
            var rock = LoadSprite("PROP_Rock");
            var crate = LoadSprite("PROP_Crate");
            var fence = LoadSprite("PROP_Fence");

            var random = new System.Random(777);
            var taken = new List<Vector2>();

            // Keep the middle clear - that is where the player starts and where the boss lands.
            System.Func<Vector2, bool> tooClose = (p) =>
            {
                if (p.magnitude < 4.5f) return true;
                for (int i = 0; i < taken.Count; i++)
                    if (Vector2.Distance(taken[i], p) < 2.2f) return true;
                return false;
            };

            System.Action<Sprite, int, float, bool> scatter = (sprite, count, pivotOffset, solid) =>
            {
                if (sprite == null) return;
                for (int i = 0, guard = 0; i < count && guard < 400; guard++)
                {
                    var p = new Vector2(
                        random.Next(-HalfExtents.x + 2, HalfExtents.x - 1),
                        random.Next(-HalfExtents.y + 2, HalfExtents.y - 1));
                    if (tooClose(p)) continue;

                    taken.Add(p);
                    i++;

                    var go = new GameObject(sprite.name);
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(p.x + 0.5f, p.y + 0.5f, 0f);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;

                    var sorter = go.AddComponent<YSortSprite>();
                    SetPrivate(sorter, "pivotYOffset", pivotOffset);

                    if (solid)
                    {
                        var col = go.AddComponent<BoxCollider2D>();
                        col.size = new Vector2(0.7f, 0.5f);
                        col.offset = new Vector2(0f, -0.25f);
                    }
                }
            };

            scatter(trees, 14, -0.6f, true);
            scatter(bush, 12, -0.2f, false);
            scatter(rock, 10, -0.2f, false);
            scatter(crate, 8, -0.35f, true);
            scatter(fence, 5, -0.3f, true);
        }

        static void BuildTorches(Transform parent)
        {
            var root = new GameObject("Torches");
            root.transform.SetParent(parent, false);

            var frames = new Sprite[3];
            for (int i = 0; i < 3; i++) frames[i] = LoadSprite("PROP_Torch_" + i);
            if (frames[0] == null) return;

            var glow = AssetDatabase.LoadAssetAtPath<Sprite>(PixelArtGenerator.FxDir + "/SPR_Glow.png");

            // Along the top and bottom walls, evenly spaced.
            var positions = new List<Vector2>();
            for (int x = -HalfExtents.x + 3; x < HalfExtents.x - 1; x += 7)
            {
                positions.Add(new Vector2(x + 0.5f, HalfExtents.y - 0.4f));
                positions.Add(new Vector2(x + 0.5f, -HalfExtents.y + 0.6f));
            }
            for (int y = -HalfExtents.y + 4; y < HalfExtents.y - 2; y += 6)
            {
                positions.Add(new Vector2(-HalfExtents.x + 0.6f, y + 0.5f));
                positions.Add(new Vector2(HalfExtents.x - 0.6f, y + 0.5f));
            }

            foreach (var p in positions)
            {
                var go = new GameObject("Torch");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(p.x, p.y, 0f);

                var flameGo = new GameObject("Flame");
                flameGo.transform.SetParent(go.transform, false);
                var flame = flameGo.AddComponent<SpriteRenderer>();
                flame.sprite = frames[0];
                var sorter = flameGo.AddComponent<YSortSprite>();
                SetPrivate(sorter, "pivotYOffset", -0.4f);

                SpriteRenderer glowRenderer = null;
                if (glow != null)
                {
                    var glowGo = new GameObject("Glow");
                    glowGo.transform.SetParent(go.transform, false);
                    glowGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                    glowRenderer = glowGo.AddComponent<SpriteRenderer>();
                    glowRenderer.sprite = glow;
                    glowRenderer.sortingOrder = SortingBands.GroundDecal + 2;
                }

                var flicker = go.AddComponent<TorchFlicker>();
                SetPrivate(flicker, "flameRenderer", flame);
                SetPrivate(flicker, "glowRenderer", glowRenderer);
                SetPrivateArray(flicker, "frames", frames);
            }
        }

        static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(PixelEnvironmentGenerator.EnvDir + "/" + name + ".png");
        }

        static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;

            if (value is Object) prop.objectReferenceValue = (Object)value;
            else if (value is float) prop.floatValue = (float)value;
            else if (value is int) prop.intValue = (int)value;
            else if (value is bool) prop.boolValue = (bool)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPrivateArray(Object target, string field, Sprite[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
