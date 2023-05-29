namespace OpenSpace.Messages;

public class SwitchedToEditorModeMessage
{
    public SwitchedToEditorModeMessage()
    {
        ProgramMode = ProgramMode.Editor;
    }
    
    public ProgramMode ProgramMode { get; }
}