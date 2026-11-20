using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Brief global freeze-frame on impact. Drops Time.timeScale for a short
    /// real-time window, then restores it, which gives hits that satisfying
    /// "crunch". Measured in unscaled time so the pause is a fixed wall-clock
    /// length no matter what timeScale is during it.
    ///
    /// One instance drives the whole game. Call <see cref="Freeze"/> from combat
    /// feedback; overlapping requests keep the longest remaining freeze rather
    /// than stacking, so a flurry of hits doesn't grind the game to a halt.
    /// </summary>
    public class Hitstop : MonoBehaviour
    {
        public static Hitstop Instance { get; private set; }

        [Tooltip("Time scale during a freeze. 0 = full stop; a small value = slow-mo crunch.")]
        [SerializeField, Range(0f, 1f)] private float frozenScale = 0f;
        [Tooltip("Safety cap so nothing can freeze the game longer than this.")]
        [SerializeField] private float maxDuration = 0.25f;

        private float baseScale = 1f;
        private float resumeTime;
        private bool frozen;

        /// <summary>True while a freeze-frame is active. False when no Hitstop exists.</summary>
        public static bool IsFrozen => Instance != null && Instance.frozen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            baseScale = Time.timeScale;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (frozen) Time.timeScale = baseScale;
                Instance = null;
            }
        }

        /// <summary>Freeze for <paramref name="duration"/> real seconds (capped).</summary>
        public void Freeze(float duration)
        {
            if (duration <= 0f) return;
            duration = Mathf.Min(duration, maxDuration);

            if (!frozen)
            {
                baseScale = Time.timeScale; // remember the scale we interrupted
                Time.timeScale = frozenScale;
                frozen = true;
            }

            // Keep the longest pending freeze rather than stacking them.
            resumeTime = Mathf.Max(resumeTime, Time.unscaledTime + duration);
        }

        /// <summary>Convenience static so callers don't need the instance.</summary>
        public static void Do(float duration) => Instance?.Freeze(duration);

        private void Update()
        {
            if (!frozen) return;
            if (Time.unscaledTime >= resumeTime)
            {
                Time.timeScale = baseScale;
                frozen = false;
            }
        }
    }
}
