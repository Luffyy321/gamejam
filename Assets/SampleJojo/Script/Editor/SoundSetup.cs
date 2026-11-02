using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Branche les sons du dossier « Audio/Sons Jeu VR » sur le jeu, et règle la dopamine.</summary>
public static class SoundSetup
{
    const string Dir = "Assets/SampleJojo/Audio/Sons Jeu VR/";

    static AudioClip C(string file)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Dir + file);
        if (c == null) Debug.LogWarning("[Sons] introuvable : " + file);
        return c;
    }

    static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

    static void Set(Object o) { if (o != null) EditorUtility.SetDirty(o); }

    [MenuItem("Tools/Boutique/33 - Sons du jeu + règles de dopamine", false, 33)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Sons] Quitte le mode Play d'abord."); return; }
        int n = 0;

        // Machine à sous.
        var fx = Find<JackpotFX>();
        if (fx != null) { Undo.RecordObject(fx, "Sons"); fx.leverClip = C("levier.mp3"); fx.winClip = C("jackpot.mp3"); Set(fx); n++; }
        var slot = Find<CoinSlot>();
        if (slot != null) { Undo.RecordObject(slot, "Sons"); slot.insertClip = C("coin dans machine.mp3"); Set(slot); n++; }
        var jar = Find<CoinJar>();
        if (jar != null) { Undo.RecordObject(jar, "Sons"); jar.popClip = C("coin dans le bocal.mp3"); Set(jar); n++; }

        // Boutique.
        foreach (var shop in Object.FindObjectsByType<LockerShop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(shop, "Sons");
            shop.openClip = C("casier-ouvert.mp3");
            shop.buyClip = C("achat_boutique.mp3");
            Set(shop); n++;
        }
        foreach (var door in Object.FindObjectsByType<LockerDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Undo.RecordObject(door, "Sons"); door.openClip = C("casier-ouvert.mp3"); Set(door); }

        // Objets à consommer.
        var eat = C("manger consomable.mp3");
        var fxDop = Find<AdrenalineEffect>();
        if (fxDop != null)
        {
            Undo.RecordObject(fxDop, "Dopamine");
            fxDop.consumeClip = eat;
            // Nouvelles règles : la dopamine ne bouge qu'avec la machine (gagné / perdu) et les objets.
            fxDop.decayPerSecond = GameBalance.DopamineDecayPerSecond;
            fxDop.winGain = GameBalance.DopamineOnWin;
            fxDop.loseGain = GameBalance.DopamineOnLose;
            fxDop.boost = 0f;
            fxDop.value = 0f;
            Set(fxDop); n++;
        }

        // QCM.
        var qcm = Find<QcmSheet>();
        if (qcm != null) { Undo.RecordObject(qcm, "Sons"); qcm.scribbleClip = C("coche une case.wav"); Set(qcm); n++; }

        // Prof.
        var prof = Find<ProfAI>();
        if (prof != null)
        {
            Undo.RecordObject(prof, "Sons");
            prof.noticeClip = C("prof nous remarque.mp3");
            prof.alertClip = C("prof énervé quand elle nous cours dessus.wav");
            prof.arriveClip = C("cout de pied sauté de la prof.wav");
            if (prof.audioSource == null) prof.audioSource = prof.gameObject.AddComponent<AudioSource>();
            prof.audioSource.spatialBlend = 0.8f;
            prof.audioSource.playOnAwake = false;
            Set(prof); n++;
        }
        var over = Find<GameOverRestart>();
        if (over != null)
        {
            Undo.RecordObject(over, "Sons");
            over.caughtClip = C("angry prof pointe du doigt.mp3");
            over.restartClip = C("recommence le cours.mp3");
            Set(over); n++;
        }

        // Fin du cours / de l'examen.
        var flow = Find<LevelFlow>();
        if (flow != null)
        {
            Undo.RecordObject(flow, "Sons");
            flow.bellClip = C("soneries fin du cours.mp3");
            flow.examEndClip = C("alarm fin d'examin.mp3");
            Set(flow); n++;
        }

        // Ambiance.
        var amb = Find<ClassroomAmbience>();
        if (amb == null)
        {
            var go = new GameObject("Ambiance classe");
            Undo.RegisterCreatedObjectUndo(go, "Ambiance");
            var room = prof != null ? prof.transform.position : Vector3.zero;
            go.transform.position = new Vector3(room.x, room.y, room.z);
            amb = go.AddComponent<ClassroomAmbience>();
        }
        Undo.RecordObject(amb, "Sons");
        amb.music = C("musique d'ambience.mp3");
        amb.ventilation = C("ambiance ventilation.mp3");
        amb.randomSounds = new[] { C("ambiance classe papier.wav"), C("ambiance éléve tousse.mp3"), C("sneeze_éléves.wav") };
        Set(amb); n++;

        // Musique longue : chargée en streaming (moins de mémoire sur le Quest).
        foreach (var f in new[] { "musique d'ambience.mp3", "ambiance ventilation.mp3" })
        {
            var imp = AssetImporter.GetAtPath(Dir + f) as AudioImporter;
            if (imp == null) continue;
            var st = imp.defaultSampleSettings;
            if (st.loadType != AudioClipLoadType.Streaming) { st.loadType = AudioClipLoadType.Streaming; imp.defaultSampleSettings = st; imp.SaveAndReimport(); }
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[Sons] {n} éléments réglés (machine, bocal, boutique, objets, QCM, prof, fin de cours, ambiance) + règles de dopamine.");
    }
}
