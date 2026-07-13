using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class VestigiaFirstPersonController : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform pitchPivot;
    [SerializeField, Range(0.01f, 1f)] private float mouseLookSensitivity = 0.08f;
    [SerializeField, Range(20f, 360f)] private float gamepadLookSpeed = 120f;
    [SerializeField, Range(30f, 89f)] private float maximumPitch = 82f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 2.6f;
    [SerializeField, Min(1f)] private float sprintMultiplier = 1.65f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.05f;
    [SerializeField, Min(0f)] private float gravity = 22f;

    [Header("Input System")]
    [Tooltip("Optional. When empty, the project-wide InputSystem.actions asset is used.")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string sprintActionName = "Sprint";

    [Header("State")]
    [SerializeField] private bool startControlLocked;
    [SerializeField] private bool lockCursorOnPlay = true;

    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private bool ownsMoveAction;
    private bool ownsLookAction;
    private bool ownsJumpAction;
    private bool ownsSprintAction;
    private bool controlLocked;
    private float verticalVelocity;
    private float pitch;
    private Vector2 pivotBaseAngles;

    public bool IsControlLocked => controlLocked;
    public Camera ViewCamera => viewCamera;
    public CharacterController CharacterController => characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (viewCamera == null)
            viewCamera = GetComponentInChildren<Camera>(true);
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (pitchPivot == null && viewCamera != null)
            pitchPivot = viewCamera.transform;

        if (pitchPivot != null)
        {
            Vector3 euler = pitchPivot.localEulerAngles;
            pitch = NormalizeAngle(euler.x);
            pivotBaseAngles = new Vector2(euler.y, euler.z);
        }

        controlLocked = startControlLocked;
        ResolveActions();
    }

    private void OnEnable()
    {
        ResolveActions();
        ownsMoveAction = EnableIfNeeded(moveAction);
        ownsLookAction = EnableIfNeeded(lookAction);
        ownsJumpAction = EnableIfNeeded(jumpAction);
        ownsSprintAction = EnableIfNeeded(sprintAction);

        if (lockCursorOnPlay && Application.isPlaying)
            SetCursorLocked(true);
    }

    private void OnDisable()
    {
        DisableIfOwned(moveAction, ownsMoveAction);
        DisableIfOwned(lookAction, ownsLookAction);
        DisableIfOwned(jumpAction, ownsJumpAction);
        DisableIfOwned(sprintAction, ownsSprintAction);
    }

    private void Update()
    {
        if (!controlLocked)
        {
            UpdateLook();
            UpdateMovement();
            return;
        }

        // Keep the capsule settled on the floor while menus or notes are open.
        if (characterController != null && characterController.enabled)
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            else
                verticalVelocity -= gravity * Time.deltaTime;

            characterController.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
        }
    }

    public void Configure(Camera cameraToUse, Transform cameraPitchPivot, InputActionAsset actions = null)
    {
        viewCamera = cameraToUse;
        pitchPivot = cameraPitchPivot != null ? cameraPitchPivot : cameraToUse != null ? cameraToUse.transform : null;
        inputActions = actions;
        ResolveActions();
    }

    public void SetControlLocked(bool locked)
    {
        controlLocked = locked;
        if (locked)
            verticalVelocity = Mathf.Min(verticalVelocity, 0f);
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void Teleport(Vector3 worldPosition, Quaternion worldRotation)
    {
        bool wasEnabled = characterController != null && characterController.enabled;
        if (wasEnabled)
            characterController.enabled = false;

        transform.SetPositionAndRotation(worldPosition, worldRotation);
        verticalVelocity = 0f;

        if (wasEnabled)
            characterController.enabled = true;
    }

    public void SetViewPitch(float degrees)
    {
        pitch = Mathf.Clamp(degrees, -maximumPitch, maximumPitch);
        ApplyPitch();
    }

    private void UpdateLook()
    {
        if (lookAction == null || pitchPivot == null)
            return;

        Vector2 look = lookAction.ReadValue<Vector2>();
        bool isMouse = lookAction.activeControl != null && lookAction.activeControl.device is Mouse;
        float scale = isMouse ? mouseLookSensitivity : gamepadLookSpeed * Time.deltaTime;

        transform.Rotate(0f, look.x * scale, 0f, Space.Self);
        pitch = Mathf.Clamp(pitch - look.y * scale, -maximumPitch, maximumPitch);
        ApplyPitch();
    }

    private void UpdateMovement()
    {
        if (characterController == null || !characterController.enabled)
            return;

        Vector2 input = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
        bool sprinting = sprintAction != null && sprintAction.IsPressed();
        float speed = walkSpeed * (sprinting ? sprintMultiplier : 1f);
        Vector3 planarMovement = (transform.right * input.x + transform.forward * input.y) * speed;

        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (jumpAction != null && jumpAction.WasPressedThisFrame())
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        planarMovement.y = verticalVelocity;
        characterController.Move(planarMovement * Time.deltaTime);
    }

    private void ApplyPitch()
    {
        if (pitchPivot != null)
            pitchPivot.localRotation = Quaternion.Euler(pitch, pivotBaseAngles.x, pivotBaseAngles.y);
    }

    private void ResolveActions()
    {
        InputActionAsset actions = inputActions != null ? inputActions : InputSystem.actions;
        InputActionMap playerMap = actions != null ? actions.FindActionMap(actionMapName, false) : null;

        moveAction = playerMap != null ? playerMap.FindAction(moveActionName, false) : null;
        lookAction = playerMap != null ? playerMap.FindAction(lookActionName, false) : null;
        jumpAction = playerMap != null ? playerMap.FindAction(jumpActionName, false) : null;
        sprintAction = playerMap != null ? playerMap.FindAction(sprintActionName, false) : null;
    }

    private static bool EnableIfNeeded(InputAction action)
    {
        if (action == null || action.enabled)
            return false;

        action.Enable();
        return true;
    }

    private static void DisableIfOwned(InputAction action, bool owned)
    {
        if (owned && action != null)
            action.Disable();
    }

    private static float NormalizeAngle(float degrees)
    {
        return degrees > 180f ? degrees - 360f : degrees;
    }
}
