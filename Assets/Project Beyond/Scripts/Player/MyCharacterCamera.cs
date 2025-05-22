using System.Collections.Generic;
using UnityEngine;

public class MyCharacterCamera : MonoBehaviour
{
    [Header("Framing")]
    public Camera Camera;
    public Vector2 FollowPointFraming = new Vector2(0f, 0f);
    public float FollowingSharpness = 10000f;

    [Header("Distance")]
    public float DefaultDistance = 6f;
    public float MinDistance = 0f;
    public float MaxDistance = 10f;
    public float DistanceMovementSpeed = 5f;
    public float DistanceMovementSharpness = 10f;

    [Header("Rotation")]
    public bool InvertX = false;
    public bool InvertY = false;
    [Range(-90f, 90f)] public float DefaultVerticalAngle = 20f;
    [Range(-90f, 90f)] public float MinVerticalAngle = -90f;
    [Range(-90f, 90f)] public float MaxVerticalAngle = 90f;
    public float RotationSpeed = 1f;
    public float RotationSharpness = 10000f;
    public bool RotateWithPhysicsMover = false;

    [Header("Obstruction")]
    public float ObstructionCheckRadius = 0.2f;
    public LayerMask ObstructionLayers = -1;
    public float ObstructionSharpness = 10000f;
    public List<Collider> IgnoredColliders = new List<Collider>();

    public Transform Transform { get; private set; }
    public Transform FollowTransform { get; private set; }
    public Vector3 PlanarDirection { get; set; }
    public float TargetDistance { get; set; }

    private const int MaxObstructions = 32;
    private bool _distanceIsObstructed;
    private float _currentDistance;
    private float _targetVerticalAngle;
    private RaycastHit _obstructionHit;
    private int _obstructionCount;
    private RaycastHit[] _obstructions = new RaycastHit[MaxObstructions];
    private Vector3 _currentFollowPosition;

    private void OnValidate()
    {
        DefaultDistance = Mathf.Clamp(DefaultDistance, MinDistance, MaxDistance);
        DefaultVerticalAngle = Mathf.Clamp(DefaultVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
    }

    private void Awake()
    {
        Transform = transform;
        _currentDistance = DefaultDistance;
        TargetDistance = _currentDistance;
        _targetVerticalAngle = 0f;
        PlanarDirection = Vector3.forward;
    }

    public void SetFollowTransform(Transform t)
    {
        FollowTransform = t;
        PlanarDirection = FollowTransform.forward;
        _currentFollowPosition = FollowTransform.position;
    }

    public void UpdateWithInput(float deltaTime, float zoomInput, Vector3 rotationInput)
    {
        if (!FollowTransform) return;

        ProcessRotationInput(rotationInput);
        ProcessZoomInput(zoomInput);
        UpdateFollowPosition(deltaTime);
        HandleObstructions(deltaTime);
        UpdateCameraPosition(deltaTime);
    }

    private void ProcessRotationInput(Vector3 rotationInput)
    {
        if (InvertX) rotationInput.x *= -1f;
        if (InvertY) rotationInput.y *= -1f;

        Quaternion rotationFromInput = Quaternion.Euler(FollowTransform.up * (rotationInput.x * RotationSpeed));
        PlanarDirection = rotationFromInput * PlanarDirection;
        PlanarDirection = Vector3.Cross(FollowTransform.up, Vector3.Cross(PlanarDirection, FollowTransform.up));

        _targetVerticalAngle -= rotationInput.y * RotationSpeed;
        _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, MinVerticalAngle, MaxVerticalAngle);

        Quaternion planarRot = Quaternion.LookRotation(PlanarDirection, FollowTransform.up);
        Quaternion verticalRot = Quaternion.Euler(_targetVerticalAngle, 0, 0);
        Quaternion targetRotation = Quaternion.Slerp(Transform.rotation, planarRot * verticalRot,
            1f - Mathf.Exp(-RotationSharpness * Time.deltaTime));

        Transform.rotation = targetRotation;
    }

    private void ProcessZoomInput(float zoomInput)
    {
        if (_distanceIsObstructed && Mathf.Abs(zoomInput) > 0f)
        {
            TargetDistance = _currentDistance;
        }

        TargetDistance += zoomInput * DistanceMovementSpeed;
        TargetDistance = Mathf.Clamp(TargetDistance, MinDistance, MaxDistance);
    }

    private void UpdateFollowPosition(float deltaTime)
    {
        _currentFollowPosition = Vector3.Lerp(_currentFollowPosition, FollowTransform.position,
            1f - Mathf.Exp(-FollowingSharpness * deltaTime));
    }

    private void HandleObstructions(float deltaTime)
    {
        RaycastHit closestHit = new RaycastHit { distance = Mathf.Infinity };
        _obstructionCount = Physics.SphereCastNonAlloc(_currentFollowPosition, ObstructionCheckRadius,
            -Transform.forward, _obstructions, TargetDistance, ObstructionLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < _obstructionCount; i++)
        {
            if (IsColliderIgnored(_obstructions[i].collider)) continue;
            if (_obstructions[i].distance < closestHit.distance && _obstructions[i].distance > 0)
            {
                closestHit = _obstructions[i];
            }
        }

        if (closestHit.distance < Mathf.Infinity)
        {
            _distanceIsObstructed = true;
            _currentDistance = Mathf.Lerp(_currentDistance, closestHit.distance,
                1 - Mathf.Exp(-ObstructionSharpness * deltaTime));
        }
        else
        {
            _distanceIsObstructed = false;
            _currentDistance = Mathf.Lerp(_currentDistance, TargetDistance,
                1 - Mathf.Exp(-DistanceMovementSharpness * deltaTime));
        }
    }

    private bool IsColliderIgnored(Collider collider)
    {
        foreach (var ignoredCollider in IgnoredColliders)
        {
            if (ignoredCollider == collider)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateCameraPosition(float deltaTime)
    {
        Vector3 targetPosition = _currentFollowPosition - (Transform.forward * _currentDistance);
        targetPosition += Transform.right * FollowPointFraming.x;
        targetPosition += Transform.up * FollowPointFraming.y;
        Transform.position = targetPosition;
    }
}