/// <summary>
/// Swappable persistence boundary for player progression. PlayerProgression only ever talks
/// to this interface, never to a concrete storage mechanism — so the backend (in-memory stub,
/// PlayerPrefs, a JSON file, Firebase, SQL, whatever) can change without touching
/// PlayerProgression or any UI code.
/// </summary>
public interface IPlayerDataStore
{
    PlayerData Load();
    void Save(PlayerData data);
}
