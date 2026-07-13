using UnityEngine;

[CreateAssetMenu(fileName = "VestigiaNote", menuName = "Vestigia/Physical Note Data")]
public sealed class VestigiaNoteData : ScriptableObject
{
    public const string IntroTitle = "VESTIGIA";
    public const string IntroBody =
        "You are trapped here.\n\n" +
        "To escape, you must pass through every room and complete the challenge hidden inside it.\n\n" +
        "Observe carefully.\n" +
        "Remember what you see.\n" +
        "Some details will change.\n" +
        "Some objects will hide the truth.\n\n" +
        "Complete each room to unlock the way forward.\n\n" +
        "Your first clue is closer than you think.\n\n" +
        "Do not trust everything you remember.";

    public const string IntroFullText = IntroTitle + "\n\n" + IntroBody;

    [SerializeField] private string noteTitle = IntroTitle;
    [SerializeField, TextArea(12, 28)] private string noteBody = IntroBody;

    public string Title => noteTitle;
    public string Body => noteBody;

    public void SetText(string title, string body)
    {
        noteTitle = title;
        noteBody = body;
    }

    public void ResetToIntroNote()
    {
        noteTitle = IntroTitle;
        noteBody = IntroBody;
    }
}
