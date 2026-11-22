using Gladius.Combat;
using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Drop this on anything that should leave blood splatter when hit. It
    /// registers this object's Health with the scene's single BloodSplatter at
    /// startup, keeping the decal wiring off the combat scripts. Finds the
    /// spawner automatically if one isn't assigned.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BloodSplatterReporter : MonoBehaviour
    {
        [SerializeField] private BloodSplatter splatter;

        private void Start()
        {
            if (splatter == null) splatter = FindAnyObjectByType<BloodSplatter>();
            if (splatter != null) splatter.Track(GetComponent<Health>());
        }
    }
}
