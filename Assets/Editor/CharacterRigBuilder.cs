using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieShooter.EditorTools
{
    /// <summary>
    /// Assembles the standard character rig under a root GameObject. Kept as an Editor tool rather
    /// than hand-built prefabs so the whole cast can be rebuilt after an art or proportion change.
    ///
    /// Layout is authored at sprite scale and the whole rig is then scaled down, which keeps the
    /// offset numbers legible instead of a pile of 0.03s.
    /// </summary>
    public static class CharacterRigBuilder
    {
        public const string FlashMaterialPath = "Assets/Art/Shaders/M_SpriteFlash.mat";

        // Sorting order inside the rig's SortingGroup.
        const int OrderShadow = -10;
        const int OrderLegBack = 0;
        const int OrderLegFront = 1;
        const int OrderArmBack = 2;
        const int OrderTorso = 3;
        const int OrderHead = 5;
        const int OrderArmFront = 6;
        const int OrderWeapon = 7;

        public static Material GetFlashMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (mat != null) return mat;

            var shader = Shader.Find("ZombieShooter/SpriteFlash");
            if (shader == null)
            {
                Debug.LogError("[CharacterRigBuilder] SpriteFlash shader not found.");
                return null;
            }

            System.IO.Directory.CreateDirectory("Assets/Art/Shaders");
            mat = new Material(shader) { name = "M_SpriteFlash" };
            AssetDatabase.CreateAsset(mat, FlashMaterialPath);
            return mat;
        }

        /// <summary>Builds the rig under <paramref name="root"/> and returns the CharacterRig.</summary>
        public static CharacterRig Build(GameObject root, CharacterPartSet parts, float rigScale, bool includeWeapon)
        {
            // Wipe any previous rig so this is safe to re-run.
            var existing = root.transform.Find("Rig");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var flashMat = GetFlashMaterial();

            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(root.transform, false);
            rigGo.transform.localScale = Vector3.one * rigScale;

            var sortingGroup = rigGo.AddComponent<SortingGroup>();
            sortingGroup.sortingOrder = 0;

            var rig = rigGo.AddComponent<CharacterRig>();
            rigGo.AddComponent<CharacterAnimator>();

            rig.root = rigGo.transform;

            // Shadow sits on the ground plane and does not flash, so it keeps the default material.
            var shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PixelArtGenerator.CharacterDir + "/CHR_Shadow.png");
            var shadow = MakeSprite(rigGo.transform, "Shadow", shadowSprite, new Vector3(0f, -0.54f, 0f), OrderShadow, null);
            shadow.color = new Color(1f, 1f, 1f, 1f);
            rig.shadow = shadow.transform;
            rig.shadowRenderer = shadow;

            // Joint layout for the 32-PPU pixel parts. Limbs hang from a pivot at the joint so a
            // step reads as a hip swing rather than the sprite spinning about its own middle.
            rig.legBack = MakeJoint(rigGo.transform, "LegBack", new Vector3(-0.09f, -0.20f, 0f));
            rig.legBackRenderer = MakeSprite(rig.legBack, "Sprite", parts.leg, new Vector3(0f, -0.16f, 0f), OrderLegBack, flashMat);

            rig.legFront = MakeJoint(rigGo.transform, "LegFront", new Vector3(0.09f, -0.20f, 0f));
            rig.legFrontRenderer = MakeSprite(rig.legFront, "Sprite", parts.leg, new Vector3(0f, -0.16f, 0f), OrderLegFront, flashMat);

            rig.armBack = MakeJoint(rigGo.transform, "ArmBack", new Vector3(-0.26f, 0.16f, 0f));
            rig.armBackRenderer = MakeSprite(rig.armBack, "Sprite", parts.arm, new Vector3(0f, -0.18f, 0f), OrderArmBack, flashMat);

            var torso = MakeJoint(rigGo.transform, "Torso", new Vector3(0f, 0f, 0f));
            rig.torso = torso;
            rig.torsoRenderer = MakeSprite(torso, "Sprite", parts.torso, Vector3.zero, OrderTorso, flashMat);

            var head = MakeJoint(rigGo.transform, "Head", new Vector3(0f, 0.42f, 0f));
            rig.head = head;
            rig.headRenderer = MakeSprite(head, "Sprite", parts.head, Vector3.zero, OrderHead, flashMat);

            rig.armFront = MakeJoint(rigGo.transform, "ArmFront", new Vector3(0.26f, 0.16f, 0f));
            rig.armFrontRenderer = MakeSprite(rig.armFront, "Sprite", parts.arm, new Vector3(0f, -0.18f, 0f), OrderArmFront, flashMat);

            if (includeWeapon)
            {
                rig.weaponPivot = MakeJoint(rigGo.transform, "WeaponPivot", new Vector3(0.14f, 0.04f, 0f));
                rig.weaponRenderer = MakeSprite(rig.weaponPivot, "Weapon", null, new Vector3(0.24f, 0f, 0f), OrderWeapon, null);
                rig.weaponRenderer.enabled = false;
            }

            return rig;
        }

        static Transform MakeJoint(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Vector3 localPosition,
                                         int sortingOrder, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        /// <summary>Loads the generated part set for an archetype id, e.g. "walker".</summary>
        public static CharacterPartSet LoadParts(string archetypeId)
        {
            string dir = PixelArtGenerator.CharacterDir;
            return new CharacterPartSet
            {
                head = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/CHR_" + archetypeId + "_Head.png"),
                torso = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/CHR_" + archetypeId + "_Torso.png"),
                arm = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/CHR_" + archetypeId + "_Arm.png"),
                leg = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/CHR_" + archetypeId + "_Leg.png"),
            };
        }
    }
}
