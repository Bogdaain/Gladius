using Gladius.Combat;
using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Combat sound, built on the same pooled-one-shot pattern as PlayerAudio, and
    /// wired the same way: it subscribes to the combat events in code (AddListener),
    /// so nothing needs dragging in the inspector. Just drop the component on a
    /// character with a Health (and a PlayerCombat or EnemyCombat) and fill in clips.
    ///
    /// Impacts (flesh, block, parry, death) come off Health.Hit. Swings, guard, and
    /// telegraph come off whichever combat script is on the same object. A random
    /// clip is picked each time with a little pitch variation, like the footsteps.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CombatAudio : MonoBehaviour
    {
        [Header("Swings")]
        [SerializeField] private AudioClip[] lightSwings;
        [SerializeField] private AudioClip[] heavySwings;
        [Tooltip("Enemy wind-up before a swing, a cue the player can parry to.")]
        [SerializeField] private AudioClip[] telegraphs;

        [Header("Guard")]
        [SerializeField] private AudioClip[] guardRaises;
        [SerializeField] private AudioClip[] guardDrops;

        [Header("Impacts")]
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
        private PlayerCombat playerCombat; // one or the other, depending on the character
        private EnemyCombat enemyCombat;
        private AudioSource[] pool;
        private int poolIndex;

        private void Awake()
        {
            health = GetComponent<Health>();
            playerCombat = GetComponent<PlayerCombat>();
            enemyCombat = GetComponent<EnemyCombat>();

            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = spatialBlend;
                pool[i] = src;
            }
        }

        // Subscribe in code (AddListener), matching how PlayerAudio hooks the
        // movement events. Nothing needs wiring in the inspector.
        private void OnEnable()
        {
            health.Hit += OnHit;

            if (playerCombat != null)
            {
                playerCombat.onLightSwing.AddListener(PlaySwing);
                playerCombat.onHeavySwing.AddListener(PlayHeavySwing);
                playerCombat.onGuardRaise.AddListener(PlayGuardRaise);
                playerCombat.onGuardDrop.AddListener(PlayGuardDrop);
            }

            if (enemyCombat != null)
            {
                enemyCombat.onTelegraph.AddListener(PlayTelegraph);
                enemyCombat.onSwing.AddListener(PlaySwing);
            }
        }

        private void OnDisable()
        {
            health.Hit -= OnHit;

            if (playerCombat != null)
            {
                playerCombat.onLightSwing.RemoveListener(PlaySwing);
                playerCombat.onHeavySwing.RemoveListener(PlayHeavySwing);
                playerCombat.onGuardRaise.RemoveListener(PlayGuardRaise);
                playerCombat.onGuardDrop.RemoveListener(PlayGuardDrop);
            }

            if (enemyCombat != null)
            {
                enemyCombat.onTelegraph.RemoveListener(PlayTelegraph);
                enemyCombat.onSwing.RemoveListener(PlaySwing);
            }
        }

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

        private void PlaySwing() => PlayRandom(lightSwings, swingVolume);
        private void PlayHeavySwing() => PlayRandom(heavySwings, swingVolume);
        private void PlayTelegraph() => PlayRandom(telegraphs, swingVolume);
        private void PlayGuardRaise() => PlayRandom(guardRaises, swingVolume);
        private void PlayGuardDrop() => PlayRandom(guardDrops, swingVolume);

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
