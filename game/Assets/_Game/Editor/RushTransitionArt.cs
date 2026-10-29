using UnityEditor;
using UnityEngine;

namespace Lotba.CityRush.Editor
{
    /// <summary>Small ordered transition set inspired by the park street reference.</summary>
    internal static class RushTransitionArt
    {
        internal static GameObject[] Create(RushArt art, Mesh foliage)
        {
            return new[] { Module(art, foliage, 0), Module(art, foliage, 1), Module(art, foliage, 2) };
        }
        private static GameObject Module(RushArt art, Mesh foliage, int stage)
        {
            string folder = RushArt.Root + "/Maps/Transitions/LaBocaToObelisco/Prefabs/Art"; RushArt.Folder(folder);
            string name = new[] { "Exit La Boca", "Park street", "Approach Centro" }[stage];
            string path = folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject(name);
            Material blue = RushArt.Material("Transition blue", "285EAA"), yellow = RushArt.Material("Transition yellow", "E7BC38");
            Material bark = RushArt.Material("Transition bark", "866648"), gold = RushArt.Material("Autumn gold", "CBA642");
            Material orange = RushArt.Material("Autumn orange", "C78B46"), green = RushArt.Material("Park green", "8B9C62");
            Material facade = RushArt.Material("Transition warm facade", "D5C5A4");
            art.Primitive("Park lawn", PrimitiveType.Cube, root.transform, new Vector3(-9.2f, .04f, 0), new Vector3(4.6f, .08f, 12), green);
            for (int i = 0; i < 9; i++)
                art.Beam("Park fence post", root.transform, new Vector3(-7, .1f, -5.5f + i * 1.4f), new Vector3(-7, 1.6f, -5.5f + i * 1.4f), .055f, art.Dark);
            art.Beam("Park fence rail", root.transform, new Vector3(-7, 1.4f, -6), new Vector3(-7, 1.4f, 6), .05f, art.Dark);
            art.Primitive("Neighbourhood building", PrimitiveType.Cube, root.transform, new Vector3(9, 3.5f + stage, 0), new Vector3(4.2f, 7 + stage * 2, 11.8f), stage == 0 ? blue : facade);
            if (stage == 0)
                foreach (float y in new[] { 1.2f, 3.5f, 5.8f }) art.Primitive("Blue and yellow facade band", PrimitiveType.Cube, root.transform, new Vector3(6.87f, y, 0), new Vector3(.05f, .7f, 11.8f), yellow);
            for (int floor = 0; floor < 3 + stage; floor++)
                foreach (float z in new[] { -3.5f, 0f, 3.5f })
                {
                    art.Primitive("Window frame", PrimitiveType.Cube, root.transform, new Vector3(6.84f, 1.8f + floor * 1.8f, z), new Vector3(.1f, 1.1f, 1.05f), art.Pavement);
                    art.Primitive("Window", PrimitiveType.Cube, root.transform, new Vector3(6.76f, 1.8f + floor * 1.8f, z), new Vector3(.06f, .9f, .85f), art.Glass);
                }
            art.Primitive("Low park building", PrimitiveType.Cube, root.transform, new Vector3(-11.5f, 2.3f, -2), new Vector3(4, 4.6f, 7), stage == 0 ? green : facade);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 trunk = new Vector3(side * 5.85f, .1f, 3.4f);
                art.Beam("Autumn tree trunk", root.transform, trunk, trunk + Vector3.up * 5.6f, .32f, bark);
                for (int branch = 0; branch < 4; branch++)
                {
                    float direction = branch % 2 == 0 ? -1 : 1;
                    Vector3 end = trunk + new Vector3(direction * 1.6f, 5 + branch * .5f, branch % 2 == 0 ? -1.1f : .9f);
                    art.Beam("Branch", root.transform, trunk + Vector3.up * (2.6f + branch * .4f), end, .12f, bark);
                    art.MeshObject("Autumn leaves", root.transform, foliage, end + Vector3.up * .4f, new Vector3(1.25f, .75f, 1.25f), branch % 3 == 0 ? green : branch % 2 == 0 ? gold : orange);
                }
                art.Beam("Lamp pole", root.transform, new Vector3(side * 5, .1f, -4.9f), new Vector3(side * 5, 4.9f, -4.9f), .1f, art.Dark);
                art.Beam("Lamp arm", root.transform, new Vector3(side * 5, 4.9f, -4.9f), new Vector3(side * 3.8f, 5, -4.9f), .07f, art.Dark);
                art.Primitive("Lamp", PrimitiveType.Cube, root.transform, new Vector3(side * 3.7f, 5, -4.9f), new Vector3(.4f, .12f, .6f), art.Dark);
            }
            art.Primitive("Park bench seat", PrimitiveType.Cube, root.transform, new Vector3(-8.2f, .6f, 0), new Vector3(.6f, .12f, 2), bark);
            art.Primitive("Park bench back", PrimitiveType.Cube, root.transform, new Vector3(-8.45f, .95f, 0), new Vector3(.1f, .7f, 2), bark);
            RushArt.Combine(root, "TransitionArt" + stage);
            return RushArt.SavePrefab(root, path);
        }
    }
}
