using DroneSim.Controls;
using DroneSim.Game;
using UnityEngine;

namespace DroneSim.CameraRig
{
    public enum CameraMode
    {
        Chase,
        Fpv
    }

    /// <summary>
    /// Two views, toggled with Triangle / C:
    /// - Chase: behind and above, follows heading only so the horizon stays level. Pulls in when a wall
    ///   is between the camera and the drone (important inside the factory).
    /// - FPV: camera on the drone's nose, tilted up slightly like a real FPV camera, wide FOV.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class DroneCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] FlightInputProvider inputProvider;
        [SerializeField] CameraMode mode = CameraMode.Chase;

        [Header("Chase")]
        [SerializeField] float distance = 4.5f;
        [SerializeField] float height = 1.6f;
        [SerializeField] float lookHeight = 0.3f;
        [SerializeField] float positionDamping = 5f;
        [SerializeField] float rotationDamping = 10f;
        [SerializeField] float chaseFieldOfView = 70f;
        [SerializeField] float collisionRadius = 0.25f;
        [SerializeField] float minHeightAboveGround = 0.4f;
        [Tooltip("If the target jumps further than this (respawn), snap instead of smoothing.")]
        [SerializeField] float snapDistance = 15f;

        [Header("FPV")]
        [SerializeField] Vector3 fpvOffset = new Vector3(0f, 0.04f, 0.2f);
        [Tooltip("Camera tilted up, so the horizon stays in view while the drone leans forward.")]
        [SerializeField, Range(0f, 40f)] float fpvUptiltDegrees = 12f;
        [SerializeField] float fpvFieldOfView = 100f;

        Camera cam;
        Vector3 lastFlatForward = Vector3.forward;
        bool snapNextFrame = true;

        public CameraMode Mode => mode;

        public void Configure(Transform drone, FlightInputProvider provider)
        {
            target = drone;
            inputProvider = provider;
            snapNextFrame = true;
        }

        public void SetMode(CameraMode newMode)
        {
            mode = newMode;
            snapNextFrame = true;
        }

        void Awake() => cam = GetComponent<Camera>();

        void Update()
        {
            if (!GameState.IsPaused && inputProvider != null && inputProvider.CameraToggleRequested)
                SetMode(mode == CameraMode.Chase ? CameraMode.Fpv : CameraMode.Chase);
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            if (mode == CameraMode.Fpv)
                UpdateFpv();
            else
                UpdateChase();

            snapNextFrame = false;
        }

        void UpdateFpv()
        {
            cam.fieldOfView = fpvFieldOfView;
            cam.nearClipPlane = 0.02f;
            transform.SetPositionAndRotation(
                target.TransformPoint(fpvOffset),
                target.rotation * Quaternion.Euler(-fpvUptiltDegrees, 0f, 0f));
        }

        void UpdateChase()
        {
            cam.fieldOfView = chaseFieldOfView;
            cam.nearClipPlane = 0.05f;

            Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 1e-4f)
                lastFlatForward = flatForward.normalized;

            Vector3 lookPoint = target.position + Vector3.up * lookHeight;
            Vector3 desiredPosition = target.position - lastFlatForward * distance + Vector3.up * height;
            desiredPosition.y = Mathf.Max(desiredPosition.y, minHeightAboveGround);
            desiredPosition = AvoidWalls(lookPoint, desiredPosition);

            bool snap = snapNextFrame ||
                        (desiredPosition - transform.position).sqrMagnitude > snapDistance * snapDistance;
            float deltaTime = Time.deltaTime;

            Vector3 position = snap
                ? desiredPosition
                : Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionDamping * deltaTime));
            // Never let smoothing drag the camera back behind a wall.
            position = AvoidWalls(lookPoint, position);
            transform.position = position;

            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - position, Vector3.up);
            transform.rotation = snap
                ? desiredRotation
                : Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationDamping * deltaTime));
        }

        /// <summary>Moves the camera in front of any geometry between it and the drone.</summary>
        Vector3 AvoidWalls(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float length = offset.magnitude;
            if (length < 1e-3f)
                return to;

            // The drone sits on the Ignore Raycast layer, so DefaultRaycastLayers skips it.
            if (Physics.SphereCast(from, collisionRadius, offset / length, out RaycastHit hit, length,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return from + offset / length * Mathf.Max(0.3f, hit.distance);

            return to;
        }
    }
}
