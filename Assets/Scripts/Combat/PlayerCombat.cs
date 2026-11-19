using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Gladius.Combat
{
    /// <summary>
    /// Turns mouse input into attacks and guard.
    ///   Left click  : tap for a light attack, hold past the weapon's threshold for a heavy.
    ///   Right click : raise guard. Releasing drops it. A hit taken within the
    ///                 weapon's parry window (measured from the press) is a parry.
    ///
    /// Mirrors the conventions in PlayerController: InputActions built in code,
    /// UnityEvents for juice, and stamina spent through the existing PlayerStamina
    /// pool (optional, auto-found) rather than a private pool.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerCombat : MonoBehaviour, IClashable, IStunnable, IGuardWeaponSource
    {
        [Header("References")]
        [SerializeField] private WeaponData weapon;
        [SerializeField] private MeleeHitbox hitbox;
        [Tooltip("Optional. Auto-found on this object if left empty. Attacks cost stamina when present.")]
        [SerializeField] private PlayerStamina stamina;

        [Header("Events (hook audio / VFX here)")]
        public UnityEvent onLightSwing;
        public UnityEvent onHeavySwing;
        public UnityEvent onGuardRaise;
        public UnityEvent onGuardDrop;
        [Tooltip("Our swing connected with a live target.")]
        public UnityEvent onHitLanded;
        [Tooltip("Our swing was blocked or clashed. Good for a recoil/clink.")]
        public UnityEvent onRecoil;
        [Tooltip("We got parried and are now stunned.")]
        public UnityEvent onStunned;

        // IClashable
        public bool IsSwinging => Time.time >= swingStartTime && Time.time < hitResolveTime;
        public AttackType CurrentAttack => pendingType;

        // IGuardWeaponSource: the weapon used for block/parry windows.
        public WeaponData GuardWeapon => weapon;

        private Health health;
        private InputAction attackAction, blockAction;

        private enum State { Ready, WindingUp, Recovering, Stunned }
        private State state = State.Ready;

        private AttackType pendingType;
        private float attackDownTime = -100f;
        private bool attackHeld;
        private float swingStartTime;
        private float hitResolveTime;   // when the spherecast fires
        private float stateEndTime;      // when recovery / stun ends
        private bool guardUp;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (stamina == null) stamina = GetComponent<PlayerStamina>();

            attackAction = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            blockAction = new InputAction("Block", InputActionType.Button, "<Mouse>/rightButton");
        }

        private void OnEnable()
        {
            attackAction.Enable();
            blockAction.Enable();
        }

        private void OnDisable()
        {
            attackAction.Disable();
            blockAction.Disable();
            DropGuard();
        }

        private void OnDestroy()
        {
            attackAction.Dispose();
            blockAction.Dispose();
        }

        private void Update()
        {
            if (!health.IsAlive)
            {
                if (guardUp) DropGuard();
                return;
            }

            HandleGuard();
            HandleAttackInput();
            TickState();
        }

        // ───────────── Guard ─────────────

        private void HandleGuard()
        {
            // Can't guard mid-swing or while stunned.
            bool canGuard = state == State.Ready || state == State.Recovering;

            if (blockAction.IsPressed() && canGuard && !guardUp) RaiseGuard();
            else if (!blockAction.IsPressed() && guardUp) DropGuard();
        }

        private void RaiseGuard()
        {
            guardUp = true;
            health.SetGuard(true, weapon);   // stamps the parry window start
            onGuardRaise?.Invoke();
        }

        private void DropGuard()
        {
            if (!guardUp) return;
            guardUp = false;
            health.SetGuard(false, weapon);
            onGuardDrop?.Invoke();
        }

        // ───────────── Attacks ─────────────

        private void HandleAttackInput()
        {
            if (weapon == null) return;

            if (attackAction.WasPressedThisFrame())
            {
                attackDownTime = Time.time;
                attackHeld = true;
            }

            if (!attackHeld) return;

            // Can only begin a swing from a settled state and not while guarding.
            bool canStart = state == State.Ready && !guardUp;

            // Commit to a heavy as soon as the hold passes the threshold.
            if (canStart && attackAction.IsPressed() &&
                Time.time - attackDownTime >= weapon.heavyHoldThreshold)
            {
                attackHeld = false;
                BeginSwing(AttackType.Heavy);
                return;
            }

            // A quick release before the threshold is a light attack.
            if (attackAction.WasReleasedThisFrame())
            {
                attackHeld = false;
                if (canStart && Time.time - attackDownTime < weapon.heavyHoldThreshold)
                    BeginSwing(AttackType.Light);
            }
        }

        private void BeginSwing(AttackType type)
        {
            // Stamina gate, if a pool is present.
            if (stamina != null && !stamina.TrySpend(weapon.StaminaCostFor(type)))
                return;

            DropGuard();
            pendingType = type;
            state = State.WindingUp;
            swingStartTime = Time.time;
            hitResolveTime = Time.time + weapon.WindupFor(type);

            if (type == AttackType.Heavy) onHeavySwing?.Invoke();
            else onLightSwing?.Invoke();
        }

        private void TickState()
        {
            switch (state)
            {
                case State.WindingUp:
                    if (Time.time >= hitResolveTime) ResolveSwing();
                    break;

                case State.Recovering:
                case State.Stunned:
                    if (Time.time >= stateEndTime) state = State.Ready;
                    break;
            }
        }

        private void ResolveSwing()
        {
            var outcome = hitbox != null
                ? hitbox.Swing(weapon, pendingType, gameObject)
                : MeleeHitbox.SwingOutcome.Miss;

            switch (outcome)
            {
                case MeleeHitbox.SwingOutcome.Hit:
                    onHitLanded?.Invoke();
                    break;
                case MeleeHitbox.SwingOutcome.Blocked:
                case MeleeHitbox.SwingOutcome.Clashed:
                    onRecoil?.Invoke();
                    break;
                case MeleeHitbox.SwingOutcome.Parried:
                    // Being parried is handled through OnParried via the stun call
                    // the defender triggers; recoil covers the feedback either way.
                    onRecoil?.Invoke();
                    break;
            }

            // If we weren't already stunned by a parry during ResolveSwing, recover.
            if (state == State.WindingUp)
            {
                state = State.Recovering;
                stateEndTime = Time.time + weapon.RecoveryFor(pendingType);
            }
        }

        // ───────────── IClashable ─────────────

        public void OnClash(AttackType incoming)
        {
            // Our swing got cancelled by a like-typed enemy swing. Short recoil.
            onRecoil?.Invoke();
            state = State.Recovering;
            stateEndTime = Time.time + weapon.RecoveryFor(pendingType) * 0.6f;
        }

        /// <summary>Called by the defender's parry so the attacker eats the stun.</summary>
        public void ApplyParryStun(float duration)
        {
            state = State.Stunned;
            stateEndTime = Time.time + duration;
            DropGuard();
            onStunned?.Invoke();
        }
    }
}
