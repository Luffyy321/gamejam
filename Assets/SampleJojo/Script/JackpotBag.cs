using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Sac d'inventaire posé aux pieds du joueur (il le suit).
/// La machine rangée est cachée à l'ouverture du sac : on plonge la main, on serre, on la tient.
/// On la lâche près du sac pour la ranger.
/// </summary>
public class JackpotBag : MonoBehaviour
{
    [Header("Contenu")]
    public JackpotItem item;
    public bool startInBag = true;

    [Header("Position")]
    [Tooltip("Décoché : le sac reste posé là où il est dans la scène (à côté de la table).")]
    public bool followPlayer = false;
    [Tooltip("Si le sac suit le joueur : x = à droite, y = hauteur, z = devant (mètres).")]
    public Vector3 offset = new Vector3(0.22f, 0f, 0.38f);
    public float followSpeed = 5f;
    [Tooltip("Hauteur de la zone où l'on attrape la machine (ouverture du sac).")]
    public float handleHeight = 0.24f;
    [Tooltip("Distance à laquelle on peut ranger la machine en la lâchant.")]
    public float storeRadius = 0.6f;

    [Header("Sons")]
    public AudioSource audioSource;
    public AudioClip takeClip;
    public AudioClip storeClip;

    [Header("Lueur du sac")]
    public Renderer glow;
    public Color fullColor = new Color(1f, 0.75f, 0.2f);
    public Color emptyColor = new Color(0.15f, 0.12f, 0.1f);
    public Color dropColor = new Color(0.3f, 1f, 0.4f);

    [Header("Pièces")]
    [Tooltip("Rayon de la zone où une pièce lâchée est récoltée.")]
    public float collectRadius = 0.3f;
    [Tooltip("Hauteur de la zone de récolte au-dessus du sol.")]
    public float collectTop = 0.8f;
    public AudioClip coinClip;
    [Tooltip("Texte au-dessus du sac (total récolté).")]
    public TMP_Text coinText;
    public string coinFormat = "{0} pièces";
    public int coinValue;
    public int coinCount;
    public UnityEvent<int> onCoinsChanged;

    public static JackpotBag Active { get; private set; }

    /// <summary>Total compté sur la calculatrice : pièces rangées dans le sac + pièces dans le bocal.</summary>
    public int Total => coinValue + (CoinJar.Active != null ? CoinJar.Active.Value : 0);

    /// <summary>Fin d'un cours : tout ce qu'on possède (sac + bocal + pièces qui traînent sur la table).</summary>
    public int TotalWithLoose => Total + (CoinJar.Active != null ? CoinJar.Active.LooseValue() : 0);

    /// <summary>Le bocal (ou autre) a changé : on met à jour les afficheurs.</summary>
    public void NotifyChanged()
    {
        UpdateCoinText();
        onCoinsChanged?.Invoke(Total);
    }

    Transform cam;
    Transform rigRoot;
    bool hasItem;
    Material glowMat;
    int glowState = -1;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public bool HasItem => hasItem;
    public float FloorY => followPlayer && rigRoot != null ? rigRoot.position.y : transform.position.y;
    public Vector3 HandlePoint => transform.position + Vector3.up * handleHeight;

    void Awake()
    {
        Active = this;
        FindRig();
        if (glow != null)
        {
            glowMat = glow.material;
            glowMat.EnableKeyword("_EMISSION");
            glowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        // Le sac n'a pas besoin de collider : c'est la machine (cachée) qu'on attrape.
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    void Start()
    {
        if (item != null)
        {
            // La machine doit exister dans la scène (si elle a été désactivée, on la rallume) : elle démarre dans le sac.
            if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
            item.bag = this;
            // La machine ne doit pas pousser le joueur (dans le sac ou dans les mains).
            var cc = rigRoot != null ? rigRoot.GetComponentInChildren<CharacterController>() : null;
            if (cc != null)
                foreach (var c in item.GetComponentsInChildren<Collider>(true))
                    Physics.IgnoreCollision(c, cc);
            if (startInBag) Store(item, false);
        }
        if (followPlayer && cam != null) SnapToPlayer(true);
        // Nos pièces (6 au départ, ou celles du cours précédent) sont posées dans le bocal.
        int total = GameProgress.CarryCoins ? GameProgress.Coins : GameBalance.StartCoins;
        coinValue = CoinJar.Active != null ? CoinJar.Active.Fill(total, !GameProgress.CarryCoins) : total;
        NotifyChanged();
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    // ---------------- Pièces ----------------
    public bool InCollectZone(Vector3 p)
    {
        Vector3 d = p - transform.position;
        return new Vector2(d.x, d.z).magnitude < collectRadius && p.y > FloorY - 0.05f && p.y < FloorY + collectTop;
    }

    public void Collect(JackpotCoin coin)
    {
        if (coin == null || coin.IsCollected) return;
        coinValue += coin.value;
        coinCount++;
        coin.CollectInto(HandlePoint);
        Play(coinClip);
        NotifyChanged();
    }

    /// <summary>Change le total (achat, nouveau niveau…) et prévient les afficheurs.</summary>
    public void SetCoins(int value)
    {
        coinValue = Mathf.Max(0, value);
        NotifyChanged();
    }

    /// <summary>Paie <paramref name="amount"/> pièces si on en a assez.</summary>
    public bool Spend(int amount)
    {
        if (amount > Total) return false;
        int fromBag = Mathf.Min(coinValue, amount);
        coinValue -= fromBag;
        int rem = amount - fromBag;
        if (rem > 0 && CoinJar.Active != null) coinValue += CoinJar.Active.Take(rem);   // monnaie rendue dans le sac
        NotifyChanged();
        return true;
    }

    void UpdateCoinText()
    {
        if (coinText != null) coinText.text = string.Format(coinFormat, Total);
    }

    void FindRig()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null)
        {
            rigRoot = origin.transform;
            if (origin.Camera != null) cam = origin.Camera.transform;
        }
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
    }

    // ---------------- Suivre le joueur ----------------
    void LateUpdate()
    {
        bool reaching = hasItem && item != null && item.Grab.isHovered;      // main dans le sac : on ne bouge plus
        if (followPlayer && !reaching)
        {
            if (cam == null) FindRig();
            if (cam != null) SnapToPlayer(false);
        }
        if (hasItem && item != null) item.FollowBag(HandlePoint, transform.rotation);
        UpdateGlow(reaching);
        // Le compteur regarde le joueur.
        if (coinText != null && cam != null)
        {
            Vector3 d = coinText.transform.position - cam.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) coinText.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }

    void SnapToPlayer(bool instant)
    {
        Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(cam.up, Vector3.up);
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 target = new Vector3(cam.position.x, FloorY, cam.position.z) + right * offset.x + fwd * offset.z + Vector3.up * offset.y;
        Quaternion rot = Quaternion.LookRotation(-fwd, Vector3.up);      // face avant vers le joueur
        float k = instant ? 1f : 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude > 4f) k = 1f;      // après une téléportation
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, target, k), Quaternion.Slerp(transform.rotation, rot, k));
    }

    // ---------------- Ranger / sortir ----------------
    /// <summary>La machine est-elle au-dessus / à côté du sac ? (maxHeight : hauteur max au-dessus du sol)</summary>
    public bool IsNear(JackpotItem it, float maxHeight = 1.0f)
    {
        Vector3 c = it.Center;
        Vector3 d = c - transform.position;
        return new Vector2(d.x, d.z).magnitude < storeRadius && c.y < FloorY + maxHeight;
    }

    public void Store(JackpotItem it, bool withSound = true)
    {
        var g = it.Grab;
        if (g.isSelected && g.interactionManager != null)
        {
            // Pas de « lancer » quand on la range (elle devient cinématique dans le sac).
            bool throwWas = g.throwOnDetach;
            g.throwOnDetach = false;
            foreach (var s in new List<IXRSelectInteractor>(g.interactorsSelecting))
                g.interactionManager.SelectExit(s, (IXRSelectInteractable)g);
            StartCoroutine(RestoreThrow(g, throwWas));
        }
        item = it;
        it.SetStored(true);
        it.FollowBag(HandlePoint, transform.rotation);
        hasItem = true;
        if (withSound) Play(storeClip);
    }

    IEnumerator RestoreThrow(XRGrabInteractable g, bool value)
    {
        yield return null;
        yield return null;
        if (g != null) g.throwOnDetach = value;
    }

    /// <summary>Appelé par la machine quand une main l'attrape dans le sac.</summary>
    public void OnTakenOut()
    {
        hasItem = false;
        Play(takeClip);
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // ---------------- Lueur (couleur changée seulement quand l'état change) ----------------
    void UpdateGlow(bool reaching)
    {
        if (glowMat == null) return;
        int state = hasItem ? (reaching ? 1 : 0) : 2;
        if (!hasItem && item != null && item.Grab.isSelected && IsNear(item)) state = 3;
        if (state == glowState) return;
        glowState = state;
        Color c = state == 3 ? dropColor : hasItem ? fullColor : emptyColor;
        float intensity = state == 1 ? 3f : state == 0 ? 1.4f : state == 3 ? 2.5f : 0f;
        glowMat.SetColor(BaseColorId, c);
        glowMat.SetColor(EmissionId, c * intensity);
    }

    // ---------------- Tests sans casque ----------------
    public void TestPlaceOn(JackpotSocket socket)
    {
        if (item == null || socket == null) return;
        StartCoroutine(TestPlaceRoutine(socket));
    }

    IEnumerator TestPlaceRoutine(JackpotSocket socket)
    {
        Store(item, false);                              // libère un éventuel autre emplacement
        yield return null;
        hasItem = false;
        item.SetStored(false);
        item.PlaceCenterAt(socket.transform.position + Vector3.up * 0.25f, socket.transform.rotation);
        Play(takeClip);
        yield return null;
        var manager = item.Grab.interactionManager;
        if (manager != null) manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)item.Grab);
    }

    public void TestStore()
    {
        if (item != null && !hasItem) Store(item);
    }
}
