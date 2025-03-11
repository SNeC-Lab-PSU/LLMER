using UnityEngine;
using Unity.VisualScripting;

namespace Oculus.Interaction.HandGrab
{
    public class MixedRealityManager : MonoBehaviour
    {
        public GameObject grabblePrefab;

        [SerializeField] private OVRHand leftHand;
        [SerializeField] private OVRHand rightHand;
        private OVRSkeleton leftHandSkeleton;
        private OVRSkeleton rightHandSkeleton;

        string schema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""prefabType"": {
                    ""type"": ""string"",
                    ""description"": ""Name of prefab used for creating objects. From Unity primitives (cube, sphere, cylinder, capsule, plane).""
                },
                ""objectName"": {
                    ""type"": ""string"",
                    ""description"": ""Assigned name for the created object, usually the prefabType plus specified properties or a number.""
                },
                ""position"": {
                    ""type"": ""string"",
                    ""description"": ""New position of the object in world coordinate, specified as 'x y z'.""
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
            ""required"": [""prefabType"", ""objectName"", ""position"", ""rotation""]
        }";

        public string GetSchema()
        {
            return schema;
        }

        // Start is called before the first frame update
        void Start()
        {
            // get hand skeleton from hand object
            if (leftHand != null && rightHand != null)
            {
                leftHandSkeleton = leftHand.gameObject.GetComponent<OVRSkeleton>();
                rightHandSkeleton = rightHand.gameObject.GetComponent<OVRSkeleton>();
            }
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void MakeGrabbable(GameObject obj)
        {
            if (obj == null) { return; }
            // create a rigidbody component for the object if it doesn't have one
            if (obj.GetComponent<Rigidbody>() == null)
            {
                obj.AddComponent<Rigidbody>();
            }
            // add collider to the object if it doesn't have one
            if (obj.GetComponent<Collider>() == null)
            {
                obj.AddComponent<BoxCollider>();
            }
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            // set properties of the rigidbody
            rb.useGravity = false;
            rb.isKinematic = true;
            // instantiate the grabble prefab as the child object of the object
            GameObject grabbable = Instantiate(grabblePrefab, obj.transform);
            // set properties of the grabbable
            // Grabble -> TargetTransform
            Grabbable grabbableScript = grabbable.GetComponent<Grabbable>();
            grabbableScript.InjectOptionalTargetTransform(obj.transform);
            bool getOneGrab = grabbableScript.TryGetComponent(out OneGrabFreeTransformer grabbableTransformer);
            if (!getOneGrab) grabbableTransformer = grabbableScript.AddComponent<OneGrabFreeTransformer>();
            grabbableScript.InjectOptionalOneGrabTransformer(grabbableTransformer);
            grabbableTransformer.Initialize(grabbableScript);
            bool getTwoGrab = grabbableScript.TryGetComponent(out TwoGrabFreeTransformer grabbableTwoGrabTransformer);
            if (!getTwoGrab) grabbableTwoGrabTransformer = grabbableScript.AddComponent<TwoGrabFreeTransformer>();
            // the script will not automatically initialize the constraints unless the script is attached before running application, in which Unity automatically initializes the serialized fields
            var twoGrabConstraints = new TwoGrabFreeTransformer.TwoGrabFreeConstraints();
            twoGrabConstraints.MinScale = new FloatConstraint();
            twoGrabConstraints.MaxScale = new FloatConstraint();
            grabbableTwoGrabTransformer.InjectOptionalConstraints(twoGrabConstraints);
            grabbableScript.InjectOptionalTwoGrabTransformer(grabbableTwoGrabTransformer);
            grabbableTwoGrabTransformer.Initialize(grabbableScript);
            // HandGrabInteractable -> Rigidbody
            HandGrabInteractable handGrabInteractableScript = grabbable.GetComponent<HandGrabInteractable>();
            handGrabInteractableScript.InjectRigidbody(rb);
            // GrabInteractable -> Rigidbody
            GrabInteractable grabInteractableScript = grabbable.GetComponent<GrabInteractable>();
            grabInteractableScript.InjectRigidbody(rb);
            // PhysicsGrabbable -> Grabbable
            PhysicsGrabbable physicsGrabbableScript = grabbable.GetComponent<PhysicsGrabbable>();
            physicsGrabbableScript.InjectRigidbody(rb);
        }

        public GameObject GetWhiteboard(Vector3 targetPos)
        {
            GameObject[] whiteboards = GameObject.FindGameObjectsWithTag("Whiteboard");
            if (whiteboards.Length == 0)
            {
                Debug.Log("No whiteboards found in the scene.");
                return null;
            }
            else if (whiteboards.Length == 1)
            {
                return whiteboards[0];
            }
            else
            {
                Debug.Log("Multiple whiteboards found in the scene.");
                // if there are multiple whiteboards, use the one closest to the target position
                GameObject whiteboard = whiteboards[0];
                float minDist = Vector3.Distance(targetPos, whiteboard.transform.position);
                foreach (GameObject wb in whiteboards)
                {
                    float dist = Vector3.Distance(targetPos, wb.transform.position);
                    if (dist < minDist)
                    {
                        whiteboard = wb;
                        minDist = dist;
                    }
                }
                return whiteboard;
            }
        }

        public Vector3 GetActiveHandPos()
        {
            if (rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index))
            {
                Vector3 thumbTip = Vector3.zero;
                Vector3 indexTip = Vector3.zero;
                foreach (OVRBone bone in rightHandSkeleton.Bones)
                {
                    if (bone.Id == OVRSkeleton.BoneId.Hand_ThumbTip)
                    {
                        thumbTip = bone.Transform.position;
                        continue;
                    }
                    if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                    {
                        indexTip = bone.Transform.position;
                        continue;
                    }
                }
                Vector3 pinchPosition = (thumbTip + indexTip) / 2;
                return pinchPosition;
            }
            else if (leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index))
            {
                Vector3 thumbTip = Vector3.zero;
                Vector3 indexTip = Vector3.zero;
                foreach (OVRBone bone in leftHandSkeleton.Bones)
                {
                    if (bone.Id == OVRSkeleton.BoneId.Hand_ThumbTip)
                    {
                        thumbTip = bone.Transform.position;
                        continue;
                    }
                    if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                    {
                        indexTip = bone.Transform.position;
                        continue;
                    }
                }
                Vector3 pinchPosition = (thumbTip + indexTip) / 2;

                return pinchPosition;
            }
            else
            {
                return Vector3.zero;
            }
        }
    }
}
