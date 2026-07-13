using UnityEngine;

[DisallowMultipleComponent]
public sealed class VestigiaPhysicalNote : MonoBehaviour
{
    public const string DefaultPrompt = "Press E to read";

    [SerializeField] private VestigiaNoteData noteData;
    [SerializeField] private string prompt = DefaultPrompt;
    [SerializeField] private string inlineTitle = VestigiaNoteData.IntroTitle;
    [SerializeField, TextArea(12, 28)] private string inlineBody = VestigiaNoteData.IntroBody;

    public string Prompt => string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt;
    public string Title => noteData != null ? noteData.Title : inlineTitle;
    public string Body => noteData != null ? noteData.Body : inlineBody;
    public VestigiaNoteData Data => noteData;

    public void Configure(VestigiaNoteData data)
    {
        noteData = data;
    }

    public void SetInlineText(string title, string body)
    {
        noteData = null;
        inlineTitle = title;
        inlineBody = body;
    }

    public void ResetToIntroNote()
    {
        noteData = null;
        prompt = DefaultPrompt;
        inlineTitle = VestigiaNoteData.IntroTitle;
        inlineBody = VestigiaNoteData.IntroBody;
    }
}
