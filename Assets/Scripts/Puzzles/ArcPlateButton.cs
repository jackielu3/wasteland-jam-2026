using UnityEngine;

public class ArcButtonPlate : MonoBehaviour
{
    [SerializeField] private int plateId = 0;

    [Tooltip("How long this button stays pressed after being hit by an arc.")]
    [SerializeField] private float pressSeconds = 0.12f;

    public int PlateId => plateId;
    public float PressSeconds => pressSeconds;
}
