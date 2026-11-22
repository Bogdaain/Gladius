using Gladius.Combat;
using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Adaptive combat music. Five looping stages tied to the player's HP
    /// (full, full→half, half, half→low, low) play in sync on separate sources;
    /// the mix crossfades between the two stages bracketing the current HP, so
    /// changes are seamless with no restarts.
    ///
    /// On top of the HP mix sit three reactive layers:
    ///   - Engagement: the whole track fades in when combat starts (a hit dealt or
    ///     taken) and fades out after a stretch with no fighting.
    ///   - Intensity: dealing damage pushes volume up toward a cap, then it decays
    ///     back to the baseline when you stop landing hits.
    ///   - Accents: a block briefly dips the volume (a "wave" that gives the block
    ///     weight), and a hitstop freeze bends the pitch down so the music slows
    ///     or stops with the freeze-frame, then recovers.
    /// </summary>
    public class CombatMusic : MonoBehaviour
    {
        public enum Stage { Full, FullToHalf, Half, HalfToLow, Low }

        [Header("Loops (one per stage, in HP order high→low)")]
        [SerializeField] private AudioClip fullLoop;
        [SerializeField] private AudioClip fullToHalfLoop;
        [SerializeField] private AudioClip halfLoop;
        [SerializeField] private AudioClip halfToLowLoop;
        [SerializeField] private AudioClip lowLoop;

        [Header("Source")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.7f;
        [Tooltip("HP thresholds (fraction) where each lower stage becomes dominant. " +
                 "Must descend. Order: full→half, half, half→low, low.")]
        [SerializeField] private Vector4 stageThresholds = new Vector4(0.8f, 0.6f, 0.4f, 0.2f);

        [Header("Who to follow")]
        [Tooltip("The player's Health. Auto-found on a 'Player'-tagged object if empty.")]
        [SerializeField] private Health playerHealth;
        [Tooltip("The player's combat, for the 'hit landed' engagement trigger.")]
        [SerializeField] private PlayerCombat playerCombat;

        [Header("Engagement")]
        [Tooltip("Seconds of no combat before the music fades back out.")]
        [SerializeField] private float combatTimeout = 6f;
        [SerializeField] private float fadeInSpeed = 2f;
        [SerializeField] private float fadeOutSpeed = 0.6f;

        [Header("Intensity (dealing damage raises volume to a cap)")]
        [Tooltip("Extra volume multiplier added per hit landed.")]
        [SerializeField] private float intensityPerHit = 0.15f;
        [Tooltip("Cap on the intensity boost. 0.5 = up to +50% over baseline.")]
        [SerializeField] private float intensityCap = 0.5f;
        [Tooltip("How fast intensity bleeds back to zero when not landing hits.")]
        [SerializeField] private float intensityDecay = 0.25f;

        [Header("Block wave (dip then recover)")]
        [SerializeField, Range(0f, 1f)] private float blockDipDepth = 0.5f;
        [SerializeField] private float blockDipRecovery = 3f;

        [Header("Stage crossfade / hitstop")]
        [SerializeField] private float stageBlendSpeed = 2.5f;
        [Tooltip("Lowest pitch the music bends to during a hitstop freeze. 0 = stop.")]
        [SerializeField, Range(0f, 1f)] private float hitstopPitch = 0.05f;
        [SerializeField] private float pitchRecovery = 8f;

        /// <summary>0 when out of combat, 1 when fully engaged. Follows the music
        /// fade, so other audio (e.g. wind) can duck under combat by this amount.</summary>
        public float EngagementLevel => engagement;

        private AudioSource[] sources;     // one per Stage, index matches the enum
        private float[] stageMix;          // current blend weight per stage (0..1)
        private float engagement;          // 0 out of combat, 1 fully engaged
        private float intensity;           // 0..intensityCap boost from landing hits
        private float blockDip;            // 0 normal, up to blockDipDepth right after a block
        private float pitch = 1f;
        private float lastCombatTime = -100f;

        private const int StageCount = 5;

        private void Awake()
        {
            if (playerHealth == null || playerCombat == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    if (playerHealth == null) playerHealth = p.GetComponent<Health>();
                    if (playerCombat == null) playerCombat = p.GetComponent<PlayerCombat>();
                }
            }

            var clips = new[] { fullLoop, fullToHalfLoop, halfLoop, halfToLowLoop, lowLoop };
            sources = new AudioSource[StageCount];
            stageMix = new float[StageCount];
            for (int i = 0; i < StageCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = clips[i];
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 0f; // music is 2D
                src.volume = 0f;
                sources[i] = src;
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.Hit += OnPlayerHit;
            if (playerCombat != null) playerCombat.onHitLanded.AddListener(OnHitLanded);
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.Hit -= OnPlayerHit;
            if (playerCombat != null) playerCombat.onHitLanded.RemoveListener(OnHitLanded);
        }

        private void Start()
        {
            // All loops play together from the start, in sync, silent until mixed up.
            foreach (var s in sources)
                if (s.clip != null) s.Play();
        }

        // ───────────── Engagement triggers ─────────────

        // Player got hit: we're in combat, and a block triggers the dip wave.
        private void OnPlayerHit(HitInfo hit, HitResult result)
        {
            lastCombatTime = Time.time;
            if (result == HitResult.Blocked || result == HitResult.Parried)
                blockDip = blockDipDepth;
        }

        // Player landed a hit: in combat, and intensity climbs toward the cap.
        private void OnHitLanded()
        {
            lastCombatTime = Time.time;
            intensity = Mathf.Min(intensityCap, intensity + intensityPerHit);
        }

        // ───────────── Per-frame mix ─────────────

        private void Update()
        {
            // Use unscaled time so hitstop freezes don't stall the fades/decay;
            // the freeze is expressed through pitch instead (below).
            float dt = Time.unscaledDeltaTime;

            UpdateEngagement(dt);
            UpdateIntensity(dt);
            UpdateBlockDip(dt);
            UpdatePitch(dt);
            UpdateStageMix(dt);
            ApplyToSources();
        }

        private void UpdateEngagement(float dt)
        {
            bool inCombat = Time.time - lastCombatTime <= combatTimeout;
            float target = inCombat ? 1f : 0f;
            float speed = inCombat ? fadeInSpeed : fadeOutSpeed;
            engagement = Mathf.MoveTowards(engagement, target, speed * dt);
        }

        private void UpdateIntensity(float dt)
        {
            intensity = Mathf.MoveTowards(intensity, 0f, intensityDecay * dt);
        }

        private void UpdateBlockDip(float dt)
        {
            blockDip = Mathf.MoveTowards(blockDip, 0f, blockDipRecovery * dt * blockDipDepth);
        }

        private void UpdatePitch(float dt)
        {
            // Bend toward near-zero during a freeze, recover to normal after. Both
            // directions are smoothed so it reads as a quick slow-down/spin-up
            // rather than a hard cut. The dive is faster than the recovery.
            float target = Hitstop.IsFrozen ? hitstopPitch : 1f;
            float speed = Hitstop.IsFrozen ? pitchRecovery * 3f : pitchRecovery;
            pitch = Mathf.MoveTowards(pitch, target, speed * dt);
        }

        private void UpdateStageMix(float dt)
        {
            int active = CurrentStage();
            float k = 1f - Mathf.Exp(-stageBlendSpeed * dt);
            for (int i = 0; i < StageCount; i++)
                stageMix[i] = Mathf.Lerp(stageMix[i], i == active ? 1f : 0f, k);
        }

        /// <summary>Which stage the current HP fraction falls into.</summary>
        private int CurrentStage()
        {
            float hp = playerHealth != null ? playerHealth.Normalized : 1f;
            if (hp >= stageThresholds.x) return (int)Stage.Full;
            if (hp >= stageThresholds.y) return (int)Stage.FullToHalf;
            if (hp >= stageThresholds.z) return (int)Stage.Half;
            if (hp >= stageThresholds.w) return (int)Stage.HalfToLow;
            return (int)Stage.Low;
        }

        private void ApplyToSources()
        {
            // Final volume = master × engagement × (1 + intensity) × (1 - dip), per stage weight.
            float gain = masterVolume * engagement * (1f + intensity) * (1f - blockDip);
            for (int i = 0; i < StageCount; i++)
            {
                if (sources[i].clip == null) continue;
                sources[i].volume = gain * stageMix[i];
                sources[i].pitch = pitch;
            }
        }
    }
}
