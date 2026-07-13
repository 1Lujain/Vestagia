using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class VestigiaNoteReader : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2.4f;
    [SerializeField, Min(0f)] private float interactionRadius = 0.14f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    [Header("Input System")]
    [Tooltip("Optional. When empty, the project-wide InputSystem.actions asset is used.")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMap = "Player";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string uiActionMap = "UI";
    [SerializeField] private string cancelActionName = "Cancel";

    [Header("Player Control")]
    [SerializeField] private VestigiaFirstPersonController playerController;

    [Header("Prompt UI")]
    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private TMP_Text promptText;

    [Header("Note UI")]
    [SerializeField] private CanvasGroup noteGroup;
    [SerializeField] private TMP_Text noteTitleText;
    [SerializeField] private TMP_Text noteBodyText;

    [Header("Events")]
    [SerializeField] private UnityEvent onNoteOpened;
    [SerializeField] private UnityEvent onNoteClosed;

    private InputAction interactAction;
    private InputAction cancelAction;
    private bool ownsInteractAction;
    private bool ownsCancelAction;
    private bool interactionEnabled = true;
    private VestigiaPhysicalNote focusedNote;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    public bool IsReading { get; private set; }
    public VestigiaPhysicalNote CurrentNote { get; private set; }

    private void Awake()
    {
        if (viewCamera == null && playerController != null)
            viewCamera = playerController.ViewCamera;
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (playerController == null)
            playerController = FindFirstObjectByType<VestigiaFirstPersonController>();

        ResolveActions();
        HidePrompt();
        SetCanvasGroup(noteGroup, false);
    }

    private void OnEnable()
    {
        ResolveActions();
        ownsInteractAction = EnableIfNeeded(interactAction);
        ownsCancelAction = EnableIfNeeded(cancelAction);
    }

    private void OnDisable()
    {
        if (IsReading)
            CloseNote();

        HidePrompt();
        DisableIfOwned(interactAction, ownsInteractAction);
        DisableIfOwned(cancelAction, ownsCancelAction);
    }

    private void Update()
    {
        bool interactPressed = InteractWasPressedThisFrame();

        if (IsReading)
        {
            if (interactPressed || CancelWasPressedThisFrame())
                CloseNote();
            return;
        }

        if (!interactionEnabled)
        {
            focusedNote = null;
            HidePrompt();
            return;
        }

        focusedNote = FindLookedAtNote();
        if (focusedNote == null)
        {
            HidePrompt();
            return;
        }

        ShowPrompt(focusedNote.Prompt);
        if (interactPressed)
            OpenNote(focusedNote);
    }

    public void Configure(
        Camera cameraToUse,
        VestigiaFirstPersonController controller,
        CanvasGroup worldPromptGroup,
        TMP_Text worldPromptText,
        CanvasGroup fullNoteGroup,
        TMP_Text fullNoteTitle,
        TMP_Text fullNoteBody,
        InputActionAsset actions = null)
    {
        viewCamera = cameraToUse;
        playerController = controller;
        promptGroup = worldPromptGroup;
        promptText = worldPromptText;
        noteGroup = fullNoteGroup;
        noteTitleText = fullNoteTitle;
        noteBodyText = fullNoteBody;
        inputActions = actions;
        ResolveActions();
        HidePrompt();
        SetCanvasGroup(noteGroup, false);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (!enabled)
        {
            focusedNote = null;
            HidePrompt();
        }
    }

    public void OpenNote(VestigiaPhysicalNote note)
    {
        if (note == null || IsReading)
            return;

        CurrentNote = note;
        IsReading = true;
        HidePrompt();

        if (noteTitleText != null)
            noteTitleText.text = note.Title;
        if (noteBodyText != null)
            noteBodyText.text = note.Body;
        SetCanvasGroup(noteGroup, true);

        playerController?.SetControlLocked(true);
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        onNoteOpened?.Invoke();
    }

    public void CloseNote()
    {
        if (!IsReading)
            return;

        IsReading = false;
        CurrentNote = null;
        SetCanvasGroup(noteGroup, false);
        playerController?.SetControlLocked(false);
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        onNoteClosed?.Invoke();
    }

    private VestigiaPhysicalNote FindLookedAtNote()
    {
        if (viewCamera == null)
            return null;

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide))
        {
            VestigiaPhysicalNote directNote = hit.collider.GetComponentInParent<VestigiaPhysicalNote>();
            if (directNote != null)
                return directNote;
        }

        // Thin paper resting exactly on a collidable tabletop is easy for a one-pixel ray to
        // miss or for the table surface to win by a tiny floating-point margin. A small cast
        // makes the intended bedside interaction reliable while still requiring the player
        // to look directly at the note and remain within the authored interaction distance.
        RaycastHit[] nearbyHits = Physics.SphereCastAll(ray, interactionRadius, interactionDistance,
            interactionLayers, QueryTriggerInteraction.Collide);
        foreach (RaycastHit nearbyHit in nearbyHits)
        {
            VestigiaPhysicalNote nearbyNote = nearbyHit.collider.GetComponentInParent<VestigiaPhysicalNote>();
            if (nearbyNote != null)
                return nearbyNote;
        }

        VestigiaPhysicalNote bestNote = null;
        float bestAngle = 18f;
        foreach (VestigiaPhysicalNote note in FindObjectsByType<VestigiaPhysicalNote>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Collider noteCollider = note.GetComponentInChildren<Collider>();
            Vector3 target = noteCollider != null ? noteCollider.bounds.center : note.transform.position;
            Vector3 toNote = target - ray.origin;
            if (toNote.magnitude > interactionDistance)
                continue;

            float angle = Vector3.Angle(ray.direction, toNote);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestNote = note;
            }
        }

        return bestNote;
    }

    private void ShowPrompt(string message)
    {
        if (promptText != null)
            promptText.text = message;
        SetCanvasGroup(promptGroup, true);
    }

    private void HidePrompt()
    {
        SetCanvasGroup(promptGroup, false);
    }

    private void ResolveActions()
    {
        InputActionAsset actions = inputActions != null ? inputActions : InputSystem.actions;
        InputActionMap playerMap = actions != null ? actions.FindActionMap(playerActionMap, false) : null;
        InputActionMap uiMap = actions != null ? actions.FindActionMap(uiActionMap, false) : null;
        interactAction = playerMap != null ? playerMap.FindAction(interactActionName, false) : null;
        cancelAction = uiMap != null ? uiMap.FindAction(cancelActionName, false) : null;
    }

    private bool InteractWasPressedThisFrame()
    {
        if (interactAction != null && interactAction.WasPressedThisFrame())
            return true;

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private bool CancelWasPressedThisFrame()
    {
        if (cancelAction != null && cancelAction.WasPressedThisFrame())
            return true;

        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
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

    private static void SetCanvasGroup(CanvasGroup group, bool visible)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
