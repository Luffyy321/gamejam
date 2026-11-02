using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Évite de rester bloqué quand on change de salle ou de scène avec un objet dans les mains :
/// - avant une téléportation ou un rechargement de scène, on lâche tout ce qu'on tient
///   (la machine, si elle n'est pas posée, retourne dans le sac) ;
/// - les commandes de déplacement sont réactivées (elles sont coupées pendant qu'on tient un objet
///   au rayon, et ne revenaient pas si l'objet disparaissait pendant qu'on le tenait).
/// </summary>
public static class PlayerSafety
{
    /// <summary>Lâche tout ce que les mains tiennent et rend les déplacements.</summary>
    public static void ReleaseAll()
    {
        int n = 0;
        foreach (var it in Object.FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None))
        {
            if (it == null || it is XRSocketInteractor || !it.hasSelection || it.interactionManager == null) continue;
            it.interactionManager.CancelInteractorSelection((IXRSelectInteractor)it);
            n++;
        }
        // La machine pas posée sur son emplacement retourne dans le sac (elle ne reste pas dans la main ou en l'air).
        var machine = Object.FindFirstObjectByType<JackpotItem>();
        if (machine != null && !machine.IsStored && !machine.IsPlaced && JackpotBag.Active != null) JackpotBag.Active.Store(machine);
        if (n > 0) Debug.Log($"[Joueur] {n} objet(s) lâché(s) avant le changement de salle.");
        RestoreMovement();
    }

    /// <summary>Réactive les commandes (déplacement, rotation) et le corps du joueur.</summary>
    public static void RestoreMovement()
    {
        foreach (var iam in Object.FindObjectsByType<InputActionManager>(FindObjectsSortMode.None))
            iam.EnableInput();
        // EnableInput rallume TOUTES les commandes, y compris « tourner » sur le joystick gauche (qui sert à avancer) :
        // on redemande à chaque manette de remettre ses commandes comme il faut (gauche = avancer, droite = tourner).
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "ControllerInputActionManager" || !mb.isActiveAndEnabled) continue;
            var prop = mb.GetType().GetProperty("smoothMotionEnabled");
            if (prop != null && prop.CanRead && prop.CanWrite) prop.SetValue(mb, prop.GetValue(mb));
        }
        foreach (var cc in Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
            if (!cc.enabled) cc.enabled = true;
    }

    // Après chaque chargement de scène : on s'assure que les commandes marchent.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnScene;
        SceneManager.sceneLoaded += OnScene;
    }

    static void OnScene(Scene s, LoadSceneMode m)
    {
        var go = new GameObject("Sécurité joueur");
        go.AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        IEnumerator Start()
        {
            // L'ancienne scène finit de se décharger (elle coupe les commandes en partant) : on les remet après.
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                RestoreMovement();
            }
            Destroy(gameObject);
        }
    }
}
