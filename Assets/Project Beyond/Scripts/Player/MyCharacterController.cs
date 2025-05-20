using KinematicCharacterController;
using UnityEngine;

/// <summary>
/// Input structure for character movement
/// </summary>
public struct PlayerCharacterInputs
{
    public float MoveAxisForward;
    public float MoveAxisRight;
    public Quaternion CameraRotation;
    public bool JumpDown;
}

/// <summary>
/// A kinematic character controller implementing the ICharacterController interface
/// </summary>
public class MyCharacterController : MonoBehaviour, ICharacterController
{
    #region Serialized Fields

    [Header("References")]
    [Tooltip("The kinematic character motor component")]
    public KinematicCharacterMotor Motor;
    [Tooltip("Root transform for character mesh")]
    public Transform MeshRoot;

    [Header("Stable Movement")]
    [Tooltip("Maximum speed when grounded")]
    public float MaxStableMoveSpeed = 10f;
    [Tooltip("Sharpness for ground movement (higher = more responsive)")]
    public float StableMovementSharpness = 15;
    [Tooltip("Sharpness for character orientation (higher = faster rotation)")]
    public float OrientationSharpness = 10;

    [Header("Air Movement")]
    [Tooltip("Maximum speed when airborne")]
    public float MaxAirMoveSpeed = 10f;
    [Tooltip("Acceleration speed in air")]
    public float AirAccelerationSpeed = 5f;
    [Tooltip("Air resistance/drag coefficient")]
    public float Drag = 0.1f;

    [Header("Jumping")]
    [Tooltip("Allow jumping when sliding on slopes")]
    public bool AllowJumpingWhenSliding = true;
    [Tooltip("Initial vertical speed when jumping")]
    public float JumpSpeed = 10f;
    [Tooltip("Grace period before landing to still allow jumping")]
    public float JumpPreGroundingGraceTime = 0.1f;
    [Tooltip("Grace period after leaving ground to still allow jumping")]
    public float JumpPostGroundingGraceTime = 0.1f;

    [Header("Misc")]
    [Tooltip("Gravity force vector")]
    public Vector3 Gravity = new Vector3(0, -30f, 0);

    #endregion

    #region Private Fields

    // Movement state
    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;

    // Jumping state
    private bool _jumpRequested = false;
    private bool _jumpConsumed = false;
    private bool _jumpedThisFrame = false;
    private float _timeSinceJumpRequested = Mathf.Infinity;
    private float _timeSinceLastAbleToJump = 0f;

    #endregion

    #region Unity Lifecycle Methods

    private void Start()
    {
        Motor.CharacterController = this;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets character inputs from external source
    /// </summary>
    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        ProcessMovementInput(inputs);
        ProcessJumpInput(inputs);
    }

    #endregion

    #region ICharacterController Implementation

    public void BeforeCharacterUpdate(float deltaTime) { }

    /// <summary>
    /// Updates character rotation based on input
    /// </summary>
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
        {
            Vector3 smoothedLookInputDirection = Vector3.Slerp(
                Motor.CharacterForward, 
                _lookInputVector, 
                1 - Mathf.Exp(-OrientationSharpness * deltaTime)
            ).normalized;
            
            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
        }
    }

    /// <summary>
    /// Updates character velocity based on input and state
    /// </summary>
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Vector3 targetMovementVelocity = CalculateTargetVelocity(deltaTime, currentVelocity);
        ApplyMovement(ref currentVelocity, targetMovementVelocity, deltaTime);
        HandleJumping(ref currentVelocity, deltaTime);  // Now passing deltaTime
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        UpdateJumpState(deltaTime);
    }

    // Remaining interface methods with default implementations
    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void PostGroundingUpdate(float deltaTime) { }
    public void AddVelocity(Vector3 velocity) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }

    #endregion

    #region Private Methods

    /// <summary>
    /// Processes movement input and converts to world space
    /// </summary>
    private void ProcessMovementInput(PlayerCharacterInputs inputs)
    {
        // Clamp and normalize input
        Vector3 moveInputVector = Vector3.ClampMagnitude(
            new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 
            1f
        );

        // Convert input to camera space
        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(
            inputs.CameraRotation * Vector3.forward, 
            Motor.CharacterUp
        ).normalized;

        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(
                inputs.CameraRotation * Vector3.up, 
                Motor.CharacterUp
            ).normalized;
        }

        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

        _moveInputVector = cameraPlanarRotation * moveInputVector;
        _lookInputVector = cameraPlanarDirection;
    }

    /// <summary>
    /// Processes jump input
    /// </summary>
    private void ProcessJumpInput(PlayerCharacterInputs inputs)
    {
        if (inputs.JumpDown)
        {
            _timeSinceJumpRequested = 0f;
            _jumpRequested = true;
        }
    }

    /// <summary>
    /// Calculates target velocity based on movement state
    /// </summary>
    private Vector3 CalculateTargetVelocity(float deltaTime, Vector3 currentVelocity)
    {
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            // Handle ground movement
            currentVelocity = Motor.GetDirectionTangentToSurface(
                currentVelocity, 
                Motor.GroundingStatus.GroundNormal
            ) * currentVelocity.magnitude;

            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(
                Motor.GroundingStatus.GroundNormal, 
                inputRight
            ).normalized * _moveInputVector.magnitude;

            return reorientedInput * MaxStableMoveSpeed;
        }
        else
        {
            // Handle air movement
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 targetVelocity = _moveInputVector * MaxAirMoveSpeed;

                if (Motor.GroundingStatus.FoundAnyGround)
                {
                    Vector3 perpenticularObstructionNormal = Vector3.Cross(
                        Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), 
                        Motor.CharacterUp
                    ).normalized;
                    
                    targetVelocity = Vector3.ProjectOnPlane(
                        targetVelocity, 
                        perpenticularObstructionNormal
                    );
                }

                return targetVelocity;
            }
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Applies movement forces to the character
    /// </summary>
    private void ApplyMovement(ref Vector3 currentVelocity, Vector3 targetMovementVelocity, float deltaTime)
    {
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            // Ground movement
            currentVelocity = Vector3.Lerp(
                currentVelocity, 
                targetMovementVelocity, 
                1 - Mathf.Exp(-StableMovementSharpness * deltaTime)
            );
        }
        else
        {
            // Air movement
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 velocityDiff = Vector3.ProjectOnPlane(
                    targetMovementVelocity - currentVelocity, 
                    Gravity
                );
                
                currentVelocity += velocityDiff * AirAccelerationSpeed * deltaTime;
            }

            // Apply gravity and drag
            currentVelocity += Gravity * deltaTime;
            currentVelocity *= (1f / (1f + (Drag * deltaTime)));
        }
    }

    /// <summary>
    /// Handles jumping logic
    /// </summary>
    private void HandleJumping(ref Vector3 currentVelocity, float deltaTime)
    {
        _jumpedThisFrame = false;
        _timeSinceJumpRequested += deltaTime;

        if (!_jumpRequested) return;

        bool canJump = !_jumpConsumed && 
            ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || 
            _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime);

        if (canJump)
        {
            Vector3 jumpDirection = Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround
                ? Motor.GroundingStatus.GroundNormal
                : Motor.CharacterUp;

            Motor.ForceUnground(0.1f);
            currentVelocity += (jumpDirection * JumpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
            
            _jumpRequested = false;
            _jumpConsumed = true;
            _jumpedThisFrame = true;
        }
    }

    /// <summary>
    /// Updates jump state after movement
    /// </summary>
    private void UpdateJumpState(float deltaTime)
    {
        // Clear jump request if grace period expired
        if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
        {
            _jumpRequested = false;
        }

        // Update jump availability
        bool isGrounded = AllowJumpingWhenSliding 
            ? Motor.GroundingStatus.FoundAnyGround 
            : Motor.GroundingStatus.IsStableOnGround;

        if (isGrounded)
        {
            if (!_jumpedThisFrame)
            {
                _jumpConsumed = false;
            }
            _timeSinceLastAbleToJump = 0f;
        }
        else
        {
            _timeSinceLastAbleToJump += deltaTime;
        }
    }

    #endregion
}