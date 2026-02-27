/// <summary>
/// Defines the capture state of a zone, including captured and currently-capturing states.
/// </summary>
public enum CurrentCaptureState
{
    Neutral = 0,
    Contested = 1,
    CapturedTeamA = 2,
    CapturedTeamB = 3,
    CapturingTeamA = 4,
    CapturingTeamB = 5
}