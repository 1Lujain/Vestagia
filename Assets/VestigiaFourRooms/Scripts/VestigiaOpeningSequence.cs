using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class VestigiaOpeningSequence : MonoBehaviour
{
    public const string GameTitle = "VESTIGIA";
    public const string GameSubtitle = "Every room remembers.";

    [Header("Player And Camera")]
    [SerializeField] private VestigiaFirstPersonController playerController;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform playerStandingPose;
    [SerializeField] private Transform wakeCameraStart;
    [SerializeField] private Transform wakeCameraEnd;

    [Header("Optional Female Body Pose")]
    [Tooltip("Assign the visible model root, not the CharacterController root.")]
    [SerializeField] private Transform femaleBodyRoot;
    [SerializeField] private Transform bodyLyingPose;
    [SerializeField] private Transform bodyStandingPose;

    [Header("Opening UI")]
    [SerializeField] private CanvasGroup blackFadeGroup;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float initialBlackHold = 0.25f;
    [SerializeField, Min(0.01f)] private float titleFadeInDuration = 0.8f;
    [SerializeField, Min(0f)] private float titleHoldDuration = 1.65f;
    [SerializeField, Min(0.01f)] private float titleFadeOutDuration = 0.85f;
    [SerializeField, Min(0.01f)] private float fadeFromBlackDuration = 1.15f;
    [SerializeField, Min(0.01f)] private float wakeUpDuration = 3.1f;
    [SerializeField] private AnimationCurve wakeUpCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Header("Sequence")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private VestigiaNoteReader noteReader;
    [SerializeField] private UnityEvent onOpeningStarted;
    [SerializeField] private UnityEvent onOpeningCompleted;

    private Coroutine sequenceRoutine;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPosition;
    private Quaternion originalCameraLocalRotation;
    private Vector3 originalBodyLocalPosition;
    private Quaternion originalBodyLocalRotation;
    private bool cameraPoseCaptured;
    private bool bodyPoseCaptured;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<VestigiaFirstPersonController>();
        if (playerRoot == null && playerController != null)
            playerRoot = playerController.transform;
        if (playerCamera == null && playerController != null)
            playerCamera = playerController.ViewCamera;
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (titleText != null)
            titleText.text = GameTitle;
        if (subtitleText != null)
            subtitleText.text = GameSubtitle;

        SetCanvasGroup(titleGroup, 0f, false);
        SetCanvasGroup(blackFadeGroup, 1f, true);
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Configure(
        VestigiaFirstPersonController controller,
        Transform standingPlayerPose,
        Camera cameraToAnimate,
        Transform cameraStart,
        Transform cameraEnd,
        Transform visibleBody,
        Transform lyingPose,
        Transform standingBodyPose,
        CanvasGroup blackGroup,
        CanvasGroup introTitleGroup,
        TMP_Text gameTitleText,
        TMP_Text gameSubtitleText,
        VestigiaNoteReader reader)
    {
        playerController = controller;
        playerRoot = controller != null ? controller.transform : playerRoot;
        playerStandingPose = standingPlayerPose;
        playerCamera = cameraToAnimate;
        wakeCameraStart = cameraStart;
        wakeCameraEnd = cameraEnd;
        femaleBodyRoot = visibleBody;
        bodyLyingPose = lyingPose;
        bodyStandingPose = standingBodyPose;
        blackFadeGroup = blackGroup;
        titleGroup = introTitleGroup;
        titleText = gameTitleText;
        subtitleText = gameSubtitleText;
        noteReader = reader;

        if (titleText != null)
            titleText.text = GameTitle;
        if (subtitleText != null)
            subtitleText.text = GameSubtitle;
    }

    public void Play()
    {
        if (!isActiveAndEnabled || IsPlaying)
            return;

        sequenceRoutine = StartCoroutine(PlayRoutine());
    }

    public void SkipToEnd()
    {
        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = null;
        CompleteSequence();
    }

    private IEnumerator PlayRoutine()
    {
        IsPlaying = true;
        onOpeningStarted?.Invoke();
        playerController?.SetControlLocked(true);
        noteReader?.SetInteractionEnabled(false);

        PreparePlayerAndCamera();
        SetCanvasGroup(blackFadeGroup, 1f, true);
        SetCanvasGroup(titleGroup, 0f, false);

        yield return WaitUnscaled(initialBlackHold);
        yield return FadeCanvasGroup(titleGroup, 0f, 1f, titleFadeInDuration, false);
        yield return WaitUnscaled(titleHoldDuration);
        yield return FadeCanvasGroup(titleGroup, 1f, 0f, titleFadeOutDuration, false);
        yield return FadeCanvasGroup(blackFadeGroup, 1f, 0f, fadeFromBlackDuration, false);
        yield return AnimateWakeUp();

        sequenceRoutine = null;
        CompleteSequence();
    }

    private void PreparePlayerAndCamera()
    {
        if (playerRoot != null && playerStandingPose != null)
        {
            if (playerController != null)
                playerController.Teleport(playerStandingPose.position, playerStandingPose.rotation);
            else
                playerRoot.SetPositionAndRotation(playerStandingPose.position, playerStandingPose.rotation);
        }

        if (femaleBodyRoot != null && femaleBodyRoot != playerRoot)
        {
            originalBodyLocalPosition = femaleBodyRoot.localPosition;
            originalBodyLocalRotation = femaleBodyRoot.localRotation;
            bodyPoseCaptured = true;

            if (bodyLyingPose != null)
                femaleBodyRoot.SetPositionAndRotation(bodyLyingPose.position, bodyLyingPose.rotation);
        }

        if (playerCamera == null)
            return;

        originalCameraParent = playerCamera.transform.parent;
        originalCameraLocalPosition = playerCamera.transform.localPosition;
        originalCameraLocalRotation = playerCamera.transform.localRotation;
        cameraPoseCaptured = true;

        playerCamera.transform.SetParent(null, true);
        if (wakeCameraStart != null)
            playerCamera.transform.SetPositionAndRotation(wakeCameraStart.position, wakeCameraStart.rotation);
    }

    private IEnumerator AnimateWakeUp()
    {
        Vector3 cameraFromPosition = playerCamera != null ? playerCamera.transform.position : Vector3.zero;
        Quaternion cameraFromRotation = playerCamera != null ? playerCamera.transform.rotation : Quaternion.identity;
        Vector3 cameraToPosition = wakeCameraEnd != null ? wakeCameraEnd.position : cameraFromPosition;
        Quaternion cameraToRotation = wakeCameraEnd != null ? wakeCameraEnd.rotation : cameraFromRotation;

        Vector3 bodyFromPosition = femaleBodyRoot != null ? femaleBodyRoot.position : Vector3.zero;
        Quaternion bodyFromRotation = femaleBodyRoot != null ? femaleBodyRoot.rotation : Quaternion.identity;
        Vector3 bodyToPosition = bodyStandingPose != null ? bodyStandingPose.position : bodyFromPosition;
        Quaternion bodyToRotation = bodyStandingPose != null ? bodyStandingPose.rotation : bodyFromRotation;

        float elapsed = 0f;
        while (elapsed < wakeUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / wakeUpDuration);
            float curvedTime = wakeUpCurve != null ? wakeUpCurve.Evaluate(normalizedTime) : Mathf.SmoothStep(0f, 1f, normalizedTime);

            if (playerCamera != null)
            {
                playerCamera.transform.position = Vector3.LerpUnclamped(cameraFromPosition, cameraToPosition, curvedTime);
                playerCamera.transform.rotation = Quaternion.SlerpUnclamped(cameraFromRotation, cameraToRotation, curvedTime);
            }

            if (femaleBodyRoot != null && femaleBodyRoot != playerRoot && bodyStandingPose != null)
            {
                float bodyTime = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, normalizedTime));
                femaleBodyRoot.position = Vector3.Lerp(bodyFromPosition, bodyToPosition, bodyTime);
                femaleBodyRoot.rotation = Quaternion.Slerp(bodyFromRotation, bodyToRotation, bodyTime);
            }

            yield return null;
        }
    }

    private void CompleteSequence()
    {
        SetCanvasGroup(titleGroup, 0f, false);
        SetCanvasGroup(blackFadeGroup, 0f, false);

        if (playerCamera != null && cameraPoseCaptured)
        {
            playerCamera.transform.SetParent(originalCameraParent, false);
            playerCamera.transform.localPosition = originalCameraLocalPosition;
            playerCamera.transform.localRotation = originalCameraLocalRotation;
        }

        if (femaleBodyRoot != null && femaleBodyRoot != playerRoot)
        {
            if (bodyStandingPose != null)
                femaleBodyRoot.SetPositionAndRotation(bodyStandingPose.position, bodyStandingPose.rotation);
            else if (bodyPoseCaptured)
            {
                femaleBodyRoot.localPosition = originalBodyLocalPosition;
                femaleBodyRoot.localRotation = originalBodyLocalRotation;
            }
        }

        playerController?.SetControlLocked(false);
        noteReader?.SetInteractionEnabled(true);
        IsPlaying = false;
        onOpeningCompleted?.Invoke();
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration, bool blocksRaycasts)
    {
        if (group == null)
            yield break;

        group.gameObject.SetActive(true);
        group.blocksRaycasts = blocksRaycasts;
        group.interactable = blocksRaycasts;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
        if (Mathf.Approximately(to, 0f))
        {
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static void SetCanvasGroup(CanvasGroup group, float alpha, bool blocksRaycasts)
    {
        if (group == null)
            return;

        group.alpha = alpha;
        group.interactable = blocksRaycasts;
        group.blocksRaycasts = blocksRaycasts;
    }
}
