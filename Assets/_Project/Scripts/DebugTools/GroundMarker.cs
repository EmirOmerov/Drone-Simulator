using UnityEngine;

namespace DroneSim.DebugTools
{
    /// <summary>
    /// Height aid for beginners: a disc on the ground straight below the drone and a thin
    /// line connecting them, so altitude is easy to judge from the chase camera.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class GroundMarker : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Transform disc;
        [SerializeField] float maxDistance = 100f;
        [SerializeField] LayerMask groundMask = ~0;

        LineRenderer line;

        public void Configure(Transform drone, Transform markerDisc)
        {
            target = drone;
            disc = markerDisc;
        }

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            bool hit = Physics.Raycast(target.position, Vector3.down, out RaycastHit info, maxDistance,
                groundMask, QueryTriggerInteraction.Ignore);

            // Ignore hits on the drone's own colliders.
            if (hit && info.rigidbody != null && info.rigidbody.transform == target)
                hit = false;

            line.enabled = hit;
            if (disc != null)
                disc.gameObject.SetActive(hit);
            if (!hit)
                return;

            Vector3 ground = info.point + info.normal * 0.02f;
            if (disc != null)
            {
                disc.position = ground;
                disc.rotation = Quaternion.FromToRotation(Vector3.up, info.normal);
            }
            line.SetPosition(0, target.position);
            line.SetPosition(1, ground);
        }
    }
}
