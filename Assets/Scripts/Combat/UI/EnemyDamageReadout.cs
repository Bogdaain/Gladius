using System;
using System.Collections.Generic;
using Gladius.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Gladius.Combat.UI
{
    /// <summary>
    /// HUD readout that flashes an enemy's health when it gets hit. On each blow
    /// it shows the enemy's total HP, pops a "-N" for the damage that just landed,
    /// then animates the total label down to the new value and fades out.
    ///
    /// Purely cosmetic: it observes <see cref="Health"/>, reading Current/Max to
    /// work out how much changed. It never alters the damage math. One readout
    /// serves every enemy; a fresh hit (from any enemy) re-targets and re-shows it.
    /// </summary>
    public class EnemyDamageReadout : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Shows the enemy's current total HP.")]
        [SerializeField] private Text totalLabel;
        [Tooltip("Shows the '-N' damage pop. Optional.")]
        [SerializeField] private Text damageLabel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Timing")]
        [Tooltip("Stays fully visible this long after the last hit.")]
        [SerializeField] private float holdTime = 1.2f;
        [SerializeField] private float fadeSpeed = 2.5f;
        [Tooltip("How fast the total label counts toward the new value.")]
        [SerializeField] private float countSpeed = 60f;

        [Header("Damage pop")]
        [SerializeField] private float popRiseDistance = 40f;
        [SerializeField] private float popDuration = 0.7f;
        [SerializeField] private Color popColor = new Color(1f, 0.3f, 0.25f);
        [Tooltip("Scale the pop punches to on appearing, before settling to 1.")]
        [SerializeField] private float popPunchScale = 1.5f;

        [Header("Total label punch")]
        [Tooltip("Scale the total label punches to each time the enemy is hit.")]
        [SerializeField] private float totalPunchScale = 1.25f;
        [SerializeField] private float totalPunchRecovery = 8f;

        // Per-enemy memory of the last HP we saw, so we can compute the delta
        // even though several enemies may share this one readout.
        private readonly Dictionary<Health, float> lastSeen = new();
        private readonly Dictionary<Health, Action<float, float>> handlers = new();

        private Health active;
        private float displayedTotal;   // the animated number on the label
        private float targetTotal;       // where it's counting to
        private float lastHitTime = -100f;

        private Vector2 popHome;
        private float popStartTime = -100f;
        private float lastDamage;
        private float totalScale = 1f;

        private void Awake()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (damageLabel != null) popHome = damageLabel.rectTransform.anchoredPosition;
        }

        /// <summary>
        /// Register an enemy's Health so its hits drive this readout. Call from
        /// the enemy (e.g. a UnityEvent on Health.onDamaged wired to this) or once
        /// at spawn. Safe to call repeatedly.
        /// </summary>
        public void Track(Health enemy)
        {
            if (enemy == null || handlers.ContainsKey(enemy)) return;
            lastSeen[enemy] = enemy.Current;
            Action<float, float> handler = (cur, max) => OnEnemyChanged(enemy, cur, max);
            handlers[enemy] = handler;
            enemy.Changed += handler;
        }

        /// <summary>Stop tracking an enemy (call before it's destroyed).</summary>
        public void Untrack(Health enemy)
        {
            if (enemy == null || !handlers.TryGetValue(enemy, out var handler)) return;
            enemy.Changed -= handler;
            handlers.Remove(enemy);
            lastSeen.Remove(enemy);
            if (active == enemy) active = null;
        }

        private void OnDestroy()
        {
            foreach (var pair in handlers)
                if (pair.Key != null) pair.Key.Changed -= pair.Value;
            handlers.Clear();
        }

        private void OnEnemyChanged(Health enemy, float current, float max)
        {
            float previous = lastSeen.TryGetValue(enemy, out var p) ? p : max;
            lastSeen[enemy] = current;

            float delta = previous - current;
            if (delta <= 0f) return; // healing or no change: ignore

            active = enemy;
            lastDamage = delta;
            lastHitTime = Time.time;

            // Snap the label to where it was showing for THIS enemy, then count down.
            displayedTotal = previous;
            targetTotal = current;

            if (damageLabel != null)
            {
                damageLabel.text = "-" + Mathf.RoundToInt(delta);
                popStartTime = Time.time;
            }

            // Punch the total label so each hit reads as a jolt.
            totalScale = totalPunchScale;
        }

        private void Update()
        {
            // Count the total label toward the new value.
            displayedTotal = Mathf.MoveTowards(displayedTotal, targetTotal, countSpeed * Time.deltaTime);
            if (totalLabel != null)
            {
                totalLabel.text = Mathf.CeilToInt(displayedTotal).ToString();
                totalScale = Mathf.Lerp(totalScale, 1f, 1f - Mathf.Exp(-totalPunchRecovery * Time.deltaTime));
                totalLabel.rectTransform.localScale = Vector3.one * totalScale;
            }

            // Visibility: full right after a hit, fading once the hold expires.
            if (canvasGroup != null)
            {
                bool visible = Time.time - lastHitTime < holdTime;
                float goal = visible ? 1f : 0f;
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, goal, fadeSpeed * Time.deltaTime);
            }

            UpdatePop();
        }

        private void UpdatePop()
        {
            if (damageLabel == null) return;

            float t = (Time.time - popStartTime) / popDuration;
            if (t >= 1f)
            {
                damageLabel.enabled = false;
                return;
            }

            damageLabel.enabled = true;

            // Rise with an ease-out so it leaps up then eases to a stop.
            float ease = 1f - (1f - t) * (1f - t);
            damageLabel.rectTransform.anchoredPosition = popHome + Vector2.up * (popRiseDistance * ease);

            // Scale: punch up fast, settle to 1 by about a third of the way through.
            float punch = t < 0.3f
                ? Mathf.Lerp(popPunchScale, 1f, t / 0.3f)
                : 1f;
            damageLabel.rectTransform.localScale = Vector3.one * punch;

            // Hold full opacity, then fade over the back half.
            Color c = popColor;
            c.a = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            damageLabel.color = c;
        }
    }
}
