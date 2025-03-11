using Meta.XR.MRUtilityKit;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class LibraryContext : MonoBehaviour
{
    public TextMeshProUGUI testText;
    string testString = "";
    public float raycastRadius = 5f;
    public GameObject user; // camera rig
    public OVRSkeleton leftHand = null;
    public OVRSkeleton rightHand = null;

    // associated with the environment creation resources
    List<string> prefabNames;
    List<Vector3> prefabBoundSizes;
    private MRUKRoom _currRoom = null;

    [SerializeField]
    private LayerMask _realworldLayer;
    [SerializeField]
    private LayerMask _envObjLayer;

    private void Awake()
    {
        LoadEnvResources();
    }

    // Start is called before the first frame update
    void Start()
    {

    }


    // Update is called once per frame
    void Update()
    {

    }

    private void LoadEnvResources()
    {
        // using resources.load to load prefabNames.txt
        TextAsset textAsset = Resources.Load<TextAsset>("prefabNames");
        if (textAsset == null)
        {
            Debug.LogWarning("Prefab names file not found.");
            return;
        }
        prefabNames = new List<string>();
        prefabBoundSizes = new List<Vector3>();

        // Split the text into lines
        string[] lines = textAsset.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
        foreach (string line in lines)
        {
            if (line.Trim() == "") continue;
            prefabNames.Add(line);
            GameObject prefab = Resources.Load<GameObject>(line);
            if (!prefab)
            {
                Debug.LogWarning("Prefab not found: " + line);
            }
            else
            {
                // get the bound size of the prefab
                Bounds bounds = GetBoundObj(prefab);
                Vector3 boundSize = bounds.size;
                prefabBoundSizes.Add(boundSize);
            }
        }
        Debug.Log("All prefabs: \n" + GetAllPrefabNames());
    }

    public string GetAllPrefabNames()
    {
        // Using LINQ to combine names and sizes
        var formattedStrings = prefabBoundSizes.Zip(prefabNames, (size, name) => $"{name} ({size.x:F3},{size.y:F3},{size.z:F3})");
        return string.Join("\n", formattedStrings) + "\n";
    }

    public string GetContextData(JObject jdata)
    {
        string contextData = "";
        bool reqPos = jdata["position"]?.Value<bool>() ?? false; // default to false
        bool reqOri = jdata["orientation"]?.Value<bool>() ?? false;
        bool reqScale = jdata["scale"]?.Value<bool>() ?? false;
        bool reqSize = jdata["size"]?.Value<bool>() ?? false;
        bool reqScene = jdata["scene"]?.Value<bool>() ?? false;
        bool reqRobot = jdata["robot"]?.Value<bool>() ?? false;
        bool reqResource = jdata["resource"]?.Value<bool>() ?? false;
        bool reqAnimationData = jdata["animationData"]?.Value<bool>() ?? false;
        bool reqUser = jdata["user"]?.Value<bool>() ?? false;
        if (reqRobot)
        {
            contextData += "The following is the contextual data associated to you, i.e., the robot, including the object name";
            contextData += reqPos ? ", position" : "";
            contextData += reqOri ? ", orientation" : "";
            contextData += reqScale ? ", scale" : "";
            contextData += reqSize ? ", size" : "";
            contextData += ".\n";
            contextData += GetRobotData(reqPos, reqOri, reqScale, reqSize) +
            "Robot is the parent object, while others are joints of the robot. \n";
        }
        if (reqScene)
        {
            contextData += "The following is the contextual data associated to the scene, including the object name";
            contextData += reqPos ? ", position" : "";
            contextData += reqOri ? ", orientation" : "";
            contextData += reqScale ? ", scale" : "";
            contextData += reqSize ? ", size" : "";
            contextData += ".\n";
            contextData += GetSceneData(GetUserPosition(), raycastRadius, reqPos, reqOri, reqScale, reqSize);
        }
        if (reqResource)
        {
            contextData += "The following are all the prefabs resources that you can use and their sizes (width,height,length):\n";
            contextData += GetAllPrefabNames();
        }
        if (reqAnimationData && Utils.NumActiveAnimations() > 0)
        {
            // get animation data
            contextData += "The following are id of active animations in current scene.\n";
            contextData += Utils.GetActiveAnimation();
        }
        if (reqUser)
        {
            contextData += "The following are contexual data associtated to the player, including the object name";
            contextData += reqPos ? ", position" : "";
            contextData += reqOri ? ", orientation" : "";
            contextData += reqScale ? ", scale" : "";
            contextData += reqSize ? ", size" : "";
            contextData += ".\n";
            contextData += GetUserInfo(reqPos, reqOri, reqScale, reqSize);
        }
        else
        {
            // at least provide the user position
            contextData += "The following is the position of the user:\n" + GetUserPosition().ToString() + "\n";
        }
        if (Utils.GetUserMessageCount() > 0)
            contextData += "The following are the previous " + Utils.GetUserMessageCount() + " messages from the user, use them to infer some contextual data:\n" + Utils.GetUserMessages();
        return contextData;
    }

    string objInfo(GameObject obj, bool reqPos, bool reqOri, bool reqScale, bool reqSize)
    {
        if (obj == null)
            return "";
        string objName = obj.name;
        string objPos = reqPos ? obj.transform.position.ToString() : "";
        string objOri = reqOri ? obj.transform.rotation.eulerAngles.ToString() : "";
        string objScale = reqScale ? obj.transform.localScale.ToString() : "";
        string objSize = reqSize ? GetBoundObj(obj).size.ToString() : "";
        return string.Join(" ", objName, objPos, objOri, objScale, objSize) + "\n";
    }

    string objInfoLocal(GameObject obj, bool reqPos, bool reqOri, bool reqScale, bool reqSize)
    {
        if (obj == null)
            return "";
        string objName = obj.name;
        string objPos = reqPos ? obj.transform.localPosition.ToString() : "";
        string objOri = reqOri ? obj.transform.localRotation.eulerAngles.ToString() : "";
        string objScale = reqScale ? obj.transform.localScale.ToString() : "";
        string objSize = reqSize ? GetBoundObj(obj).size.ToString() : "";
        return string.Join(" ", objName, objPos, objOri, objScale, objSize) + " (local)\n";
    }

    string handInfo(GameObject obj, bool reqPos, bool reqOri, bool reqScale, bool reqSize)
    {
        if (obj == null)
            return "";
        string objName = obj.name;
        Guid uuid = Utils.GetMRGuid(obj);
        string objPos = reqPos ? obj.transform.position.ToString() : "";
        string objOri = reqOri ? obj.transform.rotation.eulerAngles.ToString() : "";
        string objScale = reqScale ? obj.transform.localScale.ToString() : "";
        string objSize = reqSize ? GetBoundObj(obj).size.ToString() : "";
        return string.Join(" ", uuid.ToString(), objName, objPos, objOri, objScale, objSize) + "\n";
    }

    string sceneAnchorInfo(Transform colliderObj, MRUKAnchor anchor, bool reqPos, bool reqOri, bool reqScale, bool reqSize)
    {
        GameObject parent = anchor.gameObject;
        string uuid = "";
        Guid guid = anchor.Anchor.Uuid;
        uuid = guid.ToString();
        Utils.AddMRComponent(guid, parent);

        // parent position refers to center of surface, object position refers to center of 3D object
        string label = string.Join(",", anchor.AnchorLabels);
        string objPos = reqPos ? parent.transform.position.ToString() : "";
        string objOri = reqOri ? colliderObj.rotation.eulerAngles.ToString() : "";
        // the size of the scene anchor can be obtained by the OVRScenePlane or OVRSceneVolume component, dimensions
        string objScale = reqScale ? Vector3.one.ToString() : "";
        string objSize = "";
        if (reqSize)
        {
            // must obtain from Volume first, as for some objects have both components but volume overrides plane
            if (anchor.VolumeBounds.HasValue)
            {
                objSize = anchor.VolumeBounds.Value.size.ToString();
            }
            else if (anchor.PlaneRect.HasValue)
            {
                objSize = anchor.PlaneRect.Value.size.ToString();
            }
        }
        return string.Join(" ", uuid, label, objPos, objOri, objScale, objSize) + "\n";
    }

    public string GetSceneData(Vector3 target, float radius, bool reqPos, bool reqOri, bool reqScale, bool reqSize)
    {
        string neighborList = "";
        HashSet<GameObject> uniqueObjects = new HashSet<GameObject>();
        Debug.Log("start collecting data.");
        // find virtual objects created in the scene with specified layers
        Collider[] collidersVirtual = Physics.OverlapSphere(target, radius, _envObjLayer);
        if (collidersVirtual.Length > 0)
        {
            // sort the collider based on the distance to the target
            Array.Sort(collidersVirtual, (x, y) => Vector3.Distance(x.transform.position, target).CompareTo(Vector3.Distance(y.transform.position, target)));
            neighborList += "The following are the contextual data associated to the virtual objects:\n";
        }
        foreach (Collider collider in collidersVirtual)
        {
            // find the root parent of the collider
            Transform root = collider.transform;
            // Climb up the hierarchy until no more parents in the object layers are found
            while (root.parent != null)
            {
                if (!Utils.IsLayerInLayerMask(root.parent.gameObject.layer, _envObjLayer))
                {
                    break;
                }
                root = root.parent;
            }
            if (uniqueObjects.Contains(root.gameObject)) continue;
            uniqueObjects.Add(root.gameObject);
            // add all children of the root to the uniqueObjects set, including children of children, etc.
            Transform[] children = root.GetComponentsInChildren<Transform>();
            foreach (Transform child in children)
            {
                // filter those without collider or rigidbody, except for the root object
                if (child != root && (child.GetComponent<Collider>() == null && child.GetComponent<Rigidbody>() == null))
                {
                    continue;
                }
                neighborList += objInfo(child.gameObject, reqPos, reqOri, reqScale, reqSize);
            }
        }
        // find nearby scene anchors around the target position
        Collider[] collidersRealWorld = Physics.OverlapSphere(target, radius, _realworldLayer);
        if (collidersRealWorld.Length > 0)
        {
            // sort the collider based on the distance to the target
            Array.Sort(collidersRealWorld, (x, y) => Vector3.Distance(x.transform.position, target).CompareTo(Vector3.Distance(y.transform.position, target)));
            neighborList += "The following are the contextual data associated to the real-world objects, here we use uuid and labels to replace object name, the position refers to center of surface:\n";
        }
        foreach (Collider collider in collidersRealWorld)
        {
            // Get the parent of the collider
            GameObject obj = collider.gameObject;
            Transform parent = collider.transform.parent;
            MRUKAnchor anchor = null;
            // Try to get the MRUK anchors, either on parent or on parent's parent
            if (parent != null && parent.gameObject != null)
            {
                anchor = parent.gameObject.GetComponent<MRUKAnchor>();
                if (anchor == null && parent.parent != null && parent.parent.gameObject != null)
                {
                    anchor = parent.parent.gameObject.GetComponent<MRUKAnchor>();
                }
            }
            bool isSceneAnchor = anchor != null;
            // for those from Scene Anchor, handle specially
            if (isSceneAnchor)
            {
                neighborList += sceneAnchorInfo(obj.transform, anchor, reqPos, reqOri, reqScale, reqSize);
            }
        }

        return neighborList;
    }

    public string GetRobotData(bool reqPos, bool reqOri, bool reqScale, bool reqSize)
    {
        // find robot in the scene
        GameObject robot = GameObject.Find("Robot");
        if (robot == null)
        {
            return "Robot not found in the scene.\n";
        }
        string robotList = "";
        robotList += objInfo(robot, reqPos, reqOri, reqScale, reqSize);
        // iterate through all children of the robot
        foreach (Transform child in robot.transform)
        {
            robotList += objInfoLocal(child.gameObject, reqPos, reqOri, reqScale, reqSize);
        }
        return robotList;
    }

    public Vector3 GetUserPosition()
    {
        Vector3 pos = user.transform.Find("TrackingSpace/CenterEyeAnchor").position;
        pos.y = 0.8f;
        return pos;
    }

    public string GetUserInfo(bool req, bool reqOri, bool reqScale, bool reqSize)
    {
        string userInfo = "";
        // tracking space
        Transform trackingSpace = user.transform.Find("TrackingSpace");
        userInfo += objInfo(user, req, reqOri, reqScale, reqSize);
        // center eye anchor
        Transform centerEyeAnchor = trackingSpace.Find("CenterEyeAnchor");
        userInfo += objInfo(centerEyeAnchor.gameObject, req, reqOri, reqScale, reqSize);
        if (leftHand != null && leftHand.IsInitialized)
        {
            // left hand anchor
            userInfo += "The following are the contextual data associated to the left hand, here we use uuid and labels to replace object name: \n";
            // left hand bones
            var leftbones = leftHand.Bones;
            foreach (var bone in leftbones)
            {
                var boneId = bone.Id;
                if (boneId == OVRSkeleton.BoneId.Hand_WristRoot || boneId == OVRSkeleton.BoneId.Hand_ForearmStub || boneId == OVRSkeleton.BoneId.Hand_Thumb3 || boneId == OVRSkeleton.BoneId.Hand_Index3 || boneId == OVRSkeleton.BoneId.Hand_Middle3 || boneId == OVRSkeleton.BoneId.Hand_Ring3 || boneId == OVRSkeleton.BoneId.Hand_Pinky3)
                {
                    userInfo += handInfo(bone.Transform.gameObject, req, reqOri, reqScale, reqSize);
                }
            }
        }
        if (rightHand != null && rightHand.IsInitialized)
        {
            // right hand anchor
            userInfo += "The following are the contextual data associated to the right hand, here we use uuid and labels to replace object name: \n";
            // right hand bones
            var rightbones = rightHand.Bones;
            foreach (var bone in rightbones)
            {
                var boneId = bone.Id;
                if (boneId == OVRSkeleton.BoneId.Hand_WristRoot || boneId == OVRSkeleton.BoneId.Hand_ForearmStub || boneId == OVRSkeleton.BoneId.Hand_Thumb3 || boneId == OVRSkeleton.BoneId.Hand_Index3 || boneId == OVRSkeleton.BoneId.Hand_Middle3 || boneId == OVRSkeleton.BoneId.Hand_Ring3 || boneId == OVRSkeleton.BoneId.Hand_Pinky3)
                {
                    userInfo += handInfo(bone.Transform.gameObject, req, reqOri, reqScale, reqSize);
                }
            }
        }
        return userInfo;
    }

    public Bounds GetBoundObj(GameObject obj)
    {
        Bounds bounds;
        if (obj.GetComponent<Renderer>() == null)
        {
            bounds = GetCombinedBoundingBox(obj);
        }
        else
            bounds = obj.GetComponent<Renderer>().bounds;
        return bounds;
    }

    Bounds GetCombinedBoundingBox(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.zero); // No renderer found, create a centered bounds

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        return combinedBounds;
    }

    public void OnSceneLoaded()
    {
        StartCoroutine(UpdateLayerOfMRUKObject());
    }

    // Update the layer of objects generated by MRUK
    IEnumerator UpdateLayerOfMRUKObject()
    {
        // Wait for the initialization of effective mesh
        yield return new WaitForSeconds(1);
        _currRoom = MRUK.Instance.GetCurrentRoom();
        int layerIndex = (int)Mathf.Log(_realworldLayer.value, 2);
        foreach (MRUKAnchor childAnchor in _currRoom.Anchors)
        {
            // Update the layer of the object and its children
            childAnchor.gameObject.layer = layerIndex;
            for (int i = 0; i < childAnchor.transform.childCount; i++)
            {
                Transform child = childAnchor.transform.GetChild(i);
                child.gameObject.layer = layerIndex;
            }
        }
    }
}
