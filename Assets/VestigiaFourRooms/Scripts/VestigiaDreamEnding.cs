using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class VestigiaDreamEnding : MonoBehaviour
{
    [Header("Final Door")]
    [SerializeField] private Transform finalDoorLeaf;
    [SerializeField] private BoxCollider activationArea;
    [SerializeField, Range(30f, 120f)] private float openAngle = 78f;

    [Header("Existing Player Systems")]
    [SerializeField] private VestigiaFirstPersonController playerController;
    [SerializeField] private VestigiaNoteReader noteReader;
    [SerializeField] private Camera viewCamera;

    [Header("Ending UI")]
    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private CanvasGroup endingOverlay;
    [SerializeField] private CanvasGroup chapterGroup;
    [SerializeField] private CanvasGroup revelationGroup;
    [SerializeField] private CanvasGroup epilogueGroup;

    private InputAction interactAction;
    private bool ownsInteractAction;
    private bool endingStarted;
    private Quaternion closedDoorRotation;
    private float originalFieldOfView = 54f;
    private AudioSource ambienceSource;
    private AudioSource doorSource;
    private AudioClip ambienceClip;
    private AudioClip doorClip;

    public bool EndingStarted => endingStarted;

    private void Awake()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<VestigiaFirstPersonController>();
        if (noteReader == null)
            noteReader = FindFirstObjectByType<VestigiaNoteReader>();
        if (viewCamera == null && playerController != null)
            viewCamera = playerController.ViewCamera;
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (activationArea == null)
            activationArea = GetComponent<BoxCollider>();

        if (finalDoorLeaf != null)
            closedDoorRotation = finalDoorLeaf.localRotation;
        if (viewCamera != null)
            originalFieldOfView = viewCamera.fieldOfView;

        SetGroup(promptGroup, false, 0f);
        SetGroup(endingOverlay, false, 0f);
        SetGroup(chapterGroup, false, 0f);
        SetGroup(revelationGroup, false, 0f);
        SetGroup(epilogueGroup, false, 0f);
        if (promptText != null)
            promptText.text = "Press E to open the final door";

        ResolveInput();
        CreateProceduralAudio();
    }

    private void OnEnable()
    {
        ResolveInput();
        if (interactAction != null && !interactAction.enabled)
        {
            interactAction.Enable();
            ownsInteractAction = true;
        }

        if (ambienceSource != null && ambienceClip != null && !ambienceSource.isPlaying)
            ambienceSource.Play();
    }

    private void OnDisable()
    {
        if (ownsInteractAction && interactAction != null)
            interactAction.Disable();
        ownsInteractAction = false;
    }

    private void OnDestroy()
    {
        if (ambienceClip != null)
            Destroy(ambienceClip);
        if (doorClip != null)
            Destroy(doorClip);
    }

    private void Update()
    {
        if (endingStarted)
            return;

        bool canUseDoor = IsPlayerAtDoor();
        SetGroup(promptGroup, canUseDoor, Mathf.MoveTowards(promptGroup != null ? promptGroup.alpha : 0f,
            canUseDoor ? 1f : 0f, Time.unscaledDeltaTime * 5f));

        if (canUseDoor && InteractWasPressedThisFrame())
            BeginEnding();
    }

    public void Configure(
        Transform doorLeaf,
        BoxCollider triggerArea,
        VestigiaFirstPersonController controller,
        VestigiaNoteReader reader,
        Camera cameraToUse,
        CanvasGroup doorPrompt,
        TMP_Text doorPromptText,
        CanvasGroup overlay,
        CanvasGroup chapter,
        CanvasGroup revelation,
        CanvasGroup epilogue)
    {
        finalDoorLeaf = doorLeaf;
        activationArea = triggerArea;
        playerController = controller;
        noteReader = reader;
        viewCamera = cameraToUse;
        promptGroup = doorPrompt;
        promptText = doorPromptText;
        endingOverlay = overlay;
        chapterGroup = chapter;
        revelationGroup = revelation;
        epilogueGroup = epilogue;
    }

    public void BeginEndingForPlaytest()
    {
        if (!endingStarted)
            BeginEnding();
    }

    private bool IsPlayerAtDoor()
    {
        if (activationArea == null || playerController == null || viewCamera == null)
            return false;

        Vector3 playerCenter = playerController.transform.position + Vector3.up * 0.9f;
        if (!activationArea.bounds.Contains(playerCenter))
            return false;

        Vector3 target = finalDoorLeaf != null ? finalDoorLeaf.position : transform.position + transform.forward;
        Vector3 toDoor = (target + Vector3.up - viewCamera.transform.position).normalized;
        return Vector3.Dot(viewCamera.transform.forward, toDoor) > 0.22f;
    }

    private bool InteractWasPressedThisFrame()
    {
        if (interactAction != null && interactAction.WasPressedThisFrame())
            return true;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            return true;
        return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    }

    private void BeginEnding()
    {
        endingStarted = true;
        SetGroup(promptGroup, false, 0f);
        playerController?.SetControlLocked(true);
        noteReader?.SetInteractionEnabled(false);
        StartCoroutine(PlayEnding());
    }

    private IEnumerator PlayEnding()
    {
        if (doorSource != null && doorClip != null)
            doorSource.PlayOneShot(doorClip, 0.68f);

        Quaternion playerStart = playerController != null ? playerController.transform.rotation : Quaternion.identity;
        Quaternion playerTarget = playerStart;
        if (playerController != null && finalDoorLeaf != null)
        {
            Vector3 forward = finalDoorLeaf.position - playerController.transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
                playerTarget = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        Quaternion doorStart = finalDoorLeaf != null ? finalDoorLeaf.localRotation : Quaternion.identity;
        Quaternion doorTarget = closedDoorRotation * Quaternion.Euler(0f, openAngle, 0f);
        float elapsed = 0f;
        const float doorDuration = 2.35f;
        while (elapsed < doorDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / doorDuration));
            if (playerController != null)
                playerController.transform.rotation = Quaternion.Slerp(playerStart, playerTarget, t);
            if (finalDoorLeaf != null)
                finalDoorLeaf.localRotation = Quaternion.Slerp(doorStart, doorTarget, t);
            if (viewCamera != null)
                viewCamera.fieldOfView = Mathf.Lerp(originalFieldOfView, 45f, t);
            yield return null;
        }

        if (finalDoorLeaf != null)
        {
            foreach (Collider doorCollider in finalDoorLeaf.GetComponentsInChildren<Collider>(true))
                doorCollider.enabled = false;
        }

        SetGroup(endingOverlay, true, 0f);
        yield return FadeGroup(endingOverlay, 0f, 1f, 1.55f);
        yield return new WaitForSecondsRealtime(0.35f);

        SetGroup(chapterGroup, true, 0f);
        yield return FadeGroup(chapterGroup, 0f, 1f, 1.10f);
        yield return new WaitForSecondsRealtime(1.35f);
        yield return FadeGroup(chapterGroup, 1f, 0f, 0.85f);

        SetGroup(revelationGroup, true, 0f);
        yield return FadeGroup(revelationGroup, 0f, 1f, 1.20f);
        yield return new WaitForSecondsRealtime(3.10f);
        yield return FadeGroup(revelationGroup, 1f, 0f, 1.00f);

        SetGroup(epilogueGroup, true, 0f);
        yield return FadeGroup(epilogueGroup, 0f, 1f, 1.45f);

        if (ambienceSource != null)
            StartCoroutine(FadeAudio(ambienceSource, ambienceSource.volume, 0.018f, 3f));
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        group.alpha = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = to;
    }

    private static IEnumerator FadeAudio(AudioSource source, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (source != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        if (source != null)
            source.volume = to;
    }

    private void ResolveInput()
    {
        InputActionAsset actions = InputSystem.actions;
        InputActionMap playerMap = actions != null ? actions.FindActionMap("Player", false) : null;
        interactAction = playerMap != null ? playerMap.FindAction("Interact", false) : null;
    }

    private void CreateProceduralAudio()
    {
        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.spatialBlend = 1f;
        ambienceSource.rolloffMode = AudioRolloffMode.Logarithmic;
        ambienceSource.minDistance = 1.4f;
        ambienceSource.maxDistance = 8.5f;
        ambienceSource.volume = 0.075f;

        doorSource = gameObject.AddComponent<AudioSource>();
        doorSource.playOnAwake = false;
        doorSource.loop = false;
        doorSource.spatialBlend = 1f;
        doorSource.minDistance = 1.1f;
        doorSource.maxDistance = 10f;
        doorSource.volume = 0.34f;

        ambienceClip = BuildAmbienceClip();
        doorClip = BuildDoorClip();
        ambienceSource.clip = ambienceClip;
    }

    private static AudioClip BuildAmbienceClip()
    {
        const int sampleRate = 22050;
        const int seconds = 8;
        int count = sampleRate * seconds;
        float[] samples = new float[count];
        System.Random random = new System.Random(1327);
        float filteredNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)sampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.0025f);
            float drone = Mathf.Sin(2f * Mathf.PI * 43f * time) * 0.42f
                          + Mathf.Sin(2f * Mathf.PI * 57.25f * time) * 0.20f
                          + Mathf.Sin(2f * Mathf.PI * 86f * time) * 0.08f;
            samples[i] = (drone + filteredNoise * 0.32f) * 0.20f;
        }

        AudioClip clip = AudioClip.Create("Vestigia_Subtle_Old_House_Ambience", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip BuildDoorClip()
    {
        const int sampleRate = 22050;
        const float seconds = 3.2f;
        int count = Mathf.CeilToInt(sampleRate * seconds);
        float[] samples = new float[count];
        System.Random random = new System.Random(4079);
        float filteredNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)sampleRate;
            float normalized = time / seconds;
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(normalized));
            float frequency = Mathf.Lerp(146f, 62f, normalized);
            float creak = Mathf.Sin(2f * Mathf.PI * frequency * time + Mathf.Sin(time * 18f) * 2.2f);
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.035f);
            float thumpEnvelope = Mathf.Exp(-Mathf.Pow((time - 0.34f) * 7.2f, 2f));
            float thump = Mathf.Sin(2f * Mathf.PI * 48f * time) * thumpEnvelope;
            samples[i] = creak * envelope * 0.16f + filteredNoise * envelope * 0.11f + thump * 0.42f;
        }

        AudioClip clip = AudioClip.Create("Vestigia_Final_Door_Wood_Resonance", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void SetGroup(CanvasGroup group, bool visible, float alpha)
    {
        if (group == null)
            return;
        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
        if (!visible && alpha <= 0f)
            group.alpha = 0f;
    }
}
