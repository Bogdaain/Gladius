using Gladius.Combat;
using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Combat sound, built on the same pooled-one-shot pattern as PlayerAudio.
    /// Impact sounds (hit flesh, block clang, parry ring, death) come straight off
    /// this character's <see cref="Health.Hit"/> event, so they're always in sync
    /// with the damage. Swing "whoosh" clips are played by the combat script
    /// through PlaySwing, wired on its onLightSwing / onHeavySwing events.
    ///
    /// Works on the player and on enemies; just give it the clips you want each
    /// to use. A random clip is picked each time, with a little pitch variation,
    /// exactly like the footstep system.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CombatAudio : MonoBehaviour
    {
        [Header("Swings (call PlaySwing / PlayHeavySwing from the combat events)")]
        [SerializeField] private AudioClip[] lightSwings;
        [SerializeField] private AudioClip[] heavySwings;

        [Header("Impacts (auto-played from Health)")]
        [SerializeField] private AudioClip[] fleshHits;
        [SerializeField] private AudioClip[] blocks;
        [SerializeField] private AudioClip[] parries;
        [SerializeField] private AudioClip[] deaths;

        [Header("Volume / feel")]
        [SerializeField, Range(0f, 1f)] private float swingVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.9f;
        [SerializeField] private float pitchVariation = 0.08f;
        [Tooltip("0 = our own 2D sound (player). 1 = positioned in the world (enemies).")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;

        private const int PoolSize = 6;

        private Health health;
        private AudioSource[] pool;
        private int poolIndex;

        private void Awake()
        {
            health = GetComponent<Health>();
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = spatialBlend;
                pool[i] = src;
            }
        }

        private void OnEnable() => health.Hit += OnHit;
        private void OnDisable() => health.Hit -= OnHit;

        private void OnHit(HitInfo hit, HitResult result)
        {
            switch (result)
            {
                case HitResult.Hit: PlayRandom(fleshHits, impactVolume); break;
                case HitResult.Blocked: PlayRandom(blocks, impactVolume); break;
                case HitResult.Parried: PlayRandom(parries, impactVolume); break;
            }

            if (!health.IsAlive) PlayRandom(deaths, impactVolume);
        }

        // Hook these on the combat script's swing events (onLightSwing / onHeavySwing).
        public void PlaySwing() => PlayRandom(lightSwings, swingVolume);
        public void PlayHeavySwing() => PlayRandom(heavySwings, swingVolume);

        private void PlayRandom(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            var src = pool[poolIndex];
            poolIndex = (poolIndex + 1) % PoolSize;
            src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            src.PlayOneShot(clip, volume);
        }
    }
}
