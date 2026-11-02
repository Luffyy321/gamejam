using TMPro;
using UnityEngine;

/// <summary>
/// Affiche le total des pièces récoltées dans le sac (sur l'écran de la calculatrice).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class CoinCounterDisplay : MonoBehaviour
{
    public JackpotBag bag;
    [Tooltip("{0} = valeur totale des pièces dans le sac.")]
    public string format = "{0}";

    TMP_Text text;
    JackpotBag listening;

    void Awake() => text = GetComponent<TMP_Text>();

    void OnEnable() => Hook();
    void Start() { Hook(); Refresh(); }

    void Hook()
    {
        if (bag == null) bag = JackpotBag.Active != null ? JackpotBag.Active : FindFirstObjectByType<JackpotBag>();
        if (bag == null || listening == bag) return;
        bag.onCoinsChanged.AddListener(OnChanged);
        listening = bag;
    }

    void OnDisable()
    {
        if (listening != null) listening.onCoinsChanged.RemoveListener(OnChanged);
        listening = null;
    }

    void OnChanged(int value) => Show(value);

    public void Refresh() => Show(bag != null ? bag.Total : 0);

    void Show(int value)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        text.text = string.Format(format, value);
    }
}
