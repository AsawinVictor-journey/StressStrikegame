using System;

/// <summary>
/// Plain data container for a player's progression state. Deliberately has no behavior —
/// all formulas/rules live in PlayerProgression; this is just what gets handed to and read
/// back from an IPlayerDataStore.
///
/// No Coins field: CoinManager (Assets/b-o-o-k/shop system/CoinManager.cs) is the single
/// source of truth for spendable coins, not this store — see PlayerProgression's class header.
/// </summary>
[Serializable]
public class PlayerData
{
    public int XP;
    public int Level;

    public PlayerData()
    {
        XP = 0;
        Level = 1;
    }
}
