using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lotba.CityRush.Editor
{
    /// <summary>Small original placeholder assets, authored once and saved as editable Unity assets.</summary>
    internal sealed class RushArt
    {
        internal const string Root = "Assets/_Game";
        internal Material Road, Pavement, White, Dark, Glass, Gold, Orange, Shirt, Skin, Bike, Shadow;
        internal Mesh WheelMesh;
        internal GameObject Coin;
        internal GameObject[] LowObstacles, Vehicles;
        private Material[] facades;
        private Material Leaves, Bark, Red, Teal;

        internal void Create()
        {
            foreach (string folder in new[] { Root, Root + "/Scenes", Root + "/Settings", Root + "/Art", Root + "/Art/Materials", Root + "/Art/Meshes", Root + "/Art/UI", Root + "/Prefabs" }) Folder(folder);
            Road = Material("Asphalt", "414550"); Pavement = Material("Pavement", "C8C4B4");
            White = Material("Warm white", "F1F0DF"); Dark = Material("Rubber and metal", "222F38");
            Glass = Material("Windows", "517584"); Gold = Material("Gold", "FFC43D"); Orange = Material("Cone orange", "F18A30");
            Shirt = Material("Blue hoodie", "526F83"); Skin = Material("Skin", "BF9172"); Bike = Material("Bicycle frame", "B7CAD0");
            Leaves = Material("Tree leaves", "7E9A55"); Bark = Material("Tree bark", "82715A");
            Red = Material("Red", "BC6551"); Teal = Material("Teal", "7D9D99");
            facades = new[] { Material("Facade sand", "D0B789"), Material("Facade teal", "8AA7A2"), Material("Facade cream", "C7C5B1"), Material("Facade brick", "B7836D") };
            Shadow = Material("Soft footprint", "101820");
            Shadow.color = new Color(.03f, .05f, .07f, .24f);
            Shadow.SetFloat("_Mode", 3); Shadow.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); Shadow.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            Shadow.SetInt("_ZWrite", 0); Shadow.EnableKeyword("_ALPHABLEND_ON"); Shadow.renderQueue = 3000;
            WheelMesh = MeshAsset("Bicycle tyre", Torus());
            Coin = CoinAsset();
            LowObstacles = new[] { ConeAsset(), CrateAsset() };
            Vehicles = new[] { CarAsset("Taxi", Gold, true), CarAsset("Red car", Red, false), CarAsset("Blue car", Teal, false) };
            CircleSprite();
        }
        internal static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); string parent = path.Substring(0, split);
            Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
        internal static Material Material(string name, string hex)
        {
            string path = Root + "/Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            material = new Material(Shader.Find("Standard")) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Glossiness", .08f); AssetDatabase.CreateAsset(material, path); return material;
        }
        internal GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return go;
        }
        internal GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return go;
        }
        internal void Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material)
        {
            var go = Primitive(name, PrimitiveType.Cylinder, parent, (a + b) * .5f, new Vector3(width, Vector3.Distance(a, b) * .5f, width), material);
            go.transform.up = (b - a).normalized;
        }
        private GameObject CoinAsset()
        {
            string path = Root + "/Prefabs/Coin.prefab";
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (saved != null) return saved;
            var go = new GameObject("Coin");
            var disc = Primitive("Coin face", PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(.66f, .055f, .66f), Gold);
            disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var centre = Primitive("Coin centre", PrimitiveType.Cylinder, go.transform, new Vector3(0, 0, -.065f), new Vector3(.38f, .012f, .38f), White);
            centre.transform.localRotation = Quaternion.Euler(90, 0, 0);
            return SavePrefab(go, path);
        }
        private GameObject ConeAsset()
        {
            string path = Root + "/Prefabs/Traffic cone.prefab";
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (saved != null) return saved;
            var go = new GameObject("Traffic cone");
            Primitive("Base", PrimitiveType.Cube, go.transform, new Vector3(0, .06f, 0), new Vector3(1.15f, .12f, 1), Dark);
            Mesh cone = MeshAsset("Traffic cone mesh", Cone(10));
            MeshObject("Cone", go.transform, cone, new Vector3(0, .12f, 0), new Vector3(.43f, 1.1f, .43f), Orange);
            Primitive("Reflective stripe", PrimitiveType.Cylinder, go.transform, new Vector3(0, .65f, 0), new Vector3(.45f, .065f, .45f), White);
            return SavePrefab(go, path);
        }
        private GameObject CrateAsset()
        {
            string path = Root + "/Prefabs/Road crate.prefab";
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (saved != null) return saved;
            var go = new GameObject("Road crate");
            Primitive("Crate", PrimitiveType.Cube, go.transform, new Vector3(0, .45f, 0), new Vector3(1.7f, .9f, .75f), Bark);
            Material plank = facades[0];
            foreach (float x in new[] { -.7f, .7f }) Primitive("Vertical brace", PrimitiveType.Cube, go.transform, new Vector3(x, .45f, -.4f), new Vector3(.12f, .9f, .05f), plank);
            foreach (float y in new[] { .09f, .82f }) Primitive("Horizontal brace", PrimitiveType.Cube, go.transform, new Vector3(0, y, -.4f), new Vector3(1.7f, .12f, .05f), plank);
            Beam("Diagonal brace", go.transform, new Vector3(-.68f, .15f, -.44f), new Vector3(.68f, .75f, -.44f), .12f, plank);
            return SavePrefab(go, path);
        }
        private GameObject CarAsset(string name, Material colour, bool taxi)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (saved != null) return saved;
            var go = new GameObject(name);
            Primitive("Body", PrimitiveType.Cube, go.transform, new Vector3(0, .5f, 0), new Vector3(1.9f, .62f, 3.65f), colour);
            Primitive("Cabin", PrimitiveType.Cube, go.transform, new Vector3(0, 1.02f, .12f), new Vector3(1.65f, .58f, 1.9f), colour);
            Primitive("Windscreen", PrimitiveType.Cube, go.transform, new Vector3(0, 1.05f, -.85f), new Vector3(1.43f, .42f, .025f), Glass);
            foreach (int side in new[] { -1, 1 })
            {
                Primitive("Side glass", PrimitiveType.Cube, go.transform, new Vector3(side * .835f, 1.06f, .1f), new Vector3(.025f, .38f, 1.6f), Glass);
                foreach (float z in new[] { -1.1f, 1.1f })
                {
                    var wheel = Primitive("Wheel", PrimitiveType.Cylinder, go.transform, new Vector3(side * .91f, .34f, z), new Vector3(.59f, .09f, .59f), Dark);
                    wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
                Primitive("Headlight", PrimitiveType.Cube, go.transform, new Vector3(side * .66f, .58f, -1.84f), new Vector3(.36f, .2f, .04f), White);
            }
            Primitive("Bumper", PrimitiveType.Cube, go.transform, new Vector3(0, .35f, -1.85f), new Vector3(1.75f, .15f, .09f), Dark);
            if (taxi) Primitive("Taxi roof sign", PrimitiveType.Cube, go.transform, new Vector3(0, 1.4f, .05f), new Vector3(.6f, .19f, .3f), White);
            Combine(go, name.Replace(" ", ""));
            return SavePrefab(go, path);
        }
        internal Transform Rider(Transform parent, out Transform[] wheels, out Transform[] pedals)
        {
            Transform root = new GameObject("Placeholder cyclist").transform; root.SetParent(parent, false);
            wheels = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                Transform pivot = new GameObject("Wheel").transform; pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(0, .48f, i == 0 ? -.73f : .73f);
                MeshObject("Tyre", pivot, WheelMesh, Vector3.zero, Vector3.one * .45f, Dark);
                for (int spoke = 0; spoke < 6; spoke++)
                {
                    float angle = spoke * Mathf.PI / 3;
                    Beam("Spoke", pivot, Vector3.zero, new Vector3(0, Mathf.Cos(angle) * .4f, Mathf.Sin(angle) * .4f), .018f, Bike);
                }
                wheels[i] = pivot;
            }
            Beam("Chain stay", root, new Vector3(0, .48f, -.73f), new Vector3(0, .5f, .05f), .07f, Bike);
            Beam("Seat stay", root, new Vector3(0, .48f, -.73f), new Vector3(0, 1.08f, -.25f), .07f, Bike);
            Beam("Top tube", root, new Vector3(0, 1.08f, -.25f), new Vector3(0, 1.08f, .53f), .07f, Bike);
            Beam("Down tube", root, new Vector3(0, 1.08f, .53f), new Vector3(0, .5f, .05f), .07f, Bike);
            Beam("Seat tube", root, new Vector3(0, .5f, .05f), new Vector3(0, 1.1f, -.25f), .07f, Bike);
            Beam("Fork", root, new Vector3(0, .48f, .73f), new Vector3(0, 1.18f, .5f), .07f, Bike);
            Primitive("Seat", PrimitiveType.Cube, root, new Vector3(0, 1.08f, -.25f), new Vector3(.34f, .1f, .4f), Dark);
            Primitive("Handlebars", PrimitiveType.Cube, root, new Vector3(0, 1.24f, .58f), new Vector3(.85f, .07f, .12f), Dark);
            var torso = Primitive("Hoodie", PrimitiveType.Capsule, root, new Vector3(0, 1.48f, -.03f), new Vector3(.55f, .4f, .44f), Shirt);
            torso.transform.localRotation = Quaternion.Euler(16, 0, 0);
            Primitive("Hood", PrimitiveType.Sphere, root, new Vector3(0, 1.76f, -.16f), new Vector3(.48f, .32f, .4f), Shirt);
            Primitive("Head", PrimitiveType.Sphere, root, new Vector3(0, 1.96f, .08f), new Vector3(.38f, .4f, .38f), Skin);
            Primitive("Helmet", PrimitiveType.Sphere, root, new Vector3(0, 2.1f, .07f), new Vector3(.48f, .3f, .5f), Dark);
            for (int vent = -1; vent <= 1; vent++) Primitive("Helmet vent", PrimitiveType.Cube, root, new Vector3(vent * .105f, 2.23f, .07f), new Vector3(.04f, .018f, .25f), Glass);
            pedals = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                Beam("Upper arm", root, new Vector3(side * .24f, 1.68f, .05f), new Vector3(side * .35f, 1.38f, .31f), .14f, Shirt);
                Beam("Forearm", root, new Vector3(side * .35f, 1.38f, .31f), new Vector3(side * .36f, 1.24f, .56f), .12f, Shirt);
                Primitive("Hand", PrimitiveType.Sphere, root, new Vector3(side * .36f, 1.24f, .56f), Vector3.one * .13f, Skin);
                Transform leg = new GameObject("Pedalling leg").transform; leg.SetParent(root, false); leg.localPosition = new Vector3(side * .18f, 1.15f, -.2f);
                Beam("Thigh", leg, Vector3.zero, new Vector3(side * .03f, -.32f, .3f), .18f, Dark);
                Beam("Shin", leg, new Vector3(side * .03f, -.32f, .3f), new Vector3(side * .03f, -.68f, .03f), .14f, Dark);
                Primitive("Shoe", PrimitiveType.Cube, leg, new Vector3(side * .03f, -.7f, .1f), new Vector3(.19f, .12f, .34f), White);
                pedals[i] = leg;
            }
            return root;
        }
        internal Mesh Footprint()
        {
            var vertices = new List<Vector3> { Vector3.zero }; var triangles = new List<int>();
            for (int i = 0; i < 32; i++) vertices.Add(new Vector3(Mathf.Cos(i * Mathf.PI / 16), 0, Mathf.Sin(i * Mathf.PI / 16)));
            for (int i = 0; i < 32; i++) triangles.AddRange(new[] { 0, (i + 1) % 32 + 1, i + 1 });
            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() }; mesh.RecalculateNormals();
            return MeshAsset("Footprint disc", mesh);
        }
        internal static Sprite CircleSprite()
        {
            string path = Root + "/Art/UI/Circle.png";
            if (!System.IO.File.Exists(path))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(31.5f - d)));
                }
                texture.Apply(); System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.mipmapEnabled = false; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        internal static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); return saved;
        }
        internal static Mesh MeshAsset(string name, Mesh mesh)
        {
            string path = Root + "/Art/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(mesh); return existing; }
            mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        internal static void Combine(GameObject root, string prefix)
        {
            var groups = root.GetComponentsInChildren<MeshFilter>().GroupBy(f => f.GetComponent<Renderer>().sharedMaterial).ToArray();
            var meshes = new List<(Mesh mesh, Material material)>(); int index = 0;
            foreach (var group in groups)
            {
                var instances = group.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray();
                var mesh = new Mesh(); mesh.CombineMeshes(instances, true, true); mesh.RecalculateBounds();
                meshes.Add((MeshAsset(prefix + "_" + index++, mesh), group.Key));
            }
            while (root.transform.childCount > 0) UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            foreach (var entry in meshes)
            {
                var child = new GameObject(entry.material.name, typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(root.transform, false);
                child.GetComponent<MeshFilter>().sharedMesh = entry.mesh; var renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = entry.material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
        }
        private static Mesh Torus()
        {
            const int rings = 32, sides = 6; var vertices = new Vector3[(rings + 1) * (sides + 1)]; var triangles = new List<int>();
            for (int i = 0; i <= rings; i++) for (int j = 0; j <= sides; j++)
            {
                float a = i * Mathf.PI * 2 / rings, b = j * Mathf.PI * 2 / sides;
                vertices[i * (sides + 1) + j] = new Vector3(Mathf.Cos(b) * .1f, Mathf.Cos(a) * (1 + Mathf.Sin(b) * .1f), Mathf.Sin(a) * (1 + Mathf.Sin(b) * .1f));
                if (i < rings && j < sides) { int k = i * (sides + 1) + j; triangles.AddRange(new[] { k, k + 1, k + sides + 1, k + 1, k + sides + 2, k + sides + 1 }); }
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh Cone(int sides)
        {
            var vertices = new List<Vector3>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), q = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                vertices.AddRange(new[] { Vector3.up, q, p, Vector3.zero, p, q });
            }
            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = Enumerable.Range(0, vertices.Count).ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
