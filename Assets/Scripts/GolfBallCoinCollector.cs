using UnityEngine;

public class GolfBallCoinCollector : MonoBehaviour
{
    [Header("References")]
    public ParkourCounter parkourCounter;

    [Header("Audio")]
    public AudioClip collectClip;
    public AudioSource audioSource;

    [Header("Collection Settings")]
    public float pickupRadius = 0.60f;
    public float scanCenterYOffset = 0.20f;

    [Min(1)]
    public int scanEveryNFixedSteps = 1;

    public bool requireParkourStarted = true;

    [Header("Collect Action")]
    public bool disableCoinGameObjectOnCollect = true;

    private int _fixedStepCounter = 0;
    private readonly Collider[] _hits = new Collider[128];

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void FixedUpdate()
    {
        if (parkourCounter == null) return;
        if (requireParkourStarted && !parkourCounter.parkourStart) return;

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

            parkourCounter.coinCount++;

            if (collectClip != null)
                audioSource.PlayOneShot(collectClip);

            if (disableCoinGameObjectOnCollect)
                c.gameObject.SetActive(false);

            _hits[i] = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position + Vector3.up * scanCenterYOffset,
            pickupRadius
        );
    }
#endif
}
