using System.Collections.Generic;

/// <summary>
/// Ce qui est gardé d'un cours à l'autre (la scène est rechargée) : niveau, numéro du cours, pièces, crédits, objets achetés.
/// Niveau (Chapter) = 3 cours + l'examen. Level = numéro du cours dans le niveau (1 à 3).
/// </summary>
public static class GameProgress
{
    /// <summary>Nombre de niveaux jouables.</summary>
    public const int MaxChapter = 2;

    /// <summary>Note du dernier examen (bonnes réponses) et nombre de questions.</summary>
    public static int LastExamScore, LastExamQuestions;
    /// <summary>Dernier examen réussi (au moins 50 % de bonnes réponses).</summary>
    public static bool LastExamPassed;
    /// <summary>Objectif de pièces du dernier niveau : pièces à la fin, objectif, atteint ; niveau validé (examen + objectif).</summary>
    public static int LastGoalCoins, LastGoal;
    public static bool LastGoalReached, LastLevelValidated;
    public static int LastExamPercent => LastExamQuestions > 0 ? UnityEngine.Mathf.RoundToInt(100f * LastExamScore / LastExamQuestions) : 0;

    /// <summary>Niveau en cours (1, 2…).</summary>
    public static int Chapter = 1;
    /// <summary>Plus haut niveau débloqué.</summary>
    public static int UnlockedChapter = 1;
    /// <summary>Cours dans le niveau (1 à 3, l'examen est à la fin du 3e).</summary>
    public static int Level = 1;
    /// <summary>Pièces (bocal) et crédits dans la machine.</summary>
    public static int Coins, Credits;
    /// <summary>Vrai : on reprend nos pièces (sinon : les pièces de départ).</summary>
    public static bool CarryCoins;
    /// <summary>Dopamine et bonus de la seringue, gardés d'un cours à l'autre du niveau.</summary>
    public static float Dopamine, DopamineBoost;
    public static readonly List<string> Purchased = new List<string>();

    /// <summary>Nombre de fois attrapé par la prof dans ce niveau (au 3e, on recommence tout le niveau).</summary>
    public static int Strikes;
    public const int MaxStrikes = 3;
    /// <summary>Dernier « attrapé » : vrai si on a dû recommencer tout le niveau.</summary>
    public static bool LastCaughtRestartedLevel;
    /// <summary>Vrai juste après un « attrapé » (pour l'affichage au début du cours).</summary>
    public static bool JustCaught;

    // Début du niveau et début du cours en cours (pour recommencer si la prof nous attrape).
    struct Snapshot { public int coins, credits; public bool carry; public float dopamine, boost; public List<string> items; }
    static Snapshot chapterStart, classStart;

    static Snapshot Take() => new Snapshot { coins = Coins, credits = Credits, carry = CarryCoins, dopamine = Dopamine, boost = DopamineBoost, items = new List<string>(Purchased) };
    static void Restore(Snapshot s)
    {
        Coins = s.coins; Credits = s.credits; CarryCoins = s.carry; Dopamine = s.dopamine; DopamineBoost = s.boost;
        Purchased.Clear();
        if (s.items != null) Purchased.AddRange(s.items);
    }

    /// <summary>Nouvelle partie (depuis le menu).</summary>
    public static void Reset()
    {
        Chapter = 1;
        UnlockedChapter = 1;
        Level = 1;
        Coins = Credits = 0;
        CarryCoins = false;
        Dopamine = DopamineBoost = 0f;
        Strikes = 0;
        JustCaught = LastCaughtRestartedLevel = false;
        LastExamScore = LastExamQuestions = 0;
        LastExamPassed = false;
        LastGoalCoins = LastGoal = 0;
        LastGoalReached = LastLevelValidated = false;
        Purchased.Clear();
        chapterStart = classStart = Take();
    }

    /// <summary>
    /// La prof nous a attrapés : les 2 premières fois on recommence le cours (avec ce qu'on avait au début du cours),
    /// la 3e fois on recommence tout le niveau (avec ce qu'on avait au début du niveau).
    /// </summary>
    public static void Caught()
    {
        Strikes++;
        JustCaught = true;
        if (Strikes >= MaxStrikes)
        {
            Strikes = 0;
            LastCaughtRestartedLevel = true;
            RestartChapter();
        }
        else
        {
            LastCaughtRestartedLevel = false;
            Restore(classStart);
        }
    }

    /// <summary>On recommence le niveau en cours (1er cours) avec ce qu'on avait à son début.</summary>
    public static void RestartChapter()
    {
        Level = 1;
        Restore(chapterStart);
        classStart = Take();
    }

    /// <summary>Cours suivant en gardant les pièces et les crédits.</summary>
    public static void NextLevel(int coins, int credits)
    {
        Level++;
        Coins = coins;
        Credits = credits;
        CarryCoins = true;
        JustCaught = false;
        classStart = Take();
    }

    /// <summary>Choix du niveau : on commence ce niveau au 1er cours, avec nos pièces.</summary>
    public static void StartChapter(int chapter, int coins, int credits)
    {
        Chapter = chapter;
        Level = 1;
        Coins = coins;
        Credits = credits;
        CarryCoins = true;
        Dopamine = DopamineBoost = 0f;      // nouveau niveau : dopamine à zéro
        Strikes = 0;
        JustCaught = false;
        chapterStart = classStart = Take();
    }
}
