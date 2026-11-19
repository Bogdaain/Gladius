using UnityEngine;
using UnityEngine.Events;

namespace Gladius.Combat
{
    /// <summary>
    /// A deliberately simple placeholder enemy: walk toward the target, stop in
    /// range, telegraph, then swing. It exists so clashing, blocking, and parrying
    /// can be tested before the real models and AI land. Swap the visuals and tune
    /// the WeaponData to make the "bulky heavy" vs "light fast" archetypes.
    ///
    /// Same event-driven feedback approach as the player scripts.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyCombat : MonoBehaviour, IClashable, IStunnable, IGuardWeaponSource
    {
        [Header("References")]
        [SerializeField] private WeaponData weapon;
        [SerializeField] private MeleeHitbox hitbox;
        [Tooltip("Who to chase and hit. Auto-finds the object tagged 'Player' if empty.")]
        [SerializeField] private Transform target;

        [Header("Archetype")]
        [Tooltip("Light, fast enemy swings Light. Bulky enemy swings Heavy.")]
        [SerializeField] private AttackType attackType = AttackType.Light;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float turnSpeed = 8f;
        [Tooltip("Gets this close before it stops to attack.")]
        [SerializeField] private float attackRange = 1.8f;

        [Header("Attack rhythm")]
        [Tooltip("Visible wind-up before the swing. Longer = easier to parry.")]
        [SerializeField] private float telegraph = 0.5f;
        [SerializeField] private float attackCooldown = 1.6f;

        [Header("Events (hook telegraph VFX / audio here)")]
        public UnityEvent onTelegraph;
        public UnityEvent onSwing;
        public UnityEvent onRecoil;
        public UnityEvent onStunned;

        // IClashable
        public bool IsSwinging => state == State.WindingUp;
        public AttackType CurrentAttack => attackType;
        public WeaponData GuardWeapon => weapon;

        private enum State { Approaching, WindingUp, Recovering, Stunned }
        private State state = State.Approaching;

        private Health health;
        private CharacterController controller;
        private float stateEndTime;
        private float nextAttackTime;
        private float verticalVelocity;

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<CharacterController>();
            if (target == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) target = p.transform;
            }
        }

        private void Update()
        {
            if (!health.IsAlive) return;
            if (target == null) return;

            float dt = Time.deltaTime;
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            Vector3 dir = dist > 0.001f ? toTarget / dist : transform.forward;

            FaceAndStep(dir, dist, dt);
            TickState(dist);
        }

        private void FaceAndStep(Vector3 dir, float dist, float dt)
        {
            // Always turn toward the player (lets a swing track a little).
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSpeed * dt));

            // Only close distance while approaching and out of range.
            bool shouldMove = state == State.Approaching && dist > attackRange;
            Vector3 horizontal = shouldMove ? dir * moveSpeed : Vector3.zero;

            // Simple gravity so it sits on the ground.
            if (controller != null)
            {
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
                verticalVelocity += -25f * dt;
                controller.Move((horizontal + Vector3.up * verticalVelocity) * dt);
            }
            else
            {
                transform.position += horizontal * dt;
            }
        }

        private void TickState(float dist)
        {
            switch (state)
            {
                case State.Approaching:
                    if (dist <= attackRange && Time.time >= nextAttackTime)
                        BeginTelegraph();
                    break;

                case State.WindingUp:
                    if (Time.time >= stateEndTime) ResolveSwing();
                    break;

                case State.Recovering:
                    if (Time.time >= stateEndTime) state = State.Approaching;
                    break;

                case State.Stunned:
                    if (Time.time >= stateEndTime) state = State.Approaching;
                    break;
            }
        }

        private void BeginTelegraph()
        {
            state = State.WindingUp;
            stateEndTime = Time.time + telegraph;
            onTelegraph?.Invoke();
        }

        private void ResolveSwing()
        {
            onSwing?.Invoke();

            var outcome = hitbox != null
                ? hitbox.Swing(weapon, attackType, gameObject)
                : MeleeHitbox.SwingOutcome.Miss;

            if (outcome == MeleeHitbox.SwingOutcome.Blocked ||
                outcome == MeleeHitbox.SwingOutcome.Clashed)
                onRecoil?.Invoke();

            // Parry stuns us via ApplyParryStun before we get here; if still winding,
            // drop into recovery.
            if (state == State.WindingUp)
            {
                state = State.Recovering;
                stateEndTime = Time.time + weapon.RecoveryFor(attackType);
                nextAttackTime = Time.time + attackCooldown;
            }
        }

        // ───────────── IClashable / IStunnable ─────────────

        public void OnClash(AttackType incoming)
        {
            onRecoil?.Invoke();
            state = State.Recovering;
            stateEndTime = Time.time + weapon.RecoveryFor(attackType) * 0.6f;
            nextAttackTime = Time.time + attackCooldown;
        }

        public void ApplyParryStun(float duration)
        {
            state = State.Stunned;
            stateEndTime = Time.time + duration;
            nextAttackTime = Time.time + duration + 0.2f;
            onStunned?.Invoke();
        }
    }
}
