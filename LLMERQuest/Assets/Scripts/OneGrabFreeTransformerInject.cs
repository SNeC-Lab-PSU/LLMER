using Oculus.Interaction;
using UnityEngine;

/*
 * This script is modified from the original OneGrabFreeTransformer.cs script in Oculus Interactions SDK.
 * to allow for dependency injection of the position and rotation constraints.
 */
public class OneGrabFreeTransformerInject : MonoBehaviour, ITransformer
{
    [SerializeField]
    private TransformerUtils.PositionConstraints _positionConstraints =
        new TransformerUtils.PositionConstraints()
        {
            XAxis = new TransformerUtils.ConstrainedAxis(),
            YAxis = new TransformerUtils.ConstrainedAxis(),
            ZAxis = new TransformerUtils.ConstrainedAxis()
        };

    [SerializeField]
    private TransformerUtils.RotationConstraints _rotationConstraints =
        new TransformerUtils.RotationConstraints()
        {
            XAxis = new TransformerUtils.ConstrainedAxis(),
            YAxis = new TransformerUtils.ConstrainedAxis(),
            ZAxis = new TransformerUtils.ConstrainedAxis()
        };


    private IGrabbable _grabbable;
    private Pose _grabDeltaInLocalSpace;
    private TransformerUtils.PositionConstraints _parentConstraints;

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
        Vector3 initialPosition = _grabbable.Transform.localPosition;
        _parentConstraints = TransformerUtils.GenerateParentConstraints(_positionConstraints, initialPosition);
    }

    public void BeginTransform()
    {
        Pose grabPoint = _grabbable.GrabPoints[0];
        var targetTransform = _grabbable.Transform;
        _grabDeltaInLocalSpace = new Pose(targetTransform.InverseTransformVector(grabPoint.position - targetTransform.position),
                                        Quaternion.Inverse(grabPoint.rotation) * targetTransform.rotation);
    }

    public void UpdateTransform()
    {
        Pose grabPoint = _grabbable.GrabPoints[0];
        var targetTransform = _grabbable.Transform;

        // Constrain rotation
        Quaternion updatedRotation = grabPoint.rotation * _grabDeltaInLocalSpace.rotation;
        targetTransform.rotation = TransformerUtils.GetConstrainedTransformRotation(updatedRotation, _rotationConstraints);

        // Constrain position
        Vector3 updatedPosition = grabPoint.position - targetTransform.TransformVector(_grabDeltaInLocalSpace.position);
        targetTransform.position = TransformerUtils.GetConstrainedTransformPosition(updatedPosition, _parentConstraints, targetTransform.parent);
    }

    public void EndTransform() { }

    #region Inject

    public void InjectOptionalConstraints(TransformerUtils.PositionConstraints positionConstraints, TransformerUtils.RotationConstraints rotationConstraints)
    {
        _positionConstraints = positionConstraints;
        _rotationConstraints = rotationConstraints;
    }

    #endregion
}
