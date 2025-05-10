using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// Contains all player movement input data
/// </summary>
public struct PlayerInputs
{
    public float MoveAxisForward;  // W/S or Up/Down input (-1 to 1)
    public float MoveAxisRight;    // A/D or Left/Right input (-1 to 1)
    public Quaternion CameraRotation; // Current camera orientation
    public bool JumpPressed;       // Whether jump was pressed this frame
}

/// <summary>
/// Handles character movement physics using KinematicCharacterMotor
/// </summary>
public class CharacterController : MonoBehaviour, ICharacterController
{
    [Header("Movement Settings")]
    [SerializeField] private KinematicCharacterMotor _motor;
    [SerializeField] private float _maxStableMoveSpeed = 10f;
    [SerializeField] private float _stableMovementSharpness = 15f;
    [SerializeField] private float _orientationSharpness = 10f;
    
    [Header("Jump Settings")] 
    [SerializeField] private float _jumpSpeed = 10f;
    [SerializeField] private Vector3 _gravity = new Vector3(0f, -30f, 0f);

    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private bool _jumpRequested;

    private void Start()
    {
        _motor.CharacterController = this;
    }

    /// <summary>
    /// Processes movement input and converts to world-space vectors
    /// </summary>
    public void SetInputs(ref PlayerInputs inputs)
    {
        // Normalize and clamp movement input
        Vector3 moveInputVector = Vector3.ClampMagnitude(
            new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

        // Calculate camera-relative movement direction
        Vector3 cameraPlanarDirection = GetCameraPlanarDirection(inputs.CameraRotation);
        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, _motor.CharacterUp);

        // Store processed movement vectors
        _moveInputVector = cameraPlanarRotation * moveInputVector;
        _lookInputVector = _moveInputVector.normalized;

        // Handle jump input
        if (inputs.JumpPressed)
        {
            _jumpRequested = true;
        }
    }

    /// <summary>
    /// Gets the camera's forward direction projected onto movement plane
    /// </summary>
    private Vector3 GetCameraPlanarDirection(Quaternion cameraRotation)
    {
        Vector3 direction = Vector3.ProjectOnPlane(
            cameraRotation * Vector3.forward, 
            _motor.CharacterUp).normalized;

        // Fallback to camera up if forward is parallel to character up
        if (direction.sqrMagnitude == 0f)
        {
            direction = Vector3.ProjectOnPlane(
                cameraRotation * Vector3.up, 
                _motor.CharacterUp).normalized;
        }

        return direction;
    }

    /// <summary>
    /// Smoothly rotates character to face movement direction
    /// </summary>
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_lookInputVector.sqrMagnitude > 0f && _orientationSharpness > 0f)
        {
            Vector3 smoothedDirection = Vector3.Slerp(
                _motor.CharacterForward, 
                _lookInputVector, 
                1 - Mathf.Exp(-_orientationSharpness * deltaTime)).normalized;

            currentRotation = Quaternion.LookRotation(smoothedDirection, _motor.CharacterUp);
        }
    }

    /// <summary>
    /// Calculates character velocity based on input and physics
    /// </summary>
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (_motor.GroundingStatus.IsStableOnGround)
        {
            HandleGroundedMovement(ref currentVelocity, deltaTime);
        }
        else
        {
            // Apply gravity when airborne
            currentVelocity += _gravity * deltaTime;
        }

        HandleJump(ref currentVelocity);
    }

    /// <summary>
    /// Handles movement while grounded
    /// </summary>
    private void HandleGroundedMovement(ref Vector3 currentVelocity, float deltaTime)
    {
        float currentSpeed = currentVelocity.magnitude;
        Vector3 groundNormal = _motor.GroundingStatus.GroundNormal;

        // Maintain velocity along ground surface
        currentVelocity = _motor.GetDirectionTangentToSurface(
            currentVelocity, 
            groundNormal) * currentSpeed;

        // Reorient input based on ground slope
        Vector3 inputRight = Vector3.Cross(_moveInputVector, _motor.CharacterUp);
        Vector3 reorientedInput = Vector3.Cross(
            groundNormal, 
            inputRight).normalized * _moveInputVector.magnitude;

        // Calculate and smoothly apply target velocity
        Vector3 targetVelocity = reorientedInput * _maxStableMoveSpeed;
        currentVelocity = Vector3.Lerp(
            currentVelocity, 
            targetVelocity,
            1f - Mathf.Exp(-_stableMovementSharpness * deltaTime));
    }

    /// <summary>
    /// Handles jump mechanics
    /// </summary>
    private void HandleJump(ref Vector3 currentVelocity)
    {
        if (_jumpRequested)
        {
            // Apply vertical jump speed while preserving horizontal velocity
            currentVelocity += (_motor.CharacterUp * _jumpSpeed) - 
                             Vector3.Project(currentVelocity, _motor.CharacterUp);
            _jumpRequested = false;
            _motor.ForceUnground();
        }
    }

    // Kinematic Character Controller interface methods
    public void AfterCharacterUpdate(float deltaTime) { }
    public void BeforeCharacterUpdate(float deltaTime) { }
    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void PostGroundingUpdate(float deltaTime) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
}