using UnityEngine;
using UnityEngine.UI;

public class StaminaBar : MonoBehaviour
{
    [SerializeField] private PlayerStamina stamina;
    [SerializeField] private Image fill;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Colors")]
    [SerializeField] private Color fullColor = new Color(0.3f, 0.9f, 0.5f);
    [SerializeField] private Color midColor = new Color(0.95f, 0.8f, 0.25f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.35f, 0.25f);
    [SerializeField] private Color exhaustedColor = new Color(0.9f, 0.15f, 0.15f);

    [Header("Ring")]
    [SerializeField, Range(0.05f, 0.9f)] private float thickness = 0.18f;

    [Header("Behaviour")]
    [SerializeField] private float smoothing = 18f;
    [SerializeField] private float fadeDelay = 1f;
    [SerializeField] private float fadeSpeed = 3f;

    private const int RingSize = 256;
    private const float PulseSpeed = 3f;

    private Sprite ring;
    private float shown;
    private float fullSince;

    private void Awake()
    {
        if (stamina == null) stamina = FindFirstObjectByType<PlayerStamina>();
        if (stamina == null || fill == null)
        {
            Debug.LogError("StaminaBar needs a Fill image and a PlayerStamina in the scene.", this);
            enabled = false;
            return;
        }

        ring = MakeRing(thickness);
        fill.sprite = ring;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        fill.preserveAspect = true;

        shown = stamina.Normalized;
    }

    private void OnDestroy()
    {
        if (ring == null) return;
        Destroy(ring.texture);
        Destroy(ring);
    }

    private void Update()
    {
        float target = stamina.Normalized;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        fill.fillAmount = shown;
        fill.color = stamina.IsExhausted ? PulseColor() : LevelColor(shown);

        if (canvasGroup == null) return;

        bool full = target >= 0.999f;
        if (!full) fullSince = Time.time;

        float goal = full && Time.time - fullSince > fadeDelay ? 0f : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, goal, fadeSpeed * Time.deltaTime);
    }

    private Color LevelColor(float t)
    {
        return t > 0.5f
            ? Color.Lerp(midColor, fullColor, (t - 0.5f) * 2f)
            : Color.Lerp(lowColor, midColor, t * 2f);
    }

    private Color PulseColor()
    {
        Color dim = exhaustedColor * 0.5f;
        dim.a = exhaustedColor.a;
        return Color.Lerp(dim, exhaustedColor, Mathf.PingPong(Time.time * PulseSpeed, 1f));
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