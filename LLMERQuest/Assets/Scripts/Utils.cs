using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Utils: MonoBehaviour
{
    static Dictionary<string,bool> animationControl = new Dictionary<string, bool>();
    static Dictionary<Guid, GameObject> MRcomponents = new Dictionary<Guid, GameObject>(); // store MR components like real-world objects and the user's hands skeleton
    static Dictionary<GameObject, Guid> MRcomponentsInverseDict = new Dictionary<GameObject, Guid>(); // store inverse index for the user's hands skeleton
    public static Dictionary<Transform, Transform> prevParentsBeforeAttach = new Dictionary<Transform, Transform>();
    static ConcurrentQueue<string> userMessageQueue = new ConcurrentQueue<string>();
    static int MaxUserMessages = 10; // maintain latest 10 messages of the user

    // server IP address, can use localhost if running in Unity Editor
    public static string ServerIP = "{YOUR_SERVER_IP}";
    public static int ServerPort = 8085; // replace with the port you will use, must match with the forward server

    // for mobile version, directly provide the API key
    public static string OPENAI_API_KEY = "{YOUR_API_KEY_HERE}";

    public static void AddUserMessage(string message)
    {
        userMessageQueue.Enqueue(message);
        if (userMessageQueue.Count > MaxUserMessages)
        {
            userMessageQueue.TryDequeue(out string _);
        }
    }

    public static int GetUserMessageCount()
    {
        return userMessageQueue.Count - 1; // exclude the current message
    }

    public static string GetUserMessages()
    {
        // convert user messages to string but exclude the current message
        return string.Join("\n", userMessageQueue.ToArray().Reverse().Skip(1).Reverse());
    }

    public static int NumActiveAnimations()
    {
        return animationControl.Count;
    }

    public static string GetActiveAnimation()
    {
        return string.Join(",", animationControl.Keys) + "\n";
    }

    public static Guid GetMRGuid(GameObject obj)
    {
        if (MRcomponentsInverseDict.ContainsKey(obj))
        {
            return MRcomponentsInverseDict[obj];
        }
        Guid guid = Guid.NewGuid();
        MRcomponentsInverseDict[obj] = guid;
        AddMRComponent(guid, obj);
        return guid;
    }

    public static void AddMRComponent(Guid uuid, GameObject obj)
    {
        if (MRcomponents.ContainsKey(uuid))
        {
            return;
        }
        MRcomponents[uuid] = obj;
    }

    public static GameObject GetGameObject(string name)
    {
        bool isUUID = Guid.TryParse(name, out Guid uuid);
        if (isUUID)
        {
            if (MRcomponents.ContainsKey(uuid))
            {
                return MRcomponents[uuid];
            }
            else
            {
                Debug.Log("No object found with UUID: " + name);
                return null;
            }
        }
        GameObject[] namedObjects = FindObjectsOfType<GameObject>()
                    .Where(obj => obj.name == name).ToArray();
        if (namedObjects.Length == 0) return null;
        else if (namedObjects.Length == 1) return namedObjects[0];
        else
        {
            Debug.Log("Multiple objects found with name: " + name);
            // return objects closest to the main camera
            Vector3 userPos = Camera.main.transform.position;
            GameObject closestObj = namedObjects[0];
            float minDist = Vector3.Distance(userPos, closestObj.transform.position);
            foreach (GameObject obj in namedObjects)
            {
                float dist = Vector3.Distance(userPos, obj.transform.position);
                if (dist < minDist)
                {
                    closestObj = obj;
                    minDist = dist;
                }
            }
            return closestObj;
        }
    }

    public static bool GetAnimation(string name)
    {
        animationControl.TryGetValue(name, out bool running);
        return running;
    }

    public static void IndicateAnimationStop(string name)
    {
        animationControl[name] = false;
    }
    public static void AddAnimation(string name)
    {
        animationControl[name] = true;
    }

    public static void RemoveAllAnimations()
    {
        if (animationControl.Count > 0)
            Debug.Log("Remaining animations: " + string.Join(",", animationControl.Keys));
        animationControl.Clear();
    }

    public static void RemoveAnimationFromList(string name)
    {
        if (animationControl.ContainsKey(name))
        {
            if (name.Length > 0)
                Debug.Log("Removing animation: " + name);
            animationControl.Remove(name);
        }
    }


    public static Vector3 ParseVector3(string s)
    {
        var parts = s.Split(' ');
        float x, y, z;
        if (parts.Length>=3 && float.TryParse(parts[0], out x) && float.TryParse(parts[1], out y) && float.TryParse(parts[2], out z))
        {
            return new Vector3(x, y, z);
        }
        else
        {
            Debug.LogWarning("Failed to parse Vector3: " + s);
            return Vector3.zero;
        }
    }

    public static Quaternion ParseQuaternion(string s)
    {
        var parts = s.Split(' ');
        return new Quaternion(
            float.Parse(parts[0]),
            float.Parse(parts[1]),
            float.Parse(parts[2]),
            float.Parse(parts[3])
        );
    }

public static bool IsLayerInLayerMask(int layerIndex, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layerIndex)) != 0;
    }
}
