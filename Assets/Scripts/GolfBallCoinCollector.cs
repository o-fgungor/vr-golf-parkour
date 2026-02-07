using UnityEngine;

public class GolfBallCoinCollector : MonoBehaviour
{
    [Header("References (Assign in Inspector)")]
    public ParkourCounter parkourCounter;

    [Tooltip("Assign the LocomotionTechnique from the player rig (for ball mode check).")]
    public LocomotionTechnique locomotionTech;

    [Tooltip("Optional collect sound. If empty, will try to use AudioSource on this ball.")]
    public AudioSource collectAudio;

    [Header("Collection Settings")]
    [Tooltip("Meters. Increase if coins are floating/high. Recommended: 0.45 - 0.70")]
    public float pickupRadius = 0.60f;

    [Tooltip("Vertical offset for scan center (meters). If coins are above the ball, set to 0.15 - 0.30")]
    public float scanCenterYOffset = 0.20f;

    [Tooltip("Scan every N fixed steps to reduce cost. 1 = every FixedUpdate, 2 = every 2 FixedUpdates.")]
    [Min(1)]
    public int scanEveryNFixedSteps = 1;

    [Tooltip("If true, coins only count after parkourStart == true.")]
    public bool requireParkourStarted = true;

    [Header("Mode Gate")]
    [Tooltip("Only collect coins when the ball is in Score mode (yellow).")]
    public bool onlyCollectInScoreMode = true;

    [Header("Collect Action")]
    public bool disableCoinGameObjectOnCollect = true;

    private int _fixedStepCounter = 0;
    private readonly Collider[] _hits = new Collider[128];

    private void Awake()
    {
        if (collectAudio == null)
            collectAudio = GetComponent<AudioSource>();
    }

    private void FixedUpdate()
    {
        if (parkourCounter == null) return;

        if (requireParkourStarted && !parkourCounter.parkourStart)
            return;

        // ✅ Mode-based gating
        if (onlyCollectInScoreMode && locomotionTech != null)
        {
            if (locomotionTech.CurrentBallMode != LocomotionTechnique.BallMode.Score)
                return;
        }

        _fixedStepCounter++;
        if (_fixedStepCounter < scanEveryNFixedSteps) return;
        _fixedStepCounter = 0;

        Vector3 center = transform.position + Vector3.up * scanCenterYOffset;

        int count = Physics.OverlapSphereNonAlloc(
            center,
            pickupRadius,
            _hits,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < count; i++)
        {
            Collider c = _hits[i];
            if (c == null) continue;

            if (!c.CompareTag("coin")) continue;

            parkourCounter.coinCount += 1;

            if (collectAudio != null)
                collectAudio.Play();

            if (disableCoinGameObjectOnCollect)
                c.gameObject.SetActive(false);

            _hits[i] = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * scanCenterYOffset, pickupRadius);
    }
#endif
}
