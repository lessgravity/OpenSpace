namespace OpenSpace.Messages;

public class SwitchedToGameModeMessage
{
    public SwitchedToGameModeMessage()
    {
        ProgramMode = ProgramMode.Game;
    }
    
    public ProgramMode ProgramMode { get; }
}