#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;


/* This script is used to collect all available prefabs in the resource folder
 * only can be used in editor mode
 * 
 * */
public class ResourceNamesCollector : EditorWindow
{
    [MenuItem("Tools/Collect Resource Names")]
    public static void CollectResourceNames()
    {
        string resourceFolderPath = Application.dataPath + "/Resources";
        string[] files = Directory.GetFiles(resourceFolderPath, "*", SearchOption.AllDirectories);
        List<string> resourceNames = new List<string>();

        foreach (string file in files)
        {
            //Debug.Log(file);
            if (!file.EndsWith(".prefab")) continue;

            string relativePath = "Assets" + file.Substring(Application.dataPath.Length);
            string assetPath = AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Object>(relativePath));
            string resourcePath = Path.ChangeExtension(assetPath.Replace("Assets/Resources/", ""), null);
            resourceNames.Add(resourcePath);
            //Debug.Log(resourcePath);
        }

        // Writing the resource names to a plain text file
        using (StreamWriter sw = new StreamWriter(Application.dataPath + "/Resources/prefabNames.txt"))
        {
            foreach (string name in resourceNames)
            {
                sw.WriteLine(name);
            }
        }
        Debug.Log("Resource names collected");
    }
}
#endif
