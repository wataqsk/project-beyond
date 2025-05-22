using UnityEngine;
using KinematicCharacterController;

public struct PlayerCharacterInputs
{
    public float MoveAxisForward;
    public float MoveAxisRight;
    public Quaternion CameraRotation;
    public bool JumpDown;
    public bool CrouchDown;
    public bool CrouchUp;
}

public class MyCharacterController : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor;
    public Transform MeshRoot;

    [Header("Movement")]
    public float MaxStableMoveSpeed = 10f;
    public float StableMovementSharpness = 15;
    public float OrientationSharpness = 10;
    public float MaxAirMoveSpeed = 10f;
    public float AirAccelerationSpeed = 5f;
    public float Drag = 0.1f;

    [Header("Jumping")]
    public bool AllowJumpingWhenSliding = false;
    public bool AllowDoubleJump = false;
    public bool AllowWallJump = false;
    public float JumpSpeed = 10f;
    public float JumpPreGroundingGraceTime = 0f;
    public float JumpPostGroundingGraceTime = 0f;

    [Header("Gravity")]
    public Vector3 Gravity = new Vector3(0, -30f, 0);
    public bool OrientTowardsGravity = true;

    private Collider[] _probedColliders = new Collider[8];
    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private bool _jumpRequested = false;
    private bool _jumpConsumed = false;
    private bool _jumpedThisFrame = false;
    private float _timeSinceJumpRequested = Mathf.Infinity;
    private float _timeSinceLastAbleToJump = 0f;
    private bool _doubleJumpConsumed = false;
    private bool _canWallJump = false;
    private Vector3 _wallJumpNormal;
    private float deltaTime;
    private Vector3 _internalVelocityAdd = Vector3.zero;
    private bool _shouldBeCrouching = false;
    private bool _isCrouching = false;

    private void Start() => Motor.CharacterController = this;

    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
            cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;

        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

        _moveInputVector = cameraPlanarRotation * moveInputVector;
        _lookInputVector = cameraPlanarDirection;

        if (inputs.JumpDown)
        {
            _timeSinceJumpRequested = 0f;
            _jumpRequested = true;
        }

        if (inputs.CrouchDown)
        {
            _shouldBeCrouching = true;
            if (!_isCrouching)
            {
                _isCrouching = true;
                Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
            }
        }
        else if (inputs.CrouchUp)
        {
            _shouldBeCrouching = false;
        }
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_lookInputVector == Vector3.zero || OrientationSharpness <= 0f) return;

        Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;
        currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
        
        if (OrientTowardsGravity)
        {
            currentRotation = Quaternion.FromToRotation((currentRotation * Vector3.up), -Gravity) * currentRotation;
        }
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, Motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(Motor.GroundingStatus.GroundNormal, inputRight).normalized * _moveInputVector.magnitude;
            Vector3 targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1 - Mathf.Exp(-StableMovementSharpness * deltaTime));
        }
        else
        {
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed;

                if (Motor.GroundingStatus.FoundAnyGround)
                {
                    Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                    targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                }

                Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, Gravity);
                currentVelocity += velocityDiff * AirAccelerationSpeed * deltaTime;
            }

            currentVelocity += Gravity * deltaTime;
            currentVelocity *= (1f / (1f + (Drag * deltaTime)));
        }

        HandleJumping(ref currentVelocity);
        
        if (_internalVelocityAdd.sqrMagnitude > 0f)
        {
            currentVelocity += _internalVelocityAdd;
            _internalVelocityAdd = Vector3.zero;
        }
    }

    private void HandleJumping(ref Vector3 currentVelocity)
    {
        _jumpedThisFrame = false;
        _timeSinceJumpRequested += deltaTime;
        
        if (!_jumpRequested) return;

        if (AllowDoubleJump && _jumpConsumed && !_doubleJumpConsumed && 
            (AllowJumpingWhenSliding ? !Motor.GroundingStatus.FoundAnyGround : !Motor.GroundingStatus.IsStableOnGround))
        {
            PerformJump(ref currentVelocity, Motor.CharacterUp);
            _doubleJumpConsumed = true;
        }

        if (_canWallJump || (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || 
            _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime)))
        {
            Vector3 jumpDirection = _canWallJump ? _wallJumpNormal : 
                (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround) ? 
                Motor.GroundingStatus.GroundNormal : Motor.CharacterUp;
            
            PerformJump(ref currentVelocity, jumpDirection);
            _jumpConsumed = true;
        }

        _canWallJump = false;
    }

    private void PerformJump(ref Vector3 currentVelocity, Vector3 jumpDirection)
    {
        Motor.ForceUnground(0.1f);
        currentVelocity += (jumpDirection * JumpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
        _jumpRequested = false;
        _jumpedThisFrame = true;
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
            _jumpRequested = false;

        bool isGrounded = AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround;

        if (isGrounded)
        {
            if (!_jumpedThisFrame)
            {
                _doubleJumpConsumed = false;
                _jumpConsumed = false;
            }
            _timeSinceLastAbleToJump = 0f;
        }
        else
        {
            _timeSinceLastAbleToJump += deltaTime;
        }

        if (_isCrouching && !_shouldBeCrouching)
        {
            Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
            if (Motor.CharacterCollisionsOverlap(
                    Motor.TransientPosition,
                    Motor.TransientRotation,
                    _probedColliders) > 0)
            {
                Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
            }
            else
            {
                MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                _isCrouching = false;
            }
        }
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        if (AllowWallJump && !Motor.GroundingStatus.IsStableOnGround && !hitStabilityReport.IsStable)
        {
            _canWallJump = true;
            _wallJumpNormal = hitNormal;
        }
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
            Debug.Log("Landed");
        else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
            Debug.Log("Left ground");
    }

    public void BeforeCharacterUpdate(float deltaTime) { }
    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void AddVelocity(Vector3 velocity) => _internalVelocityAdd += velocity;
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
}