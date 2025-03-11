using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// This class is responsible for managing the 2D drawing functionality.
/// Specifically, it will lock the rotation and 1-D position of the object to realize the 'stick' effect for drawing.
/// i.e. the object will only be able to move in 2D space on top of an object like a canvas.
/// When the hand is moving away from the canvas, it will release the object to allow the object to adjust its pose in 3D space.
/// </summary>
public class DrawStickManager : MonoBehaviour
{
    private bool _stickToParent;
    private Transform handGrab; // the object where the building block of grabbing is attached to
    private bool _isGrabbing;
    private float distanceToRelease = 0.025f; // the threshold distance to release the object from parent
    private GameObject grabPoint; // the grab point stick to the object, re-positioned when the object is grabbed
    private int StickCD = 0; // reserve several frames for the object to stick to the parent again after release
    // Start is called before the first frame update
    void Start()
    {
        _stickToParent = false;
        _isGrabbing = false;
        handGrab = transform.Find("HandGrab");
        grabPoint = new GameObject("InitAPoint");
        grabPoint.transform.parent = this.transform;
    }

    void Update()
    {
        if (isGrabbed() && !_isGrabbing)
        {
            grabPoint.transform.position = GetGrabPointPos();
            _isGrabbing = true;
        }
        if (!isGrabbed() && _isGrabbing)
            _isGrabbing = false;


        // if the object is grabbed and sticked to parent, check condition to release it
        if (_isGrabbing && _stickToParent)
        {
            // release when the hand is reasonably farther from the grab point, i.e., distance is larger than the threshold
            // and the hand is moving away from the touch point, i.e., the vector from the hand to the touch point is opposite to the forward direction of parent object
            Vector3 handGrabPos = GetGrabPointPos();
            Vector3 objGrabPos = grabPoint.transform.position;
            if (Vector3.Distance(handGrabPos, objGrabPos) > distanceToRelease && (transform.parent == null || Vector3.Dot(grabPoint.transform.position - GetGrabPointPos(), transform.parent.forward) < 0))
            {
                ReleaseFromParent();
            }
        }
        if (StickCD > 0)
        {
            StickCD--;
        }
    }

    public bool GetStickToParent()
    {
        return _stickToParent;
    }

    public bool isGrabbed()
    {
        return handGrab.GetComponent<Grabbable>().GrabPoints.Count > 0;
    }

    Vector3 GetGrabPointPos()
    {
        // the position of the grab point, which is the position between the pinch fingers, not sticked to the object, always move with fingers
        if (!isGrabbed()) return Vector3.zero;
        Pose pose = handGrab.GetComponent<Grabbable>().GrabPoints[0];
        return pose.position;
    }

    public void StickToParent()
    {
        if (_stickToParent || StickCD > 0)
        {
            return;
        }
        // attach the script OneGrabTranslateTransformer to the Grabbable component
        GameObject handGrabObj = handGrab.gameObject;
        Grabbable grabbableScript = handGrabObj.GetComponent<Grabbable>();
        bool getTransformer = handGrabObj.TryGetComponent(out OneGrabFreeTransformerInject constraintTransformer);
        if (!getTransformer)
        {
            constraintTransformer = handGrabObj.AddComponent<OneGrabFreeTransformerInject>();
        }
        // Create a new instance of the constraints, lock the rotation and 1-D position of z-axis
        var posConstraints = new TransformerUtils.PositionConstraints
        {
            XAxis = new TransformerUtils.ConstrainedAxis(),
            YAxis = new TransformerUtils.ConstrainedAxis(),
            ZAxis = new TransformerUtils.ConstrainedAxis()
        };
        posConstraints.ZAxis.ConstrainAxis = true;
        posConstraints.ZAxis.AxisRange.Min = transform.localPosition.z;
        posConstraints.ZAxis.AxisRange.Max = transform.localPosition.z;
        var rotConstraints = new TransformerUtils.RotationConstraints
        {
            XAxis = new TransformerUtils.ConstrainedAxis(),
            YAxis = new TransformerUtils.ConstrainedAxis(),
            ZAxis = new TransformerUtils.ConstrainedAxis()
        };
        rotConstraints.XAxis.ConstrainAxis = true;
        rotConstraints.XAxis.AxisRange.Min = transform.rotation.eulerAngles.x;
        rotConstraints.XAxis.AxisRange.Max = transform.rotation.eulerAngles.x;
        rotConstraints.YAxis.ConstrainAxis = true;
        rotConstraints.YAxis.AxisRange.Min = transform.rotation.eulerAngles.y;
        rotConstraints.YAxis.AxisRange.Max = transform.rotation.eulerAngles.y;
        rotConstraints.ZAxis.ConstrainAxis = true;
        rotConstraints.ZAxis.AxisRange.Min = transform.rotation.eulerAngles.z;
        rotConstraints.ZAxis.AxisRange.Max = transform.rotation.eulerAngles.z;

        constraintTransformer.InjectOptionalConstraints(posConstraints, rotConstraints);
        // apply the constraints to the transformer
        constraintTransformer.Initialize(grabbableScript);
        _stickToParent = true;
        Debug.Log("Stick to whiteboard");
    }

    public void ReleaseFromParent()
    {
        if (!_stickToParent)
        {
            return;
        }
        // attach the script OneGrabTranslateTransformer to the Grabbable component
        GameObject handGrab = transform.Find("HandGrab").gameObject;
        Grabbable grabbableScript = handGrab.GetComponent<Grabbable>();
        bool GetTransformer = handGrab.TryGetComponent(out OneGrabFreeTransformerInject constraintTransformer);
        if (!GetTransformer)
        {
            constraintTransformer = handGrab.AddComponent<OneGrabFreeTransformerInject>();
        }
        // Create a new instance of the constraints
        var posConstraints = new TransformerUtils.PositionConstraints
        {
            XAxis = new TransformerUtils.ConstrainedAxis(),
            YAxis = new TransformerUtils.ConstrainedAxis(),
            ZAxis = new TransformerUtils.ConstrainedAxis()
        };
        var rotConstraints = new TransformerUtils.RotationConstraints
        {
            XAxis = new TransformerUtils.ConstrainedAxis(),
            YAxis = new TransformerUtils.ConstrainedAxis(),
            ZAxis = new TransformerUtils.ConstrainedAxis()
        };
        constraintTransformer.InjectOptionalConstraints(posConstraints, rotConstraints);
        constraintTransformer.Initialize(grabbableScript);
        _stickToParent = false;
        StickCD = 2;
        Debug.Log("Release from whiteboard");
    }
}
