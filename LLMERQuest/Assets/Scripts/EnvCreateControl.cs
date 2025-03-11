using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.VisualScripting;
using Newtonsoft.Json.Linq;

/**
 * * This class is used to let robot create specific environment with existing objects.
 * * 
 */

public class EnvCreateControl : MonoBehaviour
{

    List<GameObject> prefabObjects;
    LibraryAnimation animationLibrary;

    GameObject planeHolder;
    bool hasBase = false;
    [SerializeField] private bool MRmode = false;

    enum EnvState
    {
        Idle,
        Constructing
    }
    EnvState state = EnvState.Idle;
    int CurrLayer = -1;

    string schema = @"{
        ""type"": ""object"",
        ""properties"": {
            ""prefabType"": {
                ""type"": ""string"",
                ""description"": ""Name of prefab used for creating objects. Either from existing prefabs resources (not the name of objects associated to the scene) in the context or Unity primitives (cube, sphere, cylinder, capsule, plane, quad, empty).""
            },
            ""objectName"": {
                ""type"": ""string"",
                ""description"": ""Assigned name for the created object, usually the prefabType plus specified properties or a number.""
            },
            ""layer"": {
                ""type"": ""number"",
                ""description"": ""A specified layer number to classify objects.""
            },
            ""parent"": {
                ""type"": ""string"",
                ""description"": ""The parent object of the current object. Case sensitive. Must be exactly the same as shown in the context associated to virtual objects. Do not use it for real-world objects. Optional.""
            },
            ""position"": {
                ""type"": ""string"",
                ""description"": ""New position of the object in world coordinate, specified as 'x y z'. Determined by the given contextual information.""
            },
            ""localposition"": {
                ""type"": ""string"",
                ""description"": ""New position of the object relative to the parent object, specified as 'x y z'.""
            },
            ""rotation"": {
                ""type"": ""string"",
                ""description"": ""New orientation of the object, specified as 'x y z'.""
            },
            ""scale"": {
                ""type"": ""string"",
                ""description"": ""New scale of the object, specified as 'x y z'. Optional.""
            },
            ""color"": {
                ""type"": ""string"",
                ""description"": ""New color of the object, specified as 'r g b'. Values are with a range from 0 to 1. Optional.""
            }
        },
        ""required"": [""prefabType"", ""objectName"", ""layer"", ""rotation""]
    }";

    // Use Awake such that it is initialized before calling OpenAI API.
    void Awake()
    {
        if (!MRmode)
            planeHolder = GameObject.Find("PlaneHolder"); // find the holder plane in VR mode

        prefabObjects = new List<GameObject>();
    }

    private void Start()
    {
        animationLibrary = this.GetComponent<LibraryAnimation>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!MRmode)
        {
            // set the planeholder to inactive if there is object in layer 0 of environment creation
            if (planeHolder.activeSelf && hasBase)
            {
                planeHolder.SetActive(false);
            }
            if (!planeHolder.activeSelf && !hasBase)
            {
                planeHolder.SetActive(true);
            }
        }

    }

    public string GetSchema()
    {
        return schema;
    }
    public GameObject CreateObjPrimitive(string type)
    {
        GameObject instance = null;
        switch (type.ToLower())
        {
            case "cube":
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                break;
            case "sphere":
                instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                break;
            case "cylinder":
                instance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                break;
            case "capsule":
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                break;
            case "plane":
                instance = GameObject.CreatePrimitive(PrimitiveType.Plane);
                break;
            case "quad":
                instance = GameObject.CreatePrimitive(PrimitiveType.Quad);
                break;
            case "empty":
                instance = new GameObject();
                break;
            default:
                Debug.LogWarning("Invalid primitive type.");
                break;
        }
        return instance;
    }

    public void CreateObj(JObject jdata)
    {
        string prefabName = jdata["prefabType"]?.ToString();
        string objName = jdata["objectName"]?.ToString();
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        if (jdata["position"] != null)
        {
            pos = Utils.ParseVector3(jdata["position"].ToString());
        }
        if (jdata["rotation"] != null)
        {
            rot = Quaternion.Euler(Utils.ParseVector3(jdata["rotation"].ToString()));
        }
        GameObject instance = null;
        GameObject prefab = Resources.Load<GameObject>(prefabName);
        bool isPrimitive = prefab == null;
        if (isPrimitive)
        {
            instance = CreateObjPrimitive(prefabName);
            if (instance == null)
            {
                Debug.LogWarning("Failed to instantiate the primitive type.");
                return;
            }
            instance.transform.position = pos;
            instance.transform.rotation = rot;
        }
        else
        {
            // Instantiate the prefab.
            instance = Instantiate(prefab, pos, rot);
        }

        // adjust the scale if specified
        string scale = jdata["scale"]?.ToString();
        if (scale != null)
        {
            instance.transform.localScale = Utils.ParseVector3(scale);
        }

        string parent = jdata["parent"]?.ToString();
        GameObject parentObj = parent != null ? Utils.GetGameObject(parent) : null;
        if (parentObj != null)
        {
            instance.transform.SetParent(parentObj.transform);
            if (jdata["localposition"] != null)
            {
                // the localposition is generated in a custom based on the world coordinate system, i.e., x refers to right, y refers to up, z refers to forward
                // adjust the local position if the parent object has specified orientation
                Vector3 initLocalPosition = Utils.ParseVector3(jdata["localposition"].ToString());
                Vector3 rotateLocalPostion = parentObj.transform.InverseTransformDirection(initLocalPosition);
                instance.transform.localPosition = rotateLocalPostion;
            }
        }

        // adjust the color if specified
        string color = jdata["color"]?.ToString();
        if (color != null)
        {
            Vector3 colorVector = Utils.ParseVector3(color);
            // create color from vector3
            Color newColor = new Color(colorVector.x, colorVector.y, colorVector.z);
            // check if the object has renderer
            if (instance.GetComponent<Renderer>() != null)
            {
                instance.GetComponent<Renderer>().material.color = newColor;
            }
        }
        int layer = jdata["layer"]?.Value<int>() ?? 1;
        if (layer < 0)
        {
            Debug.Log("Invalid Layer.");
            return;
        }
        else if (layer == 0)
            hasBase = true;
        // use layer to adjust the position of the prefab if not applicable
        CurrLayer = layer;
        // set the name
        instance.name = objName;
        // set the layer of the gameobject
        instance.layer = layer + 6; // 5 is the layer of the UI
        // make all its children to have the same layer
        foreach (Transform child in instance.GetComponentInChildren<Transform>())
        {
            // do not update the layer of the whiteboard
            if (child.tag == "Whiteboard") continue;
            child.gameObject.layer = Mathf.Min(layer + 1 + 6, 8); // The maximum layer is 8
        }
        // add to the list
        prefabObjects.Add(instance);

        // add an animation to face at the object
        StartCoroutine(animationLibrary.LookAtTarget(this.GameObject(), instance.transform.position, 90, "creatingObjects"));
    }

    // destroy previous objects
    public IEnumerator DestroyObjs()
    {
        // Destroy objects at the end of the frame to properly stop existing coroutines
        yield return new WaitForEndOfFrame();
        foreach (GameObject obj in prefabObjects)
        {
            Destroy(obj);
        }
        prefabObjects.Clear();
        hasBase = false;
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

    public void IndicateIdle()
    {
        state = EnvState.Idle;
    }

    public void IndicateConstruct()
    {
        state = EnvState.Constructing;
        CurrLayer = 0;
    }

    public bool IsIdle()
    {
        return state == EnvState.Idle;
    }
}
