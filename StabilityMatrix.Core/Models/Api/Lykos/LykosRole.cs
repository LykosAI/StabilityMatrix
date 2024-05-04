namespace StabilityMatrix.Core.Models.Api.Lykos;

public enum LykosRole
{
    Unknown = -1,
    Basic = 0,
    Supporter = 1,
    PatreonSupporter = 2,
    Insider = 3,

    // 4 and 5 have been retired 🫡
    [Obsolete("Roles restructured, use the new BetaTester role")]
    OLD_BetaTester = 4,

    [Obsolete("Roles restructured, use the new Developer role")]
    OLD_Developer = 5,
    Pioneer = 6,
    Visionary = 7,
    BetaTester = 100,
    Translator = 101,
    Developer = 900,
}
