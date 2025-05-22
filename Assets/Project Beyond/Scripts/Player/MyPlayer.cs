using UnityEngine;

public class MyPlayer : MonoBehaviour
{
    public MyCharacterCamera OrbitCamera;
    public Transform CameraFollowPoint;
    public MyCharacterController Character;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        OrbitCamera.SetFollowTransform(CameraFollowPoint);
        OrbitCamera.IgnoredColliders.AddRange(Character.GetComponentsInChildren<Collider>());
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            Cursor.lockState = CursorLockMode.Locked;

        HandleCharacterInput();
    }

    private void LateUpdate() => HandleCameraInput();

    private void HandleCameraInput()
    {
        Vector3 lookInput = Cursor.lockState == CursorLockMode.Locked ? 
            new Vector3(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"), 0) : 
            Vector3.zero;

        float scrollInput = Application.platform == RuntimePlatform.WebGLPlayer ? 0 : -Input.GetAxis("Mouse ScrollWheel");
        
        OrbitCamera.UpdateWithInput(Time.deltaTime, scrollInput, lookInput);

        if (Input.GetMouseButtonDown(1))
            OrbitCamera.TargetDistance = OrbitCamera.TargetDistance == 0 ? OrbitCamera.DefaultDistance : 0;
    }

    private void HandleCharacterInput()
    {
        var inputs = new PlayerCharacterInputs
        {
            MoveAxisForward = Input.GetAxisRaw("Vertical"),
            MoveAxisRight = Input.GetAxisRaw("Horizontal"),
            CameraRotation = OrbitCamera.Transform.rotation,
            JumpDown = Input.GetKeyDown(KeyCode.Space),
            CrouchDown = Input.GetKeyDown(KeyCode.LeftControl),
            CrouchUp = Input.GetKeyUp(KeyCode.LeftControl),
        };

        Character.SetInputs(ref inputs);
        
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            Character.Motor.ForceUnground(0.1f);
            Character.AddVelocity(Vector3.one * 10f);
        }
    }
}