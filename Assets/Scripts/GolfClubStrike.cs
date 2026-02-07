using UnityEngine;

/// <summary>
/// VR golf striker tuned for "game feel" with single-root club that has:
/// - CapsuleCollider (shaft/grip)
/// - BoxCollider (head)
///
/// Attach this to the GOLFCLUB ROOT object.
/// Assign:
/// - Ball Rigidbody
/// - Head Box Collider (the BoxCollider used for hitting)
///
/// Works well with kinematic grabbed clubs.
/// </summary>
public class GolfClubStrike : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the golf ball Rigidbody here.")]
    [SerializeField] private Rigidbody ballRigidbody;

    [Tooltip("Assign the club head BoxCollider (the one used to hit the ball).")]
    [SerializeField] private BoxCollider headBoxCollider;

    [Tooltip("Optional override: if set, uses this instead of headBoxCollider center.")]
    [SerializeField] private Transform headPoint;

    [Header("Hit Tuning (Game Feel)")]
    [Min(0f)]
    [SerializeField] private float minClubSpeedToHit = 0.7f;

    [Tooltip("Primary power from speed toward the ball.")]
    [Min(0f)]
    [SerializeField] private float towardMultiplier = 3.8f;

    [Tooltip("Adds power from sideways speed to avoid tiny nudges in VR (0.3–0.9 recommended).")]
    [Range(0f, 1.5f)]
    [SerializeField] private float tangentialMultiplier = 0.55f;

    [Tooltip("Clamp added ball speed so it doesn't explode.")]
    [Min(0f)]
    [SerializeField] private float maxAddedBallSpeed = 22f;

    [Tooltip("Seconds between hits to prevent multi-hits while overlapping.")]
    [Min(0f)]
    [SerializeField] private float hitCooldownSeconds = 0.06f;

    [Header("Anti-Miss Sweep")]
    [SerializeField] private bool useSweep = true;

    [Tooltip("Try 0.04–0.06 for reliable VR swings.")]
    [Min(0.001f)]
    [SerializeField] private float sweepRadius = 0.05f;

    [Tooltip("Also checks overlap at the end position (catches 'already inside' cases).")]
    [SerializeField] private bool useOverlapAtEnd = true;

    [Header("Direction / Downward Protection")]
    [SerializeField] private bool preventDownwardHits = true;

    [Tooltip("Small upward lift for nicer feel (0.00–0.08).")]
    [Range(0f, 0.2f)]
    [SerializeField] private float loft = 0.04f;

    [Tooltip("Blend direction-to-ball (0) vs club velocity direction (1). 0.35–0.70 recommended.")]
    [Range(0f, 1f)]
    [SerializeField] private float dirBlendTowardVelocity = 0.55f;

    [Header("Optional Spin")]
    [Min(0f)]
    [SerializeField] private float spinImpulse = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool logHits = false;

    private Vector3 _prevHeadPos;
    private Vector3 _vel;
    private float _lastHitTime = -999f;

    private void Awake()
    {
        _prevHeadPos = GetHeadPos();
    }

    private void LateUpdate()
    {
        // LateUpdate tracks the final grabbed pose this frame (better VR feel)
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 headPos = GetHeadPos();
        _vel = (headPos - _prevHeadPos) / dt;

        if (useSweep)
            SweepTryHit(_prevHeadPos, headPos, dt, _vel);

        _prevHeadPos = headPos;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHitRigidbody(collision.rigidbody, GetHeadPos(), _vel);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryHitRigidbody(collision.rigidbody, GetHeadPos(), _vel);
    }

    private Vector3 GetHeadPos()
    {
        if (headPoint != null) return headPoint.position;

        if (headBoxCollider != null) return headBoxCollider.bounds.center;

        // Fallback: root pivot (least accurate)
        return transform.position;
    }

    private void SweepTryHit(Vector3 from, Vector3 to, float dt, Vector3 clubVel)
    {
        if (Time.time - _lastHitTime < hitCooldownSeconds) return;
        if (ballRigidbody == null) return;

        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist <= 0.0001f) return;

        float clubSpeed = clubVel.magnitude;
        if (clubSpeed < minClubSpeedToHit) return;

        Vector3 dir = delta / dist;

        RaycastHit[] hits = Physics.SphereCastAll(from, sweepRadius, dir, dist, ~0, QueryTriggerInteraction.Ignore);

        Rigidbody best = null;
        float bestD = float.PositiveInfinity;

        for (int i = 0; hits != null && i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            // ignore self colliders (club)
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.transform.IsChildOf(transform.root)) continue;

            Rigidbody rb = h.rigidbody != null ? h.rigidbody : h.collider.attachedRigidbody;
            if (rb == null) continue;
            if (rb != ballRigidbody) continue;

            if (h.distance < bestD)
            {
                bestD = h.distance;
                best = rb;
            }
        }

        if (best == null && useOverlapAtEnd)
        {
            Collider[] overlaps = Physics.OverlapSphere(to, sweepRadius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; overlaps != null && i < overlaps.Length; i++)
            {
                var c = overlaps[i];
                if (c == null) continue;

                if (c.transform.IsChildOf(transform)) continue;
                if (c.transform.IsChildOf(transform.root)) continue;

                Rigidbody rb = c.attachedRigidbody;
                if (rb == null) continue;
                if (rb != ballRigidbody) continue;

                best = rb;
                break;
            }
        }

        if (best == null) return;

        TryHitRigidbody(best, to, clubVel);
    }

    private void TryHitRigidbody(Rigidbody targetRb, Vector3 clubPos, Vector3 clubVel)
    {
        if (targetRb == null || ballRigidbody == null) return;
        if (targetRb != ballRigidbody) return;
        if (Time.time - _lastHitTime < hitCooldownSeconds) return;

        float clubSpeed = clubVel.magnitude;
        if (clubSpeed < minClubSpeedToHit) return;

        // Direction toward ball from club head
        Vector3 toBall = (targetRb.worldCenterOfMass - clubPos);
        Vector3 toBallDir = (toBall.sqrMagnitude > 0.000001f) ? toBall.normalized : clubVel.normalized;

        // Only "toward" component is physically correct, but VR needs some tangential for good feel
        float toward = Mathf.Max(0f, Vector3.Dot(clubVel, toBallDir));
        float tangential = Mathf.Sqrt(Mathf.Max(0f, clubSpeed * clubSpeed - toward * toward));

        float addedSpeed = toward * towardMultiplier + tangential * tangentialMultiplier;
        if (addedSpeed <= 0.0001f) return;
        addedSpeed = Mathf.Min(addedSpeed, maxAddedBallSpeed);

        // Blend between "to ball" and "velocity direction"
        Vector3 velDir = (clubVel.sqrMagnitude > 0.000001f) ? clubVel.normalized : toBallDir;
        Vector3 impulseDir = Vector3.Slerp(toBallDir, velDir, dirBlendTowardVelocity);

        // small loft
        impulseDir = impulseDir + Vector3.up * loft;

        if (preventDownwardHits && impulseDir.y < 0f)
            impulseDir.y = 0f;

        if (impulseDir.sqrMagnitude < 0.000001f)
            impulseDir = Vector3.forward;

        impulseDir.Normalize();

        // Apply as velocity add (stable VR feel)
        targetRb.linearVelocity += impulseDir * addedSpeed;
        targetRb.WakeUp();

        // Optional spin
        if (spinImpulse > 0f)
        {
            Vector3 spinAxis = Vector3.Cross(Vector3.up, impulseDir);
            targetRb.AddTorque(spinAxis * (targetRb.mass * addedSpeed * spinImpulse), ForceMode.Impulse);
        }

        _lastHitTime = Time.time;

        if (logHits)
            Debug.Log($"[GolfClubStrike] HIT addedSpeed={addedSpeed:0.00} clubSpeed={clubSpeed:0.00} toward={toward:0.00} tang={tangential:0.00}", this);
    }
}
