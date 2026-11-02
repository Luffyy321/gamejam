/// <summary>
/// Équilibrage du jeu, tout au même endroit (Mc = pièces).
/// Les valeurs ici remplacent celles de l'Inspector au lancement.
/// </summary>
public static class GameBalance
{
    // ---------------- Pièces ----------------
    /// <summary>Pièces au début d'une partie.</summary>
    public const int StartCoins = 6;
    /// <summary>Si le joueur tombe à zéro (plus de pièce nulle part, plus de crédit), on lui redonne ça.</summary>
    public const int RescueCoins = 1;

    // ---------------- Objectif de pièces (post-it) ----------------
    /// <summary>Pièces à avoir à la fin du niveau (niveau 1, 2, 3).</summary>
    public static readonly int[] Goals = { 100, 200, 400 };
    public static int GoalFor(int chapter) => Goals[UnityEngine.Mathf.Clamp(chapter - 1, 0, Goals.Length - 1)];

    // ---------------- Machine à sous ----------------
    // Chance de sortir 3 fois le symbole (le reste du temps : perdu), et gain en Mc.
    public struct SlotOdd { public string symbol; public float chance; public int payout; }
    public static readonly SlotOdd[] Slot =
    {
        new SlotOdd { symbol = "couteau", chance = 0.15f, payout = 5 },     // couteau suisse
        new SlotOdd { symbol = "crous",   chance = 0.10f, payout = 10 },
        new SlotOdd { symbol = "izly",    chance = 0.05f, payout = 20 },
        new SlotOdd { symbol = "claude",  chance = 0.02f, payout = 40 },
        new SlotOdd { symbol = "mmi",     chance = 0.01f, payout = 80 },
    };

    // ---------------- Dopamine ----------------
    /// <summary>Dopamine gagnée quand on perd à la machine.</summary>
    public const float DopamineOnLose = 0.01f;
    /// <summary>Dopamine gagnée quand on gagne à la machine.</summary>
    public const float DopamineOnWin = 0.1f;
    /// <summary>Dopamine perdue par seconde, avec le temps.</summary>
    public const float DopamineDecayPerSecond = 0.005f;
    /// <summary>Après un gain de dopamine, elle ne baisse pas pendant ce temps (s).</summary>
    public const float DopamineHold = 4f;

    // ---------------- Roulette (boutique) ----------------
    public const float RouletteBlack = 0.495f, RouletteRed = 0.495f, RouletteGreen = 0.01f;
    public const int RouletteBlackMult = 2, RouletteRedMult = 2, RouletteGreenMult = 100;

    // ---------------- Objets ----------------
    /// <summary>Café gourmand : temps ralenti (s) et vitesse du temps pendant ce temps.</summary>
    public const float CoffeeSeconds = 30f, CoffeeTimeScale = 0.45f;
}
