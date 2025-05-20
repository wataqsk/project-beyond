using UnityEngine;

/// <summary>
/// Main player controller that handles character movement and camera control
/// </summary>
public class Player : MonoBehaviour
{
    #region Serialized Fields

    [Header("Camera Settings")]
    [Tooltip("The orbit camera controller")]
    public MyCharacterCamera OrbitCamera;
    [Tooltip("Follow point for the camera")]
    public Transform CameraFollowPoint;

    [Header("Character Settings")]
    [Tooltip("The character controller")]
    public MyCharacterController Character;

    #endregion

    #region Private Constants

    private const string MouseXInput = "Mouse X";
    private const string MouseYInput = "Mouse Y";
    private const string MouseScrollInput = "Mouse ScrollWheel";
    private const string HorizontalInput = "Horizontal";
    private const string VerticalInput = "Vertical";

    #endregion

    #region Unity Lifecycle Methods

    private void Start()
    {
        InitializeCursor();
        InitializeCamera();
        IgnoreCharacterColliders();
    }

    private void Update()
    {
        HandleCursorToggle();
        HandleCharacterInput();
    }

    private void LateUpdate()
    {
        HandleCameraInput();
    }

    #endregion

    #region Initialization Methods

    /// <summary>
    /// Initialize cursor lock state
    /// </summary>
    private void InitializeCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// Set up camera follow target
    /// </summary>
    private void InitializeCamera()
    {
        OrbitCamera.SetFollowTransform(CameraFollowPoint);
    }

    /// <summary>
    /// Configure camera to ignore character colliders
    /// </summary>
    private void IgnoreCharacterColliders()
    {
        OrbitCamera.IgnoredColliders.Clear();
        OrbitCamera.IgnoredColliders.AddRange(Character.GetComponentsInChildren<Collider>());
    }

    #endregion

    #region Input Handling Methods

    /// <summary>
    /// Toggle cursor lock with mouse click
    /// </summary>
    private void HandleCursorToggle()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    /// <summary>
    /// Handle all camera-related input
    /// </summary>
    private void HandleCameraInput()
    {
        Vector3 lookInputVector = GetCameraLookInput();
        float scrollInput = GetCameraZoomInput();
        
        OrbitCamera.UpdateWithInput(Time.deltaTime, scrollInput, lookInputVector);
        HandleCameraZoomToggle();
    }

    /// <summary>
    /// Get look input vector for camera rotation
    /// </summary>
    private Vector3 GetCameraLookInput()
    {
        float mouseLookAxisUp = Input.GetAxisRaw(MouseYInput);
        float mouseLookAxisRight = Input.GetAxisRaw(MouseXInput);
        Vector3 lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f);

        // Zero input if cursor isn't locked
        return Cursor.lockState != CursorLockMode.Locked ? Vector3.zero : lookInputVector;
    }

    /// <summary>
    /// Get zoom input for camera distance
    /// </summary>
    private float GetCameraZoomInput()
    {
#if UNITY_WEBGL
        return 0f; // Disable zoom in WebGL
#else
        return -Input.GetAxis(MouseScrollInput);
#endif
    }

    /// <summary>
    /// Toggle between default and zero zoom distance
    /// </summary>
    private void HandleCameraZoomToggle()
    {
        if (Input.GetMouseButtonDown(1))
        {
            OrbitCamera.TargetDistance = OrbitCamera.TargetDistance == 0f 
                ? OrbitCamera.DefaultDistance 
                : 0f;
        }
    }

    /// <summary>
    /// Handle all character movement input
    /// </summary>
    private void HandleCharacterInput()
    {
        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs
        {
            MoveAxisForward = Input.GetAxisRaw(VerticalInput),
            MoveAxisRight = Input.GetAxisRaw(HorizontalInput),
            CameraRotation = OrbitCamera.Transform.rotation,
            JumpDown = Input.GetKeyDown(KeyCode.Space)
        };

        Character.SetInputs(ref characterInputs);
    }

    #endregion
}