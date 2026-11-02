/// <summary>
/// Questions de l'examen du niveau 1 (livre « Les bananes »). 3 sont tirées au hasard à chaque fois.
/// correct = index de la bonne réponse (dans l'ordre écrit ici).
/// </summary>
public static class QcmBank
{
    public class Question
    {
        public string text;
        public string[] answers;
        public int correct;
        public Question(string t, int c, params string[] a) { text = t; correct = c; answers = a; }
    }

    public static readonly Question[] Level1 =
    {
        new Question("Botaniquement, la banane est :", 0, "une baie", "un polyakène", "ça n'existe pas"),
        new Question("De quel type d'arbre est le bananier ?", 2, "feuillus", "conifères", "ce n'est pas un arbre"),
        new Question("La banane est plutôt :", 2, "un légume", "un véhicule", "un fruit"),
        new Question("Que représente la banane pour l'humain ?", 0, "un lointain cousin", "un simple goûter sans aucun lien avec nous", "son patron"),
        new Question("Pourquoi la banane se courbe-t-elle ?", 1, "pour l'aérodynamisme", "pour pousser vers le haut, contre la gravité", "pour simuler l'utilisation d'un téléphone"),
        new Question("Pourquoi la banane est-elle radioactive ?", 0, "à cause du potassium qu'elle contient", "à cause des centrales nucléaires", "parce qu'elle se nourrit de nutriments radioactifs"),
        new Question("Que fait une banane mûre à côté d'un avocat ?", 0, "elle l'aide à mûrir", "elle le mange", "elle meurt"),
        new Question("Quelle est l'unité de mesure la plus importante au monde ?", 2, "le mètre", "le gramme", "la banane"),
    };

    /// <summary>Pages du livre du niveau 1 (« #IMG » = la page affiche l'image de banane).</summary>
    public static readonly string[] BookLevel1 =
    {
        "<size=140%><b>Chapitre</b></size>\n\n<size=170%><b>Les bananes</b></size>",
        "<b>Qu'est-ce que c'est ?</b>\n\nLa banane est un fruit. Mais pour les botanistes, c'est plus précis : c'est une <b>baie</b>, comme la tomate ou le raisin.",
        "<b>Le faux arbre</b>\n\nLe bananier n'est <b>pas un arbre</b>. C'est une très grande herbe, dont le faux tronc est fait de feuilles enroulées les unes dans les autres.",
        "<b>Notre lointain cousin</b>\n\nEnviron la moitié de nos gènes (50 à 60 %) ont un équivalent chez la banane. Elle représente alors un <b>lointain cousin</b> pour nous.",
        "<b>La courbure de la banane</b>\n\nUne banane pousse d'abord vers le bas, accrochée à son régime. Ensuite, elle se redresse <b>vers le haut, contre la gravité</b>. C'est ce virage qui lui donne sa forme courbée.",
        "<b>Radioactive</b>\n\nLa banane contient du <b>potassium</b>, dont une toute petite partie est naturellement radioactive. C'est simplement le potassium qu'elle contient.",
        "<b>Le gaz</b>\n\nEn mûrissant, la banane libère un gaz appelé <b>éthylène</b>. Ce gaz aide les fruits voisins à mûrir plus vite.",
        "#IMG",
        "<b>Unité de mesure</b>\n\nUne banane mesure environ 20 cm et pèse environ 120 g, ce qui en fait, pour les scientifiques sérieux, <b>l'unité de mesure la plus importante au monde</b>.",
    };
}
