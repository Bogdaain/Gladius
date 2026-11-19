using Gladius.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Gladius.Combat.UI
{
    /// <summary>
    /// A "+" crosshair that reflects the heavy-attack charge. Gray at rest, it
    /// lerps toward red as PlayerCombat.ChargeProgress climbs, then blinks once
    /// the charge is full so the player knows a max-power heavy is ready.
    ///
    /// The plus sprite is generated in code (like the stamina ring), so no art is
    /// needed. Reads PlayerCombat; never drives combat, purely a readout.
    /// </summary>
    public class ChargeCrosshair : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private Image image;

        [Header("Colors")]
        [SerializeField] private Color idleColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        [SerializeField] private Color fullColor = new Color(0.95f, 0.2f, 0.2f, 1f);

        [Header("Shape (generated)")]
        [Tooltip("Pixel size of the generated plus sprite.")]
        [SerializeField] private int size = 64;
        [Tooltip("Half-thickness of each arm, in pixels.")]
        [SerializeField] private int armThickness = 3;
        [Tooltip("Gap in the centre, in pixels (0 = solid plus).")]
        [SerializeField] private int centerGap = 6;

        [Header("Behaviour")]
        [SerializeField] private float colorLerp = 10f;
        [Tooltip("Charge fraction at which blinking begins. 1 = only when completely full.")]
        [SerializeField, Range(0.5f, 1f)] private float blinkThreshold = 0.999f;
        [SerializeField] private float blinkSpeed = 8f;

        private Sprite plus;
        private float shown; // smoothed charge

        private void Awake()
        {
            if (combat == null) combat = FindAnyObjectByType<PlayerCombat>();
            if (image == null) image = GetComponent<Image>();
            if (image == null)
            {
                Debug.LogError("ChargeCrosshair needs an Image (assign one or add it to this object).", this);
                enabled = false;
                return;
            }

            plus = MakePlus(size, armThickness, centerGap);
            image.sprite = plus;
            image.color = idleColor;
            image.preserveAspect = true;
        }

        private void OnDestroy()
        {
            if (plus == null) return;
            Destroy(plus.texture);
            Destroy(plus);
        }

        private void Update()
        {
            float charge = combat != null ? combat.ChargeProgress : 0f;
            shown = Mathf.Lerp(shown, charge, 1f - Mathf.Exp(-colorLerp * Time.deltaTime));

            Color c = Color.Lerp(idleColor, fullColor, shown);

            // Blink by modulating alpha once the charge is full enough.
            if (charge >= blinkThreshold)
            {
                float blink = Mathf.PingPong(Time.time * blinkSpeed, 1f);
                c.a = Mathf.Lerp(0.35f, 1f, blink);
            }

            image.color = c;
        }

        /// <summary>Builds a white "+" sprite: two crossed bars with an optional centre gap.</summary>
        private static Sprite MakePlus(int size, int thickness, int gap)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];

            float center = (size - 1) * 0.5f;
            var clear = new Color32(255, 255, 255, 0);
            var solid = new Color32(255, 255, 255, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);

                    bool onVertical = dx <= thickness;             // the vertical bar
                    bool onHorizontal = dy <= thickness;           // the horizontal bar
                    bool inGap = dx <= gap && dy <= gap;           // hollow centre

                    px[y * size + x] = (onVertical || onHorizontal) && !inGap ? solid : clear;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
