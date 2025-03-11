using Newtonsoft.Json.Linq;
using Oculus.Interaction.HandGrab;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

public class LibraryAnimation : MonoBehaviour
{
    LibraryContext contextLibrary; // reference to the LibraryContext script
    MixedRealityManager MRManager; // reference to the MixedRealityManager script
    private ConcurrentQueue<JObject> AnimationPool;

    string schema = @"{
        ""type"": ""object"",
        ""properties"": {
            ""action"": {
                ""type"": ""string"",
                ""enum"": [
                    ""attach"",
                    ""detach"",
                    ""scale"",
                    ""color"",
                    ""movetowards"",
                    ""rotatetowards"",
                    ""looktowards"",
                    ""catch"",
                    ""selfrotate"",
                    ""orbit"",
                    ""gazing"",
                    ""stop"",
                    ""remove"",
                    ""grabbable""
                ],
                ""descriptions"": {
                    ""attach"": ""Attach an object to the other object."",
                    ""detach"": ""Detach an object from its current parent."",
                    ""scale"": ""Adjust the scale of an object."",
                    ""color"": ""Change the color of an object."",
                    ""movetowards"": ""Move an object to a specific position or the other object's position."",
                    ""rotatetowards"": ""Rotate an object to a specific orientation. Also support degrees of rotation by providing rotating axis, speed and time."",
                    ""looktowards"": ""Rotate an object to face with or look at a specific position or the other object's position."",
                    ""catch"": ""Catch an object by the Robot's hand. Used after the robot is close to the object."",
                    ""selfrotate"": ""Let an object be rotating with a specific axis."",
                    ""orbit"": ""Let an object be orbiting around the other object."",
                    ""gazing"": ""Let an object be facing at the other object."",
                    ""stop"": ""Stop the animation with the specified id. Make sure the id is shown exactly in the animation context."",
                    ""remove"": ""Remove an existing object from the scene by its name."",
                    ""grabbable"": ""Make an object grabbable, i.e., to be interacted with user hand.""
                },
                ""description"": ""Defines the type of animation to be applied to the object.""
            },
            ""object"": {
                ""type"": ""string"",
                ""description"": ""The name of the object to which the action will be applied. Case sensitive. Must be exactly the same as shown in the context instead of the user request.""
            },
            ""id"": {
                ""type"": ""string"",
                ""description"": ""A unique string to identify the animation. Either generate a new string or use exactly the same as shown in the contextual information.""
            },
            ""newobjectname"": {
                ""type"": ""string"",
                ""description"": ""The new name of the object if some properties like the color is changed after the action. Required for 'color' action.""
            },
            ""time"": {
                ""type"": ""number"",
                ""description"": ""The duration over which the action should be completed or ended, in seconds. Optional.""
            },
            ""target"": {
                ""type"": ""string"",
                ""description"": ""The other object's name for an animation invloving multiple objects. Serve as parent object for some animations. Must be exactly the same as shown in the context instead of the user request. Optional.""
            },
            ""scale"": {
                ""type"": ""string"",
                ""description"": ""New scale of the object, specified as 'x y z'. Optional""
            },
            ""color"": {
                ""type"": ""string"",
                ""description"": ""New color of the object, specified as 'r g b'. Values are with a range from 0 to 1. Optional.""
            },
            ""position"": {
                ""type"": ""string"",
                ""description"": ""New position of the object in world coordinate, specified as 'x y z', for movement actions and 'looktowards' action. Optional.""
            },
            ""localposition"": {
                ""type"": ""string"",
                ""description"": ""New position of the object in local coordinate, specified as 'x y z'. If not specified the other object, refer to the coordinate of the object itself. Otherwise, refer to the coordinate of the other object. Optional.""
            },
            ""localdirection"": {
                ""type"": ""string"",
                ""description"": ""The direction in which the action will take place, like moving in specific direction. Optional.""
            },
            ""distance"": {
                ""type"": ""number"",
                ""description"": ""The distance to move the object, used in movement related animations with specified direction. Optional.""
            },
            ""safebound"": {
                ""type"": ""number"",
                ""description"": ""The safe distance to avoid collision with other objects. This can be negative number, which will leverage the rendering information instead of an absolute value. Optional.""
            },
            ""orientation"": {
                ""type"": ""string"",
                ""description"": ""New orientation of the object, specified as 'x y z', for rotational actions. Optional.""
            },
            ""axis"": {
                ""type"": ""string"",
                ""description"": ""Axis along which to rotate, specified as 'x y z', for the 'selfrotate' action. Optional."",
                ""default"": ""0 1 0""
            },
            ""speedRot"": {
                ""type"": ""number"",
                ""default"": 90,
                ""description"": ""Rotation speed in degrees per second, used in rotation related animations. Optional.""
            },
            ""speedMov"": {
                ""type"": ""number"",
                ""default"": 1,
                ""description"": ""Movement speed in meters per second, used in movement related animations. Optional.""
            },
        },
        ""required"": [""action"", ""object"", ""id""]
    }";

    // Start is called before the first frame update
    void Awake()
    {
        AnimationPool = new ConcurrentQueue<JObject>();
        contextLibrary = this.GetComponent<LibraryContext>();
        MRManager = this.GetComponent<MixedRealityManager>();
        if (contextLibrary == null)
        {
            Debug.LogError("contextLibrary is not initialized at the beginning!");
        }
    }

    private void Start()
    {
        StartCoroutine(ExecuteAnimation());
    }

    // Update is called once per frame
    void Update()
    {

    }

    public string GetSchema()
    {
        return schema;
    }

    private LineRenderer SetupLineRenderer(GameObject obj)
    {
        LineRenderer lineRenderer = obj.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = obj.AddComponent<LineRenderer>();
        }
        // set the width scalable to object's size
        Bounds bound = contextLibrary.GetBoundObj(obj);
        float maxBound = Mathf.Max(bound.extents.x, bound.extents.y, bound.extents.z);
        lineRenderer.startWidth = maxBound * 0.1f;
        lineRenderer.endWidth = maxBound * 0.1f;
        lineRenderer.positionCount = 0;
        lineRenderer.material = Resources.Load<Material>("DottedLine");
        // adapt the texture scale to line width
        lineRenderer.material.mainTextureScale = new Vector2(10, 1f);
        lineRenderer.textureMode = LineTextureMode.Tile;
        return lineRenderer;
    }

    public (LineRenderer, Vector3) DrawTrajectoryOrbit(GameObject obj, GameObject target, float radius, int resolution = 100)
    {
        // calculate the normal vector of the plane between the object and the target
        Vector3 auxiliaryVector = Vector3.forward;
        Vector3 directionToTarget = target.transform.position - obj.transform.position;
        if (Mathf.Abs(Vector3.Dot(directionToTarget.normalized, Vector3.forward)) > 0.999)
        {
            auxiliaryVector = Vector3.right;  // Change auxiliary vector if needed
        }
        Vector3 normal = Vector3.Cross(directionToTarget, auxiliaryVector);
        // make the orbiting always clockwise
        if (Vector3.Dot(normal, Vector3.up) < 0)
            normal = -normal;

        LineRenderer lineRenderer = SetupLineRenderer(obj);
        Vector3 center = target.transform.position;
        Vector3[] points = new Vector3[resolution + 1];

        for (int i = 0; i <= resolution; i++)
        {
            float angle = 360f / resolution * i; // Angle in degrees
            Quaternion rotation = Quaternion.AngleAxis(angle, normal);
            Vector3 orbitalPoint = center + rotation * (auxiliaryVector * radius);
            points[i] = orbitalPoint;
        }

        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
        return (lineRenderer, normal);
    }

    public void AddToAnimationPool(JObject data)
    {
        if (data != null)
            AnimationPool.Enqueue(data);
    }

    public IEnumerator ExecuteAnimation()
    {
        while (true)
        {
            if (AnimationPool.TryDequeue(out JObject jdata))
            {
                string action = jdata["action"].ToString();
                string objName = jdata["object"].ToString();
                GameObject obj = Utils.GetGameObject(objName);
                if (obj == null)
                {
                    Debug.LogWarning("Cannot find the object: " + objName);
                    continue;
                }
                string aniName = jdata["id"].ToString();
                string newObjectName = jdata["newobjectname"]?.ToString();
                if (newObjectName != null)
                    obj.name = newObjectName;
                switch (action.ToLower())
                {
                    case "attach":
                        string newparentName = jdata["target"].ToString();
                        GameObject newparent = Utils.GetGameObject(newparentName);
                        AttachObject(obj, newparent);
                        break;
                    case "detach":
                        DetachObject(obj);
                        break;
                    case "scale":
                        Vector3 scale = Utils.ParseVector3(jdata["scale"].ToString());
                        float scaleTime = jdata["time"]?.ToObject<float>() ?? 1f; // default 1 second
                        yield return StartCoroutine(ScaleOverTime(obj, scale, scaleTime, aniName));
                        break;
                    case "color":
                        Vector3 vColor = Utils.ParseVector3(jdata["color"].ToString());
                        Color color = new Color(vColor.x, vColor.y, vColor.z);
                        float colorTime = jdata["time"]?.ToObject<float>() ?? 1f; // default 1 second
                        if (obj.GetComponent<Renderer>() == null)
                        {
                            Debug.LogWarning("Cannot find the renderer component for the object: " + objName);
                            break;
                        }
                        Material material = obj.GetComponent<Renderer>().material;
                        yield return StartCoroutine(FadeColor(material, color, colorTime, aniName));
                        break;
                    case "movetowards":
                        Vector3 position = Vector3.zero;
                        if (jdata["position"] != null)
                            position = Utils.ParseVector3(jdata["position"].ToString());
                        else if (jdata["localposition"] != null)
                        {
                            Vector3 localposition = Utils.ParseVector3(jdata["localposition"].ToString());
                            position = obj.transform.TransformPoint(localposition);
                        }
                        else if (jdata["localdirection"] != null)
                        {
                            Vector3 localdirection = Utils.ParseVector3(jdata["localdirection"].ToString());
                            float distance = jdata["distance"]?.ToObject<float>() ?? 1; // default 1 meter
                            position = obj.transform.position + obj.transform.TransformDirection(localdirection) * distance;
                        }
                        GameObject targetMove = null;
                        if (jdata["target"] != null)
                        {
                            string targetMoveName = jdata["target"].ToString();
                            targetMove = Utils.GetGameObject(targetMoveName);
                            if (targetMove != null)
                            {
                                if (jdata["localposition"] != null)
                                {
                                    Vector3 localposition = Utils.ParseVector3(jdata["localposition"].ToString());
                                    position = targetMove.transform.TransformPoint(localposition);
                                }
                                else if (jdata["localdirection"] != null)
                                {
                                    Vector3 localdirection = Utils.ParseVector3(jdata["localdirection"].ToString());
                                    float distance = jdata["distance"]?.ToObject<float>() ?? 1; // default 1 meter
                                    position = obj.transform.position + targetMove.transform.TransformDirection(localdirection) * distance;
                                }
                                else
                                    position = targetMove.transform.position;
                            }
                        }
                        if (jdata["safebound"] != null)
                        {
                            // calculate the position with safe distance
                            float safebound;
                            float.TryParse(jdata["safebound"].ToString(), out safebound);
                            if (safebound < 0 && targetMove != null)
                                safebound = contextLibrary.GetBoundObj(targetMove).extents.magnitude;
                            position = position + (obj.transform.position - position).normalized * safebound;
                        }
                        // if the object is robot, do not change the y position
                        if (obj.name.Contains("Robot"))
                            position.y = obj.transform.position.y;
                        float moveTowardsSpeed = jdata["speedMov"]?.ToObject<float>() ?? 1; // default 1 meter per second
                        yield return StartCoroutine(MoveObjectTowards(obj, position, moveTowardsSpeed, aniName));
                        break;
                    case "rotatetowards":
                        if (jdata["orientation"] == null)
                        {
                            Debug.LogWarning("Cannot find the orientation for rotating: " + objName);
                            break;
                        }
                        Vector3 orientation = Utils.ParseVector3(jdata["orientation"].ToString());
                        float rotateTowardsSpeed = jdata["speedRot"]?.ToObject<float>() ?? 90; // default 90 degrees per second
                        yield return StartCoroutine(RotateObjectTowards(obj, orientation, rotateTowardsSpeed, aniName));
                        break;
                    case "looktowards":
                        Vector3 lookPosition = Vector3.zero;
                        if (jdata["position"] != null)
                            lookPosition = Utils.ParseVector3(jdata["position"].ToString());
                        else if (jdata["localposition"] != null)
                        {
                            Vector3 localposition = Utils.ParseVector3(jdata["localposition"].ToString());
                            lookPosition = obj.transform.TransformPoint(localposition);
                        }
                        else if (jdata["localdirection"] != null)
                        {
                            Vector3 localdirection = Utils.ParseVector3(jdata["localdirection"].ToString());
                            float distance = jdata["distance"]?.ToObject<float>() ?? 1; // default 1 meter
                            lookPosition = obj.transform.position + obj.transform.TransformDirection(localdirection) * distance;
                        }
                        if (jdata["target"] != null)
                        {
                            string targetLookName = jdata["target"].ToString();
                            GameObject targetLook = Utils.GetGameObject(targetLookName);
                            if (targetLook != null)
                            {
                                if (jdata["localposition"] != null)
                                {
                                    Vector3 localposition = Utils.ParseVector3(jdata["localposition"].ToString());
                                    lookPosition = targetLook.transform.TransformPoint(localposition);
                                }
                                else if (jdata["localdirection"] != null)
                                {
                                    Vector3 localdirection = Utils.ParseVector3(jdata["localdirection"].ToString());
                                    float distance = jdata["distance"]?.ToObject<float>() ?? 1; // default 1 meter
                                    lookPosition = obj.transform.position + targetLook.transform.TransformDirection(localdirection) * distance;
                                }
                                else
                                    lookPosition = targetLook.transform.position;
                            }
                        }
                        float lookSpeed = jdata["speedRot"]?.ToObject<float>() ?? 90; // default 90 degrees per second
                        yield return StartCoroutine(LookAtTarget(obj, lookPosition, lookSpeed, aniName));
                        break;
                    case "catch":
                        if (jdata["target"] != null)
                        {
                            string targetCatchName = jdata["target"].ToString();
                            GameObject targetCatch = Utils.GetGameObject(targetCatchName);
                            if (targetCatch != null)
                            {
                                // original position of the object
                                Vector3 originalPosition = obj.transform.position;
                                float catchSpeed = jdata["speedMov"]?.ToObject<float>() ?? 1; // default 1 meter per second
                                                                                              // calculate the position with safe distance
                                float safebound = jdata["safebound"]?.ToObject<float>() ?? -1;
                                if (safebound < 0)
                                    safebound = contextLibrary.GetBoundObj(targetCatch).extents.magnitude;
                                Vector3 catchTargetPosition = targetCatch.transform.position + (obj.transform.position - targetCatch.transform.position).normalized * safebound;
                                // move the object towards the target
                                yield return StartCoroutine(MoveObjectTowards(obj, catchTargetPosition, catchSpeed, aniName));
                                // attach the target object
                                AttachObject(targetCatch, obj);
                                // move the object back to the original position
                                yield return StartCoroutine(MoveObjectTowards(obj, originalPosition, catchSpeed, aniName));
                                break;
                            }
                        }
                        Debug.LogWarning("Cannot find the target object for catching: " + objName);
                        break;
                    case "selfrotate":
                        Vector3 axis = Vector3.up;
                        if (jdata["axis"] != null)
                        {
                            Vector3 newAxis = Utils.ParseVector3(jdata["axis"].ToString());
                            if (newAxis.magnitude > 0)
                                axis = newAxis;
                        }
                        float speed = jdata["speedRot"]?.ToObject<float>() ?? 90; // default 90 degrees per second
                        float rotatePersistTime = jdata["time"]?.ToObject<float>() ?? -1; // default -1, infinite time
                        StartCoroutine(RotateObjectAxis(obj, axis, speed, rotatePersistTime, aniName));
                        break;
                    case "orbit":
                        string targetOrbitName = jdata["target"].ToString();
                        GameObject targetOrbit = Utils.GetGameObject(targetOrbitName);
                        if (targetOrbit == null)
                        {
                            Debug.LogWarning("Cannot find the orbit target object: " + targetOrbitName);
                            break;
                        }
                        float orbitSpeed = jdata["speedRot"]?.ToObject<float>() ?? 20f; // default 20 degrees per second
                        float orbitPersistTime = jdata["time"]?.ToObject<float>() ?? -1; // default -1, infinite time
                        StartCoroutine(OrbitObject(obj, targetOrbit, orbitSpeed, orbitPersistTime, aniName));
                        break;
                    case "gazing":
                        string targetName = jdata["target"].ToString();
                        GameObject target = Utils.GetGameObject(targetName);
                        if (target == null)
                        {
                            Debug.LogWarning("Cannot find the gazing target object: " + targetName);
                            break;
                        }
                        float gazePersistTime = jdata["time"]?.ToObject<float>() ?? -1; // default -1, infinite time
                        StartCoroutine(Gazing(obj, target, gazePersistTime, aniName));
                        break;
                    case "stop":
                        StopAnimation(aniName);
                        break;
                    case "remove":
                        StartCoroutine(RemoveObject(obj));
                        break;
                    case "grabbable":
                        MRManager.MakeGrabbable(obj);
                        break;
                    default:
                        Debug.Log("Invalid animation type.");
                        break;
                }
            }
            yield return null;
        }
    }

    // move to a specific position
    public IEnumerator MoveObjectTowards(GameObject obj, Vector3 target, float speed, string animationName)
    {
        Utils.AddAnimation(animationName);
        // Store the original position
        Vector3 originalPosition = obj.transform.position;

        // draw the preview trajectory line
        LineRenderer lineRenderer = SetupLineRenderer(obj);
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, originalPosition);
        lineRenderer.SetPosition(1, target);

        float time = 0;
        float duration = Vector3.Distance(originalPosition, target) / speed;

        while (time < duration && Utils.GetAnimation(animationName))
        {
            // Stop the coroutine if the object is destroyed
            if (obj == null)
                break;
            // Increment time by the time elapsed since last frame
            time += Time.deltaTime;
            // Calculate the lerp factor, 0 means original position, 1 means target position
            float lerpFactor = time / duration;

            // Update the object's position smoothly from original position to target position
            obj.transform.position = Vector3.Lerp(originalPosition, target, lerpFactor);

            // Yield execution until the next frame
            yield return null;
        }

        if (Utils.GetAnimation(animationName) && obj != null)
            // Ensure the final position is set exactly to the target position
            obj.transform.position = target;

        // destroy the preview trajectory line
        Destroy(lineRenderer);
        Utils.RemoveAnimationFromList(animationName);
    }

    public IEnumerator RotateObjectTowards(GameObject obj, Vector3 target, float speed, string animationName)
    {
        Utils.AddAnimation(animationName);
        // rotate object to a specific angle (Euler angles)
        // Store the original rotation
        Quaternion originalRotation = obj.transform.rotation;
        float time = 0;

        float duration = Quaternion.Angle(originalRotation, Quaternion.Euler(target)) / speed;

        while (time < duration && Utils.GetAnimation(animationName))
        {
            // Stop the coroutine if the object is destroyed
            if (obj == null)
                break;
            // Increment time by the time elapsed since last frame
            time += Time.deltaTime;
            // Calculate the lerp factor, 0 means original rotation, 1 means target rotation
            float lerpFactor = time / duration;

            // Update the object's rotation smoothly from original rotation to target rotation
            obj.transform.rotation = Quaternion.Lerp(originalRotation, Quaternion.Euler(target), lerpFactor);

            // Yield execution until the next frame
            yield return null;
        }

        if (Utils.GetAnimation(animationName) && obj != null)
            // Ensure the final rotation is set exactly to the target rotation, when running normally to end
            obj.transform.rotation = Quaternion.Euler(target);

        Utils.RemoveAnimationFromList(animationName);
    }

    public IEnumerator RotateObjectAxis(GameObject obj, Vector3 axis, float speed, float time, string animationName)
    {
        Utils.AddAnimation(animationName);

        float duration = 0;
        while ((time < 0 || duration < time) && Utils.GetAnimation(animationName))
        {
            // Stop the coroutine if the object is destroyed
            if (obj == null)
                break;
            // Update the object's rotation for specified axis and speed
            obj.transform.Rotate(axis, speed * Time.deltaTime);

            // Yield execution until the next frame
            yield return null;
            duration += Time.deltaTime;
        }
        Utils.RemoveAnimationFromList(animationName);
    }

    public IEnumerator OrbitObject(GameObject obj, GameObject target, float speed, float time, string animationName)
    {
        // if the object has rigidbody, set it to kinematic
        if (obj.GetComponent<Rigidbody>() != null)
            obj.GetComponent<Rigidbody>().isKinematic = true;

        // draw the preview trajectory line for orbiting
        float radius = Vector3.Distance(obj.transform.position, target.transform.position);

        // get line renderer and normal vector of the plane
        (LineRenderer lineRenderer, Vector3 normal) = DrawTrajectoryOrbit(obj, target, radius);

        // record the original position of the target
        Vector3 originalTargetPosition = target.transform.position;
        // record the original scale of the object
        Vector3 originalScale = obj.transform.localScale;

        Utils.AddAnimation(animationName);

        // rotate object around a specific target
        float duration = 0;
        float start_time = Time.time;
        while (Utils.GetAnimation(animationName) && (time < 0 || duration < time))
        {
            // Stop the coroutine if the object or the target is destroyed
            if (obj == null || target == null)
                break;
            // update the trajectory once the target is moving
            if (Vector3.Distance(originalTargetPosition, target.transform.position) > 0.01f || obj.transform.localScale != originalScale)
            {
                // move the object towards the same direction as the target
                Vector3 offset = target.transform.position - originalTargetPosition;
                obj.transform.position += offset;
                (lineRenderer, normal) = DrawTrajectoryOrbit(obj, target, radius);
                originalTargetPosition = target.transform.position;
                originalScale = obj.transform.localScale;
            }
            obj.transform.RotateAround(target.transform.position, normal, speed * Time.deltaTime);
            // Yield execution until the next frame
            yield return null;
            duration = Time.time - start_time;
        }

        // destroy the preview trajectory line
        Destroy(lineRenderer);
        Utils.RemoveAnimationFromList(animationName);
    }

    public IEnumerator EmitDottedLine(GameObject obj, Vector3 targetPosition)
    {
        LineRenderer lineRenderer = SetupLineRenderer(obj);
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, obj.transform.position);
        lineRenderer.SetPosition(1, targetPosition);
        // let the line disappear after 3 seconds
        yield return new WaitForSeconds(3);
        Destroy(lineRenderer);
    }

    public IEnumerator LookAtTarget(GameObject obj, Vector3 targetPosition, float speed, string animationName)
    {
        // create a dotted line to hint the user
        StartCoroutine(EmitDottedLine(obj, targetPosition));

        // rotate object to look at a specific target (position)
        // Store the original rotation
        Quaternion originalRotation = obj.transform.rotation;
        float time = 0;

        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0; // Keep rotation only on the Y axis, i.e., without tiling the head

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        float duration = Quaternion.Angle(originalRotation, targetRotation) / speed;

        Utils.AddAnimation(animationName);

        while (time < duration && Utils.GetAnimation(animationName))
        {
            // Stop the coroutine if the object is destroyed
            if (obj == null)
                break;
            // Increment time by the time elapsed since last frame
            time += Time.deltaTime;
            // Calculate the lerp factor, 0 means original rotation, 1 means target rotation
            float lerpFactor = time / duration;

            // Update the object's rotation smoothly from original rotation to target rotation
            obj.transform.rotation = Quaternion.Lerp(originalRotation, targetRotation, lerpFactor);

            // Yield execution until the next frame
            yield return null;
        }

        if (Utils.GetAnimation(animationName) && obj != null)
            // Ensure the final rotation is set exactly to the target rotation
            obj.transform.rotation = targetRotation;

        Utils.RemoveAnimationFromList(animationName);
    }

    public IEnumerator Gazing(GameObject obj, GameObject target, float time, string animationName)
    {
        Utils.AddAnimation(animationName);
        float duration = 0;
        float start_time = Time.time;
        while ((time < 0 || duration < time) && Utils.GetAnimation(animationName))
        {
            // Stop the coroutine if the object or the target is destroyed
            if (obj == null || target == null)
                break;
            yield return StartCoroutine(LookAtTarget(obj, target.transform.position, 90, ""));
            // leave one frame to avoid the flickering
            yield return null;
            duration = Time.time - start_time;
        }
        Utils.RemoveAnimationFromList(animationName);
    }

    public IEnumerator ScaleOverTime(GameObject obj, Vector3 target, float duration, string animationName)
    {
        Utils.AddAnimation(animationName);
        // Store the original scale
        Vector3 originalScale = obj.transform.localScale;
        float time = 0;

        while (time < duration && Utils.GetAnimation(animationName))
        {
            // Stop the coroutine if the object is destroyed
            if (obj == null)
                break;
            // Increment time by the time elapsed since last frame
            time += Time.deltaTime;
            // Calculate the lerp factor, 0 means original scale, 1 means target scale
            float lerpFactor = time / duration;

            // Update the object's scale smoothly from original scale to target scale
            obj.transform.localScale = Vector3.Lerp(originalScale, target, lerpFactor);

            // Yield execution until the next frame
            yield return null;
        }

        if (Utils.GetAnimation(animationName) && obj != null)
            // Ensure the final scale is set exactly to the target scale
            obj.transform.localScale = target;

        Utils.RemoveAnimationFromList(animationName);
    }

    IEnumerator FadeColor(Material material, Color endColor, float duration, string animationName)
    {
        Utils.AddAnimation(animationName);
        Color startColor = material.color;
        float elapsed = 0;
        while (Utils.GetAnimation(animationName) && elapsed < duration)
        {
            // Stop the coroutine if the material is destroyed
            if (material == null)
                break;
            material.color = Color.Lerp(startColor, endColor, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (Utils.GetAnimation(animationName) && material != null)
            material.color = endColor;

        Utils.RemoveAnimationFromList(animationName);
    }

    public void AttachObject(GameObject target, GameObject newParent)
    {
        if (target == null || newParent == null)
        {
            Debug.LogWarning("cannot find some objects for attach animation!");
            return;
        }
        Transform targetTransform = target.transform;
        Transform parentTransform = newParent.transform;
        // store the previous parent before pick
        Utils.prevParentsBeforeAttach[targetTransform] = targetTransform.parent;
        targetTransform.SetParent(parentTransform);
        // if having rigidbody, set it to kinematic
        if (targetTransform.GetComponent<Rigidbody>() != null)
            targetTransform.GetComponent<Rigidbody>().isKinematic = true;
        // adjust the local position and rotation to emulate the grab action
        Vector3 localPose = Vector3.zero;
        Bounds bounds = contextLibrary.GetBoundObj(target);
        localPose.x = -bounds.extents.x;
        targetTransform.localPosition = localPose; // adjust the local position to like grabbing the object
        targetTransform.localRotation = Quaternion.identity; // adjust the local rotation
    }

    public void DetachObject(GameObject target)
    {
        Transform targetTransform = target.transform;
        // if having rigidbody, set it to non-kinematic
        if (targetTransform.GetComponent<Rigidbody>() != null)
            targetTransform.GetComponent<Rigidbody>().isKinematic = false;
        // restore the previous parent before pick
        if (Utils.prevParentsBeforeAttach.TryGetValue(targetTransform, out Transform prevParent))
        {
            targetTransform.SetParent(prevParent);
            Utils.prevParentsBeforeAttach.Remove(targetTransform);
        }
    }

    public void StopAnimation(string id)
    {
        Utils.IndicateAnimationStop(id);
    }

    public IEnumerator RemoveObject(GameObject obj)
    {
        // Destroy the object at the end of the frame to properly stop existing coroutines
        yield return new WaitForEndOfFrame();
        Destroy(obj);
    }

    void OnDestroy()
    {
        // Stop all coroutines on destroy to clean up
        StopAllCoroutines();
        // remove all animation from the dictionary
        Utils.RemoveAllAnimations();
    }
}
