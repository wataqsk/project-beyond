using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A sophisticated third-person camera controller that follows a target transform
/// with configurable rotation, distance, and obstruction handling.
/// </summary>
public class MyCharacterCamera : MonoBehaviour
{
    #region Serialized Fields

    [Header("Framing")]
    [Tooltip("The camera component being controlled")]
    public Camera Camera;
    [Tooltip("Offset from the target position for framing the subject")]
    public Vector2 FollowPointFraming = new Vector2(0f, 0f);
    [Tooltip("How sharply the camera follows the target (higher = more rigid)")]
    public float FollowingSharpness = 10000f;

    [Header("Distance")]
    [Tooltip("Default distance from target")]
    public float DefaultDistance = 6f;
    [Tooltip("Minimum allowed distance from target")]
    public float MinDistance = 0f;
    [Tooltip("Maximum allowed distance from target")]
    public float MaxDistance = 10f;
    [Tooltip("Speed of zoom in/out movement")]
    public float DistanceMovementSpeed = 5f;
    [Tooltip("Sharpness of distance adjustments (higher = more rigid)")]
    public float DistanceMovementSharpness = 10f;

    [Header("Rotation")]
    [Tooltip("Invert horizontal rotation input")]
    public bool InvertX = false;
    [Tooltip("Invert vertical rotation input")]
    public bool InvertY = false;
    [Range(-90f, 90f), Tooltip("Default vertical angle (pitch)")]
    public float DefaultVerticalAngle = 20f;
    [Range(-90f, 90f), Tooltip("Minimum vertical angle")]
    public float MinVerticalAngle = -90f;
    [Range(-90f, 90f), Tooltip("Maximum vertical angle")]
    public float MaxVerticalAngle = 90f;
    [Tooltip("Rotation speed multiplier")]
    public float RotationSpeed = 1f;
    [Tooltip("Sharpness of rotation adjustments (higher = more rigid)")]
    public float RotationSharpness = 10000f;
    [Tooltip("Should the camera rotate with physics-based movement?")]
    public bool RotateWithPhysicsMover = false;

    [Header("Obstruction")]
    [Tooltip("Radius for obstruction detection checks")]
    public float ObstructionCheckRadius = 0.2f;
    [Tooltip("Layer mask for obstruction detection")]
    public LayerMask ObstructionLayers = -1;
    [Tooltip("Sharpness for obstruction distance adjustments")]
    public float ObstructionSharpness = 10000f;
    [Tooltip("Colliders to ignore when checking for obstructions")]
    public List<Collider> IgnoredColliders = new List<Collider>();

    #endregion

    #region Public Properties

    /// <summary>
    /// The camera's transform component
    /// </summary>
    public Transform Transform { get; private set; }

    /// <summary>
    /// The transform being followed by the camera
    /// </summary>
    public Transform FollowTransform { get; private set; }

    /// <summary>
    /// Current planar (horizontal) direction vector
    /// </summary>
    public Vector3 PlanarDirection { get; set; }

    /// <summary>
    /// Target distance from the follow transform
    /// </summary>
    public float TargetDistance { get; set; }

    #endregion

    #region Private Fields

    private const int MaxObstructions = 32; // Maximum obstructions to check for

    // Distance state
    private float _currentDistance;
    private bool _distanceIsObstructed;

    // Rotation state
    private float _targetVerticalAngle;

    // Obstruction detection
    private RaycastHit _obstructionHit;
    private int _obstructionCount;
    private readonly RaycastHit[] _obstructions = new RaycastHit[MaxObstructions];
    private float _obstructionTime;

    // Follow position
    private Vector3 _currentFollowPosition;

    #endregion

    #region Unity Lifecycle Methods

    /// <summary>
    /// Validate serialized fields in the editor
    /// </summary>
    private void OnValidate()
    {
        DefaultDistance = Mathf.Clamp(DefaultDistance, MinDistance, MaxDistance);
        DefaultVerticalAngle = Mathf.Clamp(DefaultVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
    }

    /// <summary>
    /// Initialize camera state
    /// </summary>
    private void Awake()
    {
        Transform = this.transform;
        _currentDistance = DefaultDistance;
        TargetDistance = _currentDistance;
        _targetVerticalAngle = DefaultVerticalAngle;
        PlanarDirection = Vector3.forward;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set the transform that the camera will follow
    /// </summary>
    /// <param name="t">Transform to follow</param>
    public void SetFollowTransform(Transform t)
    {
        FollowTransform = t;
        PlanarDirection = FollowTransform.forward;
        _currentFollowPosition = FollowTransform.position;
    }

    /// <summary>
    /// Update camera position and rotation based on input
    /// </summary>
    /// <param name="deltaTime">Time since last frame</param>
    /// <param name="zoomInput">Zoom input value (-1 to 1)</param>
    /// <param name="rotationInput">Rotation input vector</param>
    public void UpdateWithInput(float deltaTime, float zoomInput, Vector3 rotationInput)
    {
        if (FollowTransform == null) return;

        ProcessRotationInput(deltaTime, rotationInput);
        ProcessDistanceInput(zoomInput);
        UpdateFollowPosition(deltaTime);
        HandleObstructions(deltaTime);
        UpdateCameraPosition();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Process rotation input and update camera orientation
    /// </summary>
    private void ProcessRotationInput(float deltaTime, Vector3 rotationInput)
    {
        // Apply input inversion
        if (InvertX) rotationInput.x *= -1f;
        if (InvertY) rotationInput.y *= -1f;

        // Calculate horizontal rotation
        Quaternion rotationFromInput = Quaternion.Euler(FollowTransform.up * (rotationInput.x * RotationSpeed));
        PlanarDirection = rotationFromInput * PlanarDirection;
        PlanarDirection = Vector3.Cross(FollowTransform.up, Vector3.Cross(PlanarDirection, FollowTransform.up));
        Quaternion planarRot = Quaternion.LookRotation(PlanarDirection, FollowTransform.up);

        // Calculate vertical rotation
        _targetVerticalAngle -= (rotationInput.y * RotationSpeed);
        _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
        Quaternion verticalRot = Quaternion.Euler(_targetVerticalAngle, 0, 0);

        // Smoothly interpolate to target rotation
        Quaternion targetRotation = Quaternion.Slerp(
            Transform.rotation, 
            planarRot * verticalRot, 
            1f - Mathf.Exp(-RotationSharpness * deltaTime)
        );

        Transform.rotation = targetRotation;
    }

    /// <summary>
    /// Process zoom input and update target distance
    /// </summary>
    private void ProcessDistanceInput(float zoomInput)
    {
        // If zooming while obstructed, reset target distance to current
        if (_distanceIsObstructed && Mathf.Abs(zoomInput) > 0f)
        {
            TargetDistance = _currentDistance;
        }

        // Update target distance with clamping
        TargetDistance += zoomInput * DistanceMovementSpeed;
        TargetDistance = Mathf.Clamp(TargetDistance, MinDistance, MaxDistance);
    }

    /// <summary>
    /// Update the smoothed follow position
    /// </summary>
    private void UpdateFollowPosition(float deltaTime)
    {
        _currentFollowPosition = Vector3.Lerp(
            _currentFollowPosition, 
            FollowTransform.position, 
            1f - Mathf.Exp(-FollowingSharpness * deltaTime)
        );
    }

    /// <summary>
    /// Handle camera obstructions by adjusting distance
    /// </summary>
    private void HandleObstructions(float deltaTime)
    {
        RaycastHit closestHit = new RaycastHit { distance = Mathf.Infinity };
        
        // Check for obstructions
        _obstructionCount = Physics.SphereCastNonAlloc(
            _currentFollowPosition,
            ObstructionCheckRadius,
            -Transform.forward,
            _obstructions,
            TargetDistance,
            ObstructionLayers,
            QueryTriggerInteraction.Ignore
        );

        // Find closest valid obstruction
        for (int i = 0; i < _obstructionCount; i++)
        {
            if (IsColliderIgnored(_obstructions[i].collider)) continue;
            if (_obstructions[i].distance < closestHit.distance && _obstructions[i].distance > 0)
            {
                closestHit = _obstructions[i];
            }
        }

        // Adjust distance based on obstructions
        if (closestHit.distance < Mathf.Infinity)
        {
            _distanceIsObstructed = true;
            _currentDistance = Mathf.Lerp(
                _currentDistance, 
                closestHit.distance, 
                1 - Mathf.Exp(-ObstructionSharpness * deltaTime)
            );
        }
        else
        {
            _distanceIsObstructed = false;
            _currentDistance = Mathf.Lerp(
                _currentDistance, 
                TargetDistance, 
                1 - Mathf.Exp(-DistanceMovementSharpness * deltaTime)
            );
        }
    }

    /// <summary>
    /// Check if a collider is in the ignored list
    /// </summary>
    private bool IsColliderIgnored(Collider collider)
    {
        // Using foreach is more readable and performance difference is negligible for small lists
        foreach (var ignoredCollider in IgnoredColliders)
        {
            if (ignoredCollider == collider) return true;
        }
        return false;
    }

    /// <summary>
    /// Update the camera's final position with framing offsets
    /// </summary>
    private void UpdateCameraPosition()
    {
        // Calculate base position
        Vector3 targetPosition = _currentFollowPosition - (Transform.forward * _currentDistance);

        // Apply framing offsets
        targetPosition += Transform.right * FollowPointFraming.x;
        targetPosition += Transform.up * FollowPointFraming.y;

        Transform.position = targetPosition;
    }

    #endregion
}