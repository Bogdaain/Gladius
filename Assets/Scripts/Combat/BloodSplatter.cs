using System.Collections.Generic;
using Gladius.Combat;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gladius.Combat
{
    /// <summary>
    /// Persistent Isaac-style blood splatter. On a clean hit it sprays a few
    /// URP decal projectors onto nearby surfaces: down onto the floor and out
    /// along the blow for walls, so gore sticks to whatever's closest. Splatters
    /// linger, then fade; a hard cap recycles the oldest so a long fight can't
    /// spawn thousands and tank the frame rate.
    ///
    /// One spawner serves every character: each Health registers through
    /// <see cref="Track"/> (via a small reporter, like the damage readout), so
    /// this lives once in the scene, not on every enemy.
    ///
    /// Needs the URP Decal Renderer Feature enabled (it is, on PC_Renderer).
    /// Assign a decal Material with a blood texture; projectors are built in code.
    /// </summary>
    public class BloodSplatter : MonoBehaviour
    {
        [Header("Decal look")]
        [Tooltip("Material for the decal projector (URP > Decal shader, blood texture).")]
        [SerializeField] private Material decalMaterial;
        [SerializeField] private Vector2 sizeRange = new Vector2(0.4f, 1.1f);
        [Tooltip("How deep the projector box reaches into the surface.")]
        [SerializeField] private float projectionDepth = 0.4f;

        [Header("Spray")]
        [Tooltip("Decals attempted per hit (each finds its own surface).")]
        [SerializeField] private int splatsPerHit = 3;
        [Tooltip("How far a splat looks for a surface to stick to.")]
        [SerializeField] private float reach = 4f;
        [Tooltip("Random spread (degrees) of the spray directions around the blow.")]
        [SerializeField] private float spread = 55f;
        [Tooltip("Surfaces blood can stick to. Set this to floors/walls only (exclude " +
                 "Enemy and Player) so splatter lands on the environment, not on bodies.")]
        [SerializeField] private LayerMask surfaceMask = ~0;
        [Tooltip("Push the ray start out from the hit point so it clears the body it " +
                 "came from (roughly the character's radius).")]
        [SerializeField] private float bodyClearance = 0.6f;

        [Header("Death burst (the kill splatters much harder)")]
        [Tooltip("Decals sprayed when a hit is the killing blow. 0 = no extra death burst.")]
        [SerializeField] private int splatsOnDeath = 12;
        [Tooltip("Wider spread on death so gore goes everywhere, not just along the blow.")]
        [SerializeField] private float deathSpread = 120f;
        [Tooltip("Death decals are scaled up by this much.")]
        [SerializeField] private float deathSizeScale = 1.6f;
        [Tooltip("Optional gib/gore particle burst spawned at the kill. Authored prefab.")]
        [SerializeField] private ParticleSystem deathGibPrefab;
        [SerializeField] private float gibLifetime = 5f;

        [Header("Lifetime")]
        [Tooltip("Max live splatters. Oldest is recycled past this. Guards performance.")]
        [SerializeField] private int maxSplats = 120;
        [Tooltip("Seconds a splat stays at full opacity before fading. 0 = never fade.")]
        [SerializeField] private float lifetime = 0f;
        [SerializeField] private float fadeDuration = 3f;

        private static BloodSplatter instance;

        // Live decals in spawn order, so the oldest is at the front for recycling.
        private readonly Queue<DecalProjector> live = new();
        private readonly List<(DecalProjector proj, float bornAt, float baseAlpha)> fading = new();

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Static entry so feedback/reporters can spawn without a reference.</summary>
        public static void Spawn(Vector3 point, Vector3 direction)
        {
            if (instance != null) instance.SpawnSplatter(point, direction, instance.splatsPerHit, instance.spread, 1f);
        }

        /// <summary>Register a Health so its hits produce splatter, and its death a big burst.</summary>
        public void Track(Health h)
        {
            if (h == null) return;
            h.Hit += (hit, result) =>
            {
                if (result != HitResult.Hit) return;

                if (h.IsAlive)
                {
                    // Normal clean hit.
                    SpawnSplatter(hit.Point, hit.Direction, splatsPerHit, spread, 1f);
                }
                else
                {
                    // That blow was the kill: spray far more, wider, bigger, plus gibs.
                    SpawnSplatter(hit.Point, hit.Direction, splatsOnDeath, deathSpread, deathSizeScale);
                    SpawnGibs(hit.Point, hit.Direction);
                }
            };
        }

        private void SpawnSplatter(Vector3 point, Vector3 direction, int count, float spreadDeg, float sizeScale)
        {
            if (decalMaterial == null) return;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.down;
            direction.Normalize();

            for (int i = 0; i < count; i++)
            {
                // Bias the spray: mostly along the blow and downward (gravity), with scatter.
                Vector3 baseDir = (direction + Vector3.down * 0.6f).normalized;
                Vector3 rayDir = Random.rotation * Vector3.forward;
                rayDir = Vector3.Slerp(baseDir, rayDir, spreadDeg / 180f).normalized;

                // Start a little out from the body so the ray doesn't immediately
                // hit the character's own collider and stick blood to the mesh.
                Vector3 origin = point + rayDir * bodyClearance;

                // RaycastAll + pick the first surface that isn't a character (has no
                // Health), so blood lands on floors/walls even if the mask is wide.
                var hits = Physics.RaycastAll(origin, rayDir, reach, surfaceMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    // Skip living/dead characters (anything with a Health in its hierarchy).
                    if (h.collider.GetComponentInParent<Health>() != null) continue;
                    PlaceDecal(h.point, h.normal, sizeScale);
                    break;
                }
            }
        }

        private void SpawnGibs(Vector3 point, Vector3 direction)
        {
            if (deathGibPrefab == null) return;
            Quaternion rot = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized)
                : Quaternion.identity;
            var ps = Instantiate(deathGibPrefab, point, rot);
            ps.Play();
            Destroy(ps.gameObject, gibLifetime);
        }

        private void PlaceDecal(Vector3 pos, Vector3 normal, float sizeScale)
        {
            DecalProjector proj = Recycle();

            var go = proj.gameObject;
            go.transform.position = pos + normal * 0.02f;        // lift slightly off the surface
            // Projector looks along -Z; face it into the surface, with a random spin.
            go.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            float s = Random.Range(sizeRange.x, sizeRange.y) * sizeScale;
            proj.size = new Vector3(s, s, projectionDepth);
            proj.pivot = new Vector3(0, 0, projectionDepth * 0.5f);
            proj.material = decalMaterial;
            proj.fadeFactor = 1f;
            go.SetActive(true);

            live.Enqueue(proj);
            if (lifetime > 0f) fading.Add((proj, Time.time, 1f));
        }

        private Transform decalRoot;

        // A standalone container at the origin that holds all decals. Decoupled
        // from this component so splatter never inherits a moving transform.
        private Transform DecalRoot()
        {
            if (decalRoot == null)
            {
                var go = new GameObject("BloodDecals");
                decalRoot = go.transform;
            }
            return decalRoot;
        }

        // Reuse the oldest projector once we hit the cap; otherwise make a new one.
        private DecalProjector Recycle()
        {
            if (live.Count >= maxSplats && live.Count > 0)
            {
                var old = live.Dequeue();
                fading.RemoveAll(f => f.proj == old);
                return old;
            }

            // Parent to a dedicated root at the scene origin, never to this
            // component's transform. If BloodSplatter sits on a moving object,
            // parenting decals to it would drag every splatter around with it.
            var go = new GameObject("BloodDecal");
            go.transform.SetParent(DecalRoot(), true);
            var proj = go.AddComponent<DecalProjector>();
            return proj;
        }

        private void Update()
        {
            if (lifetime <= 0f || fading.Count == 0) return;

            for (int i = fading.Count - 1; i >= 0; i--)
            {
                var f = fading[i];
                if (f.proj == null) { fading.RemoveAt(i); continue; }

                float age = Time.time - f.bornAt;
                if (age < lifetime) continue;

                float t = (age - lifetime) / fadeDuration;
                if (t >= 1f)
                {
                    f.proj.gameObject.SetActive(false);
                    fading.RemoveAt(i);
                }
                else
                {
                    f.proj.fadeFactor = f.baseAlpha * (1f - t);
                }
            }
        }
    }
}
