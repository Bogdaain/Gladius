using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Shoves a character when it gets hit. Listens to <see cref="Health.Hit"/>,
    /// so it reacts to the same blow the damage came from, using that blow's
    /// direction. A heavy hit pushes harder than a light; a blocked hit pushes
    /// less; a parry doesn't push at all (the attacker is the one who suffers).
    ///
    /// Drives the push through CharacterController.Move with a decaying velocity,
    /// which plays nicely alongside a mover that calls Move the same frame (both
    /// displacements accumulate). Falls back to moving the transform if there's
    /// no controller.
    ///
    /// Runs on UNSCALED time on purpose. The hitstop that fires on the same blow
    /// drops Time.timeScale to near zero, so a scaled-time shove would stall for
    /// the whole freeze and then limp out late. On unscaled time the push plays
    /// through the freeze-frame, so a good hit launches the target during the
    /// pause, the satisfying "freeze, then they fly" read.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Knockback : MonoBehaviour
    {
        [Header("Strength (metres/sec at the start of the shove)")]
        [SerializeField] private float lightForce = 4f;
        [SerializeField] private float heavyForce = 8f;
        [Tooltip("Multiplier applied when the hit was blocked rather than clean.")]
        [SerializeField, Range(0f, 1f)] private float blockedScale = 0.4f;

        [Header("Feel")]
        [Tooltip("How quickly the shove bleeds off. Higher = snappier, shorter push.")]
        [SerializeField] private float decay = 12f;
        [Tooltip("Keep the push flat so hits don't pop characters into the air.")]
        [SerializeField] private bool horizontalOnly = true;

        private Health health;
        private CharacterController controller;
        private Vector3 velocity;

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<CharacterController>();
        }

        private void OnEnable() => health.Hit += OnHit;
        private void OnDisable() => health.Hit -= OnHit;

        private void OnHit(HitInfo hit, HitResult result)
        {
            if (result == HitResult.Parried) return; // parry: no shove on the defender

            Vector3 dir = hit.Direction;
            if (horizontalOnly) dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            float force = hit.Type == AttackType.Heavy ? heavyForce : lightForce;
            if (result == HitResult.Blocked) force *= blockedScale;

            velocity = dir * force;
        }

        private void Update()
        {
            if (velocity.sqrMagnitude < 0.0001f) return;

            // Unscaled so the shove isn't stalled by the hitstop freeze (see class note).
            float dt = Time.unscaledDeltaTime;

            if (controller != null && controller.enabled)
                controller.Move(velocity * dt);
            else
                transform.position += velocity * dt;

            velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-decay * dt));
        }
    }
}
