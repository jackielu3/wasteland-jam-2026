using UnityEngine;
using UnityEngine.UI;

public class BatteryBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private PlayerBattery _battery;

    private void Awake()
    {
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    private void Start()
    {
        InvokeRepeating(nameof(TryBind), 0f, 0.25f);
    }

    private void TryBind()
    {
        if (_battery != null) return;

        foreach (var b in Object.FindObjectsByType<PlayerBattery>(FindObjectsSortMode.None))
        {
            if (b.IsOwner)
            {
                _battery = b;
                _battery.OnBatteryChanged += HandleBatteryChanged;
                HandleBatteryChanged(_battery.Battery.Value, _battery.MaxBattery);
                CancelInvoke(nameof(TryBind));
                return;
            }
        }
    }

    private void Update()
    {
        if (_battery != null)
        {
            HandleBatteryChanged(_battery.Battery.Value, _battery.MaxBattery);
        }
    }

    private void HandleBatteryChanged(float current, float max)
    {
        if (fillImage == null) return;

        float t = (max <= 0f) ? 0f : Mathf.Clamp01(current / max);
        fillImage.fillAmount = t;
    }

    private void OnDestroy()
    {
        if (_battery != null)
            _battery.OnBatteryChanged -= HandleBatteryChanged;
    }
}
