using UnityEngine;

/// <summary>Fait tourner les pales du ventilateur.</summary>
public class FanSpin : MonoBehaviour
{
    [Tooltip("Axe de rotation, dans le repère local de cet objet.")]
    public Vector3 localAxis = Vector3.forward;
    [Tooltip("Tours par seconde.")]
    public float turnsPerSecond = 4f;

    void Update()
    {
        transform.Rotate(localAxis, turnsPerSecond * 360f * Time.deltaTime, Space.Self);
    }
}
