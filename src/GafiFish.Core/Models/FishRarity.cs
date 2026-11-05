namespace GafiFish.Core.Models;

/// <summary>
/// Ordered rarity categories used for the default session-summary ranking.
/// Unknown values stay below known rarities.
/// </summary>
public enum FishRarity
{
    Unknown = 0,
    Common = 10,
    Unusual = 20,
    Rare = 30,
    Legendary = 40,
    Mythical = 50,
    Exotic = 60,
    Secret = 70,
    Apex = 80
}
