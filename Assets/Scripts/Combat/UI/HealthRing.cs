using Gladius.Combat;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Centered ring for the player's health, built to sit alongside the stamina
/// ring and match its look. Generates the same radial ring texture and lerps
/// its colour with the fill, so the two read as a matched pair. Drives off
/// Health.Changed, so it never polls and never touches the damage math.
/// </summary>
public class HealthRing : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Image fill;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Colors")]
    [SerializeField] private Color fullColor = new Color(0.85f, 0.25f, 0.25f);
    [SerializeField] private Color midColor = new Color(0.9f, 0.45f, 0.2f);
    [SerializeField] private Color lowColor = new Color(0.6f, 0.1f, 0.1f);

    [Header("Ring")]
    [SerializeField, Range(0.05f, 0.9f)] private float thickness = 0.18f;

    [Header("Behaviour")]
    [SerializeField] private float smoothing = 18f;
    [Tooltip("Pulse the ring when health is at or below this fraction.")]
    [SerializeField, Range(0f, 1f)] private float lowPulseThreshold = 0.3f;
    [Tooltip("Fade the ring out this long after reaching full health. 0 = never fade.")]
    [SerializeField] private float fadeDelay = 0f;
    [SerializeField] private float fadeSpeed = 3f;

    private const int RingSize = 256;
    private const float PulseSpeed = 3f;

    private Sprite ring;
    private float shown;
    private float target = 1f;
    private float fullSince;

    private void Awake()
    {
        if (health == null) health = FindAnyObjectByType<Health>();
        if (health == null || fill == null)
        {
            Debug.LogError("HealthRing needs a Fill image and a Health in the scene.", this);
            enabled = false;
            return;
        }

        ring = MakeRing(thickness);
        fill.sprite = ring;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.preserveAspect = true;

        shown = health.Normalized;
        target = health.Normalized;
    }

    private void OnEnable()
    {
        if (health != null) health.Changed += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (health != null) health.Changed -= OnHealthChanged;
    }

    private void OnDestroy()
    {
        if (ring == null) return;
        Destroy(ring.texture);
        Destroy(ring);
    }

    private void OnHealthChanged(float current, float max)
    {
        target = max > 0f ? current / max : 0f;
    }

    private void Update()
    {
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        fill.fillAmount = shown;
        fill.color = shown <= lowPulseThreshold ? PulseColor() : LevelColor(shown);

        if (canvasGroup == null || fadeDelay <= 0f) return;

        bool full = target >= 0.999f;
        if (!full) fullSince = Time.time;

        float goal = full && Time.time - fullSince > fadeDelay ? 0f : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, goal, fadeSpeed * Time.deltaTime);
    }

    private Color LevelColor(float t)
    {
        // Full = fullColor, draining toward lowColor through midColor.
        return t > 0.5f
            ? Color.Lerp(midColor, fullColor, (t - 0.5f) * 2f)
            : Color.Lerp(lowColor, midColor, t * 2f);
    }

    private Color PulseColor()
    {
        Color dim = lowColor * 0.5f;
        dim.a = lowColor.a;
        return Color.Lerp(dim, lowColor, Mathf.PingPong(Time.time * PulseSpeed, 1f));
    }

    private static Sprite MakeRing(float thickness)
    {
        var tex = new Texture2D(RingSize, RingSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[RingSize * RingSize];
        float center = (RingSize - 1) * 0.5f;
        float outer = RingSize * 0.5f - 1f;
        float inner = outer * (1f - thickness);

        for (int y = 0; y < RingSize; y++)
        {
            for (int x = 0; x < RingSize; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float alpha = Mathf.Clamp01(outer - dist + 0.5f) * Mathf.Clamp01(dist - inner + 0.5f);
                pixels[y * RingSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, RingSize, RingSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
