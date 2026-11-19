using Gladius.Combat;
using UnityEngine;

namespace Gladius.Combat.UI
{
    /// <summary>
    /// Drop this on an enemy to make its hits show up on the shared
    /// <see cref="EnemyDamageReadout"/>. It just registers this object's Health
    /// with the readout at startup, keeping the UI wiring off the combat scripts.
    /// Finds the readout automatically if one isn't assigned.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyReadoutReporter : MonoBehaviour
    {
        [SerializeField] private EnemyDamageReadout readout;

        private void Start()
        {
            if (readout == null) readout = FindAnyObjectByType<EnemyDamageReadout>();
            if (readout == null) return; // no readout in the scene: silently do nothing
            readout.Track(GetComponent<Health>());
        }

        private void OnDestroy()
        {
            if (readout != null) readout.Untrack(GetComponent<Health>());
        }
    }
}
