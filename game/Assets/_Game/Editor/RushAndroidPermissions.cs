#if UNITY_ANDROID
using System.IO;
using System.Xml.Linq;
using UnityEditor.Android;

namespace Lotba.CityRush.Editor
{
    /// <summary>Adds the normal vibration permission to the generated project, preserving Unity's activity manifest.</summary>
    public sealed class RushAndroidPermissions : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            XDocument document = XDocument.Load(manifest);
            XNamespace android = "http://schemas.android.com/apk/res/android";
            foreach (XElement permission in document.Root.Elements("uses-permission"))
                if ((string)permission.Attribute(android + "name") == "android.permission.VIBRATE") return;
            document.Root.AddFirst(new XElement("uses-permission", new XAttribute(android + "name", "android.permission.VIBRATE")));
            document.Save(manifest);
        }
    }
}
#endif
