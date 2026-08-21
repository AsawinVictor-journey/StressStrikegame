// Shared "boxing"/"rage_room"/"yoga" <-> GameMode mapping, used by both
// CheckInSceneRouter (to know which scene to load) and CheckInResultPanel
// (to know which wordmark/coach message to show) so the two stay in sync.
public static class CheckInModeMapping
{
    public static bool TryToGameMode(string mode, out GameMode gameMode)
    {
        switch (mode)
        {
            case "boxing": gameMode = GameMode.Boxing; return true;
            case "rage_room": gameMode = GameMode.RageRoom; return true;
            case "yoga": gameMode = GameMode.Meditate; return true;
            default:
                gameMode = GameMode.Meditate;
                return false;
        }
    }
}
