using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lotba.CityRush.Editor
{
    /// <summary>Low-cost location dressing built from shared street furniture and original meshes.</summary>
    internal sealed class RushLocationArt
    {
        internal GameObject[] ObeliscoModules, LaBocaModules;
        internal GameObject Obelisco;
        private RushArt art;
        private Mesh crown;
        internal void Create(RushArt shared)
        {
            art = shared; crown = TreeMesh();
            ObeliscoModules = new GameObject[4]; LaBocaModules = new GameObject[4];
            for (int i = 0; i < 4; i++) { ObeliscoModules[i] = Module(i, false); LaBocaModules[i] = Module(i, true); }
            Obelisco = Landmark();
        }
        private GameObject Module(int variant, bool boca)
        {
            string name = (boca ? "La Boca" : "Obelisco avenue") + " module " + variant;
            string folder = RushArt.Root + "/Maps/" + (boca ? "LaBoca" : "Obelisco") + "/Prefabs/Art";
            RushArt.Folder(folder);
            string assetPath = folder + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null) return existing;
            var root = new GameObject(name);
            Material[] colours = boca
                ? new[] { RushArt.Material("Boca yellow", "EFBF35"), RushArt.Material("Boca orange", "DC663D"), RushArt.Material("Boca blue", "277CA8"), RushArt.Material("Boca teal", "279B88") }
                : new[] { RushArt.Material("Centro limestone", "D8C8A4"), RushArt.Material("Centro ivory", "DDD6C3"), RushArt.Material("Centro stone", "BAAD91"), RushArt.Material("Centro cream", "DBBD8A") };
            Material rail = RushArt.Material("Balcony iron", "303B3E");
            Material foliage = RushArt.Material("Location foliage", "779447");
            Material roof = RushArt.Material("Centro slate", "59616B");
            if (boca) CobblestoneMaterial();
            foreach (int side in new[] { -1, 1 })
            {
                for (int building = 0; building < 2; building++)
                {
                    float z = building == 0 ? -3 : 3;
                    float height = boca ? 4.6f + (variant + building) % 2 * 1.2f : 9 + (variant + building) % 3 * 2.2f;
                    Material paint = colours[(variant + building + (side > 0 ? 1 : 0)) % 4];
                    Box("Facade", root.transform, new Vector3(side * 8.9f, height * .5f, z), new Vector3(4, height, 5.85f), paint);
                    Box("Cornice", root.transform, new Vector3(side * 8.9f, height, z), new Vector3(4.2f, .22f, 6), boca ? colours[(variant + building + 2) % 4] : art.White);
                    Box("Shop glazing", root.transform, new Vector3(side * 6.87f, 1.15f, z), new Vector3(.06f, 1.8f, 2.2f), art.Glass);
                    int floors = boca ? 1 : 4;
                    for (int floor = 0; floor < floors; floor++)
                        foreach (float dz in new[] { -1.55f, 1.55f })
                        {
                            float y = 3.3f + floor * 1.85f;
                            Box("Window frame", root.transform, new Vector3(side * 6.84f, y, z + dz), new Vector3(.13f, 1.2f, 1.05f), boca ? paint : art.White);
                            Box("Window", root.transform, new Vector3(side * 6.74f, y, z + dz), new Vector3(.06f, .98f, .78f), boca ? rail : art.Glass);
                            Box("Balcony slab", root.transform, new Vector3(side * 6.52f, y - .65f, z + dz), new Vector3(.8f, .12f, 1.4f), boca ? colours[(variant + 1) % 4] : art.Pavement);
                            Box("Balcony rail", root.transform, new Vector3(side * 6.13f, y - .15f, z + dz), new Vector3(.06f, .07f, 1.4f), rail);
                            for (int post = 0; post < 5; post++) Box("Balcony post", root.transform, new Vector3(side * 6.13f, y - .4f, z + dz - .6f + post * .3f), new Vector3(.045f, .5f, .045f), rail);
                        }
                    if (boca)
                    {
                        for (int rib = 0; rib < 18; rib++) Box("Corrugated siding", root.transform, new Vector3(side * 6.875f, 2.3f, z - 2.8f + rib * .32f), new Vector3(.035f, 4.35f, .035f), paint);
                        var awning = Box("Cafe awning", root.transform, new Vector3(side * 6.35f, 2.4f, z), new Vector3(1.1f, .12f, 2.5f), colours[(variant + building + 2) % 4]);
                        awning.transform.localRotation = Quaternion.Euler(0, 0, side * -15);
                    }
                    else if (building == 0 && variant % 2 == 0)
                    {
                        art.Primitive("Corner dome", PrimitiveType.Sphere, root.transform, new Vector3(side * 8.9f, height + .65f, z), new Vector3(3.5f, 2.5f, 3.8f), roof);
                        art.Primitive("Dome lantern", PrimitiveType.Cylinder, root.transform, new Vector3(side * 8.9f, height + 2, z), new Vector3(.55f, .5f, .55f), roof);
                    }
                }
                art.Beam("Tree trunk", root.transform, new Vector3(side * 5.7f, .1f, 3.5f), new Vector3(side * 5.7f, 2.7f, 3.5f), .25f, art.Dark);
                art.MeshObject("Faceted canopy", root.transform, crown, new Vector3(side * 5.7f, 3.3f, 3.5f), new Vector3(1.1f, 1.35f, 1.1f), foliage);
                if (boca)
                {
                    Box("Planter", root.transform, new Vector3(side * 5.75f, .37f, -3.8f), new Vector3(.7f, .65f, .7f), art.White);
                    art.MeshObject("Planter foliage", root.transform, crown, new Vector3(side * 5.75f, 1.25f, -3.8f), new Vector3(.6f, .7f, .6f), foliage);
                    art.Beam("Cafe table pedestal", root.transform, new Vector3(side * 5.7f, .1f, -.5f), new Vector3(side * 5.7f, .95f, -.5f), .07f, rail);
                    art.Primitive("Cafe table", PrimitiveType.Cylinder, root.transform, new Vector3(side * 5.7f, .95f, -.5f), new Vector3(1, .035f, 1), colours[variant]);
                    foreach (float dz in new[] { -1.3f, 1f })
                    {
                        Box("Chair seat", root.transform, new Vector3(side * 5.9f, .54f, dz), new Vector3(.5f, .07f, .5f), colours[(variant + 2) % 4]);
                        Box("Chair back", root.transform, new Vector3(side * 5.9f, .85f, dz + .22f), new Vector3(.5f, .6f, .06f), rail);
                        foreach (float dx in new[] { -.2f, .2f }) art.Beam("Chair leg", root.transform, new Vector3(side * 5.9f + dx, .1f, dz), new Vector3(side * 5.9f + dx, .54f, dz), .035f, rail);
                    }
                }
                else
                {
                    Box("Hedge planter", root.transform, new Vector3(side * 5.7f, .3f, -1), new Vector3(1.1f, .35f, 2.4f), art.Pavement);
                    Box("Hedge", root.transform, new Vector3(side * 5.7f, .75f, -1), new Vector3(1, .65f, 2.25f), foliage);
                }
                art.Beam("Street light", root.transform, new Vector3(side * 5, .1f, -4.8f), new Vector3(side * 5, boca ? 3.5f : 5, -4.8f), .11f, rail);
                Box("Lantern", root.transform, new Vector3(side * 5, boca ? 3.6f : 5.1f, -4.8f), new Vector3(.4f, .5f, .4f), art.Gold);
            }
            if (boca)
            {
                art.Beam("Overhead wire left", root.transform, new Vector3(-6.7f, 5.8f, 0), new Vector3(0, 5.2f, 0), .025f, rail);
                art.Beam("Overhead wire right", root.transform, new Vector3(0, 5.2f, 0), new Vector3(6.7f, 5.8f, 0), .025f, rail);
                art.Primitive("Hanging street lamp", PrimitiveType.Cylinder, root.transform, new Vector3(0, 5.1f, 0), new Vector3(.55f, .1f, .55f), art.Dark);
            }
            RushArt.Combine(root, (boca ? "LaBocaArt" : "ObeliscoArt") + variant);
            return RushArt.SavePrefab(root, assetPath);
        }
        private GameObject Landmark()
        {
            string path = RushArt.Root + "/Maps/Obelisco/Prefabs/Landmarks/Obelisco.prefab";
            RushArt.Folder(RushArt.Root + "/Maps/Obelisco/Prefabs/Landmarks");
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("Obelisco landmark");
            Material stone = RushArt.Material("Obelisco pale stone", "E8DFCA");
            Box("Plaza step", root.transform, new Vector3(0, .2f, 0), new Vector3(8, .4f, 8), art.Pavement);
            Box("Plinth", root.transform, new Vector3(0, .7f, 0), new Vector3(4, .6f, 4), stone);
            Vector3[] bottom = { new Vector3(-1.7f, 1, -1.7f), new Vector3(1.7f, 1, -1.7f), new Vector3(1.7f, 1, 1.7f), new Vector3(-1.7f, 1, 1.7f) };
            Vector3[] top = bottom.Select(v => new Vector3(v.x * .62f, 27, v.z * .62f)).ToArray();
            var vertices = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                vertices.AddRange(new[] { bottom[i], top[i], top[j], bottom[i], top[j], bottom[j], top[i], new Vector3(0, 32, 0), top[j] });
            }
            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = Enumerable.Range(0, vertices.Count).ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            art.MeshObject("Tapered stone and pyramid", root.transform, RushArt.MeshAsset("Obelisco tapered mesh", mesh), Vector3.zero, Vector3.one, stone);
            Box("Top opening", root.transform, new Vector3(0, 26.2f, -1.08f), new Vector3(.45f, .5f, .04f), art.Dark);
            RushArt.Combine(root, "ObeliscoLandmark");
            return RushArt.SavePrefab(root, path);
        }
        private static Material CobblestoneMaterial()
        {
            string folder = RushArt.Root + "/Art/Textures"; RushArt.Folder(folder); string path = folder + "/La Boca cobblestones.png";
            if (!System.IO.File.Exists(path))
            {
                var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    int row = y / 32, shifted = (x + row % 2 * 16) % 256, cell = shifted / 32;
                    bool seam = shifted % 32 < 2 || y % 32 < 2;
                    float shade = ((row * 17 + cell * 13) % 7) * .018f;
                    texture.SetPixel(x, y, seam ? new Color(.28f, .27f, .24f) : new Color(.49f + shade, .46f + shade, .38f + shade));
                }
                texture.Apply(); System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.SaveAndReimport();
            }
            Material material = RushArt.Material("La Boca cobblestones", "FFFFFF");
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); material.mainTextureScale = new Vector2(3, 5); EditorUtility.SetDirty(material); return material;
        }
        internal Mesh TreeMesh()
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            Vector3[] p = { new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1) };
            int[] faces = {0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
            var mesh = new Mesh { vertices = faces.Select(i => p[i].normalized).ToArray(), triangles = Enumerable.Range(0, faces.Length).ToArray() }; mesh.RecalculateNormals();
            return RushArt.MeshAsset("Faceted street canopy", mesh);
        }
        private GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material) => art.Primitive(name, PrimitiveType.Cube, parent, position, scale, material);
    }
}
