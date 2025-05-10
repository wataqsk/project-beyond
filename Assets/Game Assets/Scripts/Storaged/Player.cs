using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main player controller that bridges input system with character and camera systems
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerCamera _playerCamera;
    [SerializeField] private Transform _cameraFollowPoint;
    
    [Header("Input Settings")]
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private float _scrollSensitivity = 5f;
    
    private CharacterController _characterController;
    private Vector2 _lookInput;
    private bool _cursorLocked = true;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        InitializeCursor();
    }

    private void Start()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        if (_playerCamera != null && _cameraFollowPoint != null)
        {
            _playerCamera.SetFollowTransform(_cameraFollowPoint);
        }
    }

    private void InitializeCursor()
    {
        Cursor.lockState = _cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !_cursorLocked;
    }

    private void Update()
    {
        HandleCharacterInputs();
        HandleCursorToggle();
    }

    private void LateUpdate()
    {
        HandleCameraInput();
    }

    /// <summary>
    /// Processes all movement and action inputs
    /// </summary>
    private void HandleCharacterInputs()
    {
        var inputs = new PlayerInputs
        {
            MoveAxisForward = Input.GetAxisRaw("Vertical"),
            MoveAxisRight = Input.GetAxisRaw("Horizontal"),
            CameraRotation = _playerCamera.transform.rotation,
            JumpPressed = Input.GetButtonDown("Jump")
        };

        _characterController.SetInputs(ref inputs);
    }

    /// <summary>
    /// Processes camera look and zoom inputs
    /// </summary>
    private void HandleCameraInput()
    {
        // Apply sensitivity to mouse input
        _lookInput = new Vector2(
            Input.GetAxisRaw("Mouse X") * _mouseSensitivity,
            Input.GetAxisRaw("Mouse Y") * _mouseSensitivity
        );

        float scrollInput = -Input.GetAxis("Mouse ScrollWheel") * _scrollSensitivity;

        _playerCamera.UpdateWithInput(Time.deltaTime, scrollInput, _lookInput);
    }

    /// <summary>
    /// Toggles cursor lock state when Escape is pressed
    /// </summary>
    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _cursorLocked = !_cursorLocked;
            Cursor.lockState = _cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_cursorLocked;
        }
    }
}