using System.Collections;
using System.Collections.Generic;
using Gladius.Combat;
using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Runs the death sequence: stop acting, ragdoll (or topple), linger, dissolve,
    /// despawn. Driven off the killing blow in Health.Hit so the body flops away
    /// from the hit.
    ///
    /// Ragdoll: if the character has a rig with Rigidbodies on its bones (a real
    /// ragdoll setup), this switches them live and shoves them along the blow.
    /// The placeholder capsule has no rig, so it falls back to dropping a single
    /// Rigidbody and toppling. Either way the body reacts, and the real model drops
    /// in later with no code change beyond having its ragdoll bodies present.
    ///
    /// Dissolve: drives a material float (e.g. "_DissolveAmount") if the shader has
    /// one; otherwise sinks the body into the floor while shrinking, so it fades
    /// out even without a dissolve shader.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DeathHandler : MonoBehaviour
    {
        [Header("Shut these off on death")]
        [Tooltip("Scripts to disable when the body dies (AI, combat, movers). " +
                 "The CharacterController and this object's colliders are handled automatically.")]
        [SerializeField] private MonoBehaviour[] disableOnDeath;

        [Header("Ragdoll")]
        [Tooltip("Rigidbodies of the ragdoll rig. Leave empty for the capsule fallback.")]
        [SerializeField] private Rigidbody[] ragdollBodies;
        [Tooltip("Impulse strength applied along the killing blow.")]
        [SerializeField] private float deathForce = 6f;
        [Tooltip("Extra upward kick so the body lifts a little as it's knocked.")]
        [SerializeField] private float deathUpForce = 2f;

        [Header("Timing")]
        [Tooltip("How long the body lies there before it starts to vanish.")]
        [SerializeField] private float lingerTime = 4f;
        [Tooltip("How long the dissolve/sink takes.")]
        [SerializeField] private float dissolveTime = 2f;

        [Header("Dissolve")]
        [Tooltip("Material float driven 0→1 to dissolve, if the shader exposes it.")]
        [SerializeField] private string dissolveProperty = "_DissolveAmount";

        private Health health;
        private bool dying;
        private Vector3 lastBlowDir = Vector3.forward;

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable() => health.Hit += OnHit;
        private void OnDisable() => health.Hit -= OnHit;

        private void OnHit(HitInfo hit, HitResult result)
        {
            if (result == HitResult.Hit && hit.Direction.sqrMagnitude > 0.0001f)
                lastBlowDir = hit.Direction.normalized;

            if (!health.IsAlive && !dying)
                StartCoroutine(Die());
        }

        private IEnumerator Die()
        {
            dying = true;

            // 1) Stop acting: disable listed scripts and the CharacterController.
            foreach (var b in disableOnDeath)
                if (b != null) b.enabled = false;

            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            // 2) Ragdoll, or topple the capsule as a fallback.
            if (ragdollBodies != null && ragdollBodies.Length > 0)
                EnableRagdoll();
            else
                ToppleFallback();

            // 3) Lie there, then dissolve away.
            yield return new WaitForSeconds(lingerTime);
            yield return Dissolve();

            // 4) Gone.
            Destroy(gameObject);
        }

        private void EnableRagdoll()
        {
            Vector3 impulse = lastBlowDir * deathForce + Vector3.up * deathUpForce;
            foreach (var rb in ragdollBodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.AddForce(impulse, ForceMode.VelocityChange);
            }
        }

        // No rig: add a Rigidbody to the whole object and shove it so it tumbles.
        private void ToppleFallback()
        {
            var rb = gameObject.GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;

            // A capsule collider so it has something to rest on once the
            // CharacterController is off. Reuse one if present.
            if (GetComponent<Collider>() == null)
            {
                var cap = gameObject.AddComponent<CapsuleCollider>();
                cap.height = 2f;
                cap.radius = 0.4f;
            }

            Vector3 impulse = lastBlowDir * deathForce + Vector3.up * deathUpForce;
            rb.AddForce(impulse, ForceMode.VelocityChange);
            // Off-centre torque so it tips over instead of sliding flat.
            rb.AddTorque(Vector3.Cross(Vector3.up, lastBlowDir) * deathForce, ForceMode.VelocityChange);
        }

        private IEnumerator Dissolve()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var mats = new List<Material>();
            bool hasDissolve = false;

            foreach (var r in renderers)
                foreach (var m in r.materials)
                {
                    mats.Add(m);
                    if (m.HasProperty(dissolveProperty)) hasDissolve = true;
                }

            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;

            float t = 0f;
            while (t < dissolveTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dissolveTime);

                if (hasDissolve)
                {
                    // Shader-driven dissolve.
                    foreach (var m in mats)
                        if (m.HasProperty(dissolveProperty)) m.SetFloat(dissolveProperty, k);
                }
                else
                {
                    // Fallback: sink into the floor while shrinking slightly.
                    transform.position = startPos + Vector3.down * (k * 1.2f);
                    transform.localScale = startScale * (1f - k * 0.3f);
                }

                yield return null;
            }
        }
    }
}
