using UnityEngine;

/// Third-person camera controller with smooth follow, rotation, and zoom functionality
public class PlayerCamera : MonoBehaviour
{
    [Header("Distance Settings")]
    [SerializeField, Range(1f, 20f)] private float _defaultDistance = 6f;
    [SerializeField, Range(1f, 10f)] private float _minDistance = 3f;
    [SerializeField, Range(5f, 20f)] private float _maxDistance = 10f;
    
    [Header("Movement Settings")]
    [SerializeField, Min(0.1f)] private float _distanceMovementSpeed = 5f;
    [SerializeField, Min(1f)] private float _distanceMovementSharpness = 10f;
    [SerializeField, Min(1f)] private float _rotationSpeed = 10f;
    [SerializeField, Min(1f)] private float _rotationSharpness = 10000f;
    [SerializeField, Min(1f)] private float _followSharpness = 10000f;
    
    [Header("Vertical Angle Settings")]
    [SerializeField, Range(-90f, 0f)] private float _minVerticalAngle = -90f;
    [SerializeField, Range(0f, 90f)] private float _maxVerticalAngle = 90f;
    [SerializeField] private float _defaultVerticalAngle = 20f;

    // Runtime variables
    private Transform _followTransform;
    private Vector3 _currentFollowPosition;
    private Vector3 _planarDirection;
    private float _targetVerticalAngle;
    private float _currentDistance;
    private float _targetDistance;

    private void Awake()
    {
        InitializeCamera();
    }

    /// <summary>
    /// Initializes camera with default values
    /// </summary>
    private void InitializeCamera()
    {
        _currentDistance = _defaultDistance;
        _targetDistance = _defaultDistance;
        _targetVerticalAngle = _defaultVerticalAngle;
        _planarDirection = Vector3.forward;
    }

    /// <summary>
    /// Sets the transform for the camera to follow
    /// </summary>
    public void SetFollowTransform(Transform targetTransform)
    {
        _followTransform = targetTransform;
        _currentFollowPosition = targetTransform.position;
        _planarDirection = targetTransform.forward;
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    /// <summary>
    /// Ensures all settings stay within valid ranges
    /// </summary>
    private void ValidateSettings()
    {
        _defaultDistance = Mathf.Clamp(_defaultDistance, _minDistance, _maxDistance);
        _defaultVerticalAngle = Mathf.Clamp(_defaultVerticalAngle, _minVerticalAngle, _maxVerticalAngle);
    }

    /// <summary>
    /// Updates camera with input for current frame
    /// </summary>
    public void UpdateWithInput(float deltaTime, float zoomInput, Vector3 rotationInput)
    {
        if (!_followTransform) return;

        HandleRotationInput(deltaTime, rotationInput, out Quaternion targetRotation);
        HandlePosition(deltaTime, zoomInput, targetRotation);
    }

    /// <summary>
    /// Processes rotation input and calculates target rotation
    /// </summary>
    private void HandleRotationInput(float deltaTime, Vector3 rotationInput, out Quaternion targetRotation)
    {
        // Horizontal rotation around follow target's up axis
        Quaternion horizontalRotation = Quaternion.Euler(_followTransform.up * (rotationInput.x * _rotationSpeed));
        _planarDirection = horizontalRotation * _planarDirection;
        Quaternion planarRotation = Quaternion.LookRotation(_planarDirection, _followTransform.up);

        // Vertical rotation with angle clamping
        _targetVerticalAngle = Mathf.Clamp(
            _targetVerticalAngle - (rotationInput.y * _rotationSpeed),
            _minVerticalAngle,
            _maxVerticalAngle);
        
        Quaternion verticalRotation = Quaternion.Euler(_targetVerticalAngle, 0, 0);

        // Smoothly interpolate to target rotation
        targetRotation = Quaternion.Slerp(
            transform.rotation,
            planarRotation * verticalRotation,
            Mathf.Exp(-_rotationSharpness * deltaTime));
        
        transform.rotation = targetRotation;
    }
    
    /// <summary>
    /// Handles camera position and zoom movement
    /// </summary>
    private void HandlePosition(float deltaTime, float zoomInput, Quaternion targetRotation)
    {
        UpdateZoomDistance(zoomInput);
        UpdateFollowPosition(deltaTime);
        
        Vector3 targetPosition = CalculateTargetPosition(targetRotation);
        transform.position = targetPosition;
    }

    /// <summary>
    /// Updates target zoom distance based on input
    /// </summary>
    private void UpdateZoomDistance(float zoomInput)
    {
        _targetDistance = Mathf.Clamp(
            _targetDistance + zoomInput * _distanceMovementSpeed,
            _minDistance,
            _maxDistance);
        
        _currentDistance = Mathf.Lerp(
            _currentDistance,
            _targetDistance,
            Mathf.Exp(-_distanceMovementSharpness * Time.deltaTime));
    }

    /// <summary>
    /// Smoothly updates camera follow position
    /// </summary>
    private void UpdateFollowPosition(float deltaTime)
    {
        _currentFollowPosition = Vector3.Lerp(
            _currentFollowPosition,
            _followTransform.position,
            Mathf.Exp(-_followSharpness * deltaTime));
    }

    /// <summary>
    /// Calculates target position behind follow target
    /// </summary>
    private Vector3 CalculateTargetPosition(Quaternion targetRotation)
    {
        return _currentFollowPosition - (targetRotation * Vector3.forward * _currentDistance);
    }
}