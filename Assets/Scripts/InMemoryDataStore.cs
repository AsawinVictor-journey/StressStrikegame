/// <summary>
/// Placeholder IPlayerDataStore with no real persistence: Load() always returns fresh default
/// values, and Save() just holds the given data in memory for the lifetime of the app.
/// Nothing survives an app restart — that's expected until this is swapped for a real backend.
/// </summary>
public class InMemoryDataStore : IPlayerDataStore
{
    private PlayerData storedData;

    public PlayerData Load()
    {
        return storedData ?? new PlayerData();
    }

    public void Save(PlayerData data)
    {
        storedData = data;
    }
}
