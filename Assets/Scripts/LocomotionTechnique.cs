using UnityEngine;

public class LocomotionTechnique : MonoBehaviour
{
    // Please implement your locomotion technique in this script.
    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    [Range(0, 10)] public float translationGain = 0.5f; // legacy (not used for golf), keep for compatibility
    public GameObject hmd;

    [SerializeField] private float leftTriggerValue;
    [SerializeField] private float rightTriggerValue;
    [SerializeField] private Vector3 startPos;
    [SerializeField] private Vector3 offset;
    [SerializeField] private bool isIndexTriggerDown;

    public enum BallMode { Teleport, Score }

    [Header("BALL MODE (Toggle with Right B)")]
    [SerializeField] private BallMode currentBallMode = BallMode.Teleport;

    [Tooltip("Renderer of the golf ball to change its color (optional). If empty, auto-find from golfBall.")]
    [SerializeField] private Renderer golfBallRenderer;

    [SerializeField] private Color teleportModeColor = Color.white;
    [SerializeField] private Color scoreModeColor = new Color(1f, 0.8f, 0.1f, 1f);
    private MaterialPropertyBlock _mpb;

    [Header("GOLF LOCOMOTION")]
    [Tooltip("Rigidbody of the golf ball. Teleport happens when it stops (Teleport mode only).")]
    [SerializeField] private Rigidbody golfBall;

    [Tooltip("If true, teleport triggers only after the ball moved at least once (hit).")]
    [SerializeField] private bool requireBallMovementBeforeTeleport = true;

    [Header("STOP DETECTION (Teleport Trigger)")]
    [SerializeField] private float stopSpeedThreshold = 0.30f;
    [SerializeField] private float stopAngularThreshold = 1.50f;
    [SerializeField] private float stopHoldTime = 0.15f;

    [Header("TELEPORT OFFSETS")]
    [Tooltip("Teleport to the LEFT of the ball (ball ends up on your RIGHT) by this amount.")]
    [SerializeField] private float teleportLeftOffsetMeters = 0.50f;

    [Tooltip("Additionally, teleport slightly behind the ball.")]
    [SerializeField] private float teleportBackOffsetMeters = 0.10f;

    [Tooltip("Keep current rig/root Y position (recommended).")]
    [SerializeField] private bool keepRigY = true;

    [Header("BALL PLACEMENT / DROP")]
    [Tooltip("Place the ball at start so it is not under your feet.")]
    [SerializeField] private bool positionBallOnStart = true;

    [Tooltip("Ball start offset to your RIGHT.")]
    [SerializeField] private float startRightOffsetMeters = 0.35f;

    [Tooltip("Ball start offset in FRONT of you.")]
    [SerializeField] private float startForwardOffsetMeters = 0.80f;

    [Tooltip("When placing/resetting ball, drop it from this height above ground (meters).")]
    [SerializeField] private float ballDropHeight = 0.20f;

    [Tooltip("After placing/resetting ball, ignore teleport logic for this many seconds so it can fall and settle.")]
    [SerializeField] private float ignoreTeleportAfterReset = 0.60f;

    [Header("RECALL / RESET (X/Y)")]
    [Tooltip("Root transform of the golf club prefab in the scene.")]
    [SerializeField] private Transform golfClubRoot;

    [Tooltip("Optional rigidbody of the golf club root (recommended).")]
    [SerializeField] private Rigidbody golfClubRb;

    [Tooltip("Where to place club relative to your head (forward).")]
    [SerializeField] private float clubForwardMeters = 0.80f;

    [Tooltip("Where to place club relative to your head (right).")]
    [SerializeField] private float clubRightMeters = 0.20f;

    [Tooltip("Extra up offset for club spawn.")]
    [SerializeField] private float clubUpMeters = 0.00f;

    [Tooltip("Ball reset offset to your RIGHT.")]
    [SerializeField] private float ballRestartRightMeters = 0.35f;

    [Tooltip("Ball reset offset in FRONT of you.")]
    [SerializeField] private float ballRestartForwardMeters = 0.80f;

    [Header("BANNER TRIGGER FIX (Teleport may skip triggers)")]
    [SerializeField] private float bannerOverlapRadius = 0.35f;
    [SerializeField] private float bannerSweepRadius = 0.25f;

    // stop/teleport state
    private float stillTimer;
    private bool ballHasMoved;

    // teleport arming: prevents teleport after reset/drop until real movement happens
    private bool _teleportArmed = false;

    // ignore teleport for a short window after reset/spawn
    private float _ignoreTeleportUntil = 0f;

    /////////////////////////////////////////////////////////
    // These are for the game mechanism.
    public ParkourCounter parkourCounter;
    public string stage;
    public SelectionTaskMeasure selectionTaskMeasure;
    /////////////////////////////////////////////////////////

    void Start()
    {
        stillTimer = 0f;
        ballHasMoved = false;

        _mpb = new MaterialPropertyBlock();

        if (golfBallRenderer == null && golfBall != null)
            golfBallRenderer = golfBall.GetComponentInChildren<Renderer>();

        ApplyBallModeVisual(currentBallMode);

        if (positionBallOnStart && golfBall != null && hmd != null)
            PlaceBallAtStart();
    }

    void Update()
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Golf locomotion:
        // - Ball slowing/physics is handled by GolfBallRollingResistance on the ball.
        // - Here we only detect "ball stopped" and teleport (Teleport mode only).

        // Right controller B -> Toggle mode (Teleport <-> Score)
        // Use RawButton.B only (avoid using Button.Two here)
        if (OVRInput.GetDown(OVRInput.RawButton.B))
        {
            ToggleBallMode();
        }

        // Left controller X -> Restart ball near player
        if (OVRInput.GetDown(OVRInput.RawButton.X))
        {
            RestartBallNearPlayer();
        }

        // Left controller Y -> Recall club in front
        if (OVRInput.GetDown(OVRInput.RawButton.Y))
        {
            RecallGolfClubInFront();
        }

        // Teleport decision
        if (golfBall != null && hmd != null)
        {
            bool ignoreTeleport = Time.time < _ignoreTeleportUntil;
            bool grounded = IsBallGrounded();

            float linearSpeed = golfBall.linearVelocity.magnitude;
            float angularSpeed = golfBall.angularVelocity.magnitude;

            // movement detection: if ball is moving, arm teleport (after reset window)
            if (linearSpeed > stopSpeedThreshold || angularSpeed > stopAngularThreshold)
            {
                ballHasMoved = true;
                stillTimer = 0f;

                if (!ignoreTeleport)
                    _teleportArmed = true;
            }

            // Score mode: never teleport
            if (currentBallMode == BallMode.Score)
            {
                stillTimer = 0f;
            }
            else // Teleport mode
            {
                if (!ignoreTeleport && grounded)
                {
                    if ((!requireBallMovementBeforeTeleport || ballHasMoved) && _teleportArmed)
                    {
                        bool isStill = (linearSpeed <= stopSpeedThreshold && angularSpeed <= stopAngularThreshold);

                        if (isStill)
                        {
                            stillTimer += Time.deltaTime;
                            if (stillTimer >= stopHoldTime)
                            {
                                TeleportToBallLeftSide();
                                stillTimer = 0f;

                                // After teleport, require new movement again
                                _teleportArmed = false;

                                if (requireBallMovementBeforeTeleport)
                                    ballHasMoved = false;
                            }
                        }
                        else
                        {
                            stillTimer = 0f;
                        }
                    }
                    else
                    {
                        stillTimer = 0f;
                    }
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        // These are for the game mechanism.
        if (OVRInput.Get(OVRInput.Button.Two) || OVRInput.Get(OVRInput.Button.Four))
        {
            if (parkourCounter.parkourStart)
            {
                transform.position = parkourCounter.currentRespawnPos;
            }
        }
    }

    private void ToggleBallMode()
    {
        currentBallMode = (currentBallMode == BallMode.Teleport) ? BallMode.Score : BallMode.Teleport;
        ApplyBallModeVisual(currentBallMode);

        // avoid accidental teleport on mode flip
        stillTimer = 0f;
        ballHasMoved = false;
        _teleportArmed = false;
        _ignoreTeleportUntil = Time.time + 0.15f;

        Debug.Log($"[LocomotionTechnique] Ball mode toggled -> {currentBallMode}");
    }

    private void ApplyBallModeVisual(BallMode mode)
    {
        if (golfBallRenderer == null)
        {
            Debug.LogWarning("[LocomotionTechnique] golfBallRenderer is NULL (color won't change). Assign in Inspector for easy testing.");
            return;
        }

        Color col = (mode == BallMode.Teleport) ? teleportModeColor : scoreModeColor;

        golfBallRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", col);
        _mpb.SetColor("_BaseColor", col); // URP/Lit
        golfBallRenderer.SetPropertyBlock(_mpb);
    }

    private void TeleportToBallLeftSide()
    {
        if (golfBall == null || hmd == null) return;

        // Teleport "before" head pos for banner sweep
        Vector3 headBefore = hmd.transform.position;

        Vector3 ballPos = golfBall.position;

        Vector3 right = hmd.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();

        Vector3 forward = hmd.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        // We want HEAD to end up here (ball on your right)
        Vector3 desiredHeadWorld = ballPos - right * teleportLeftOffsetMeters - forward * teleportBackOffsetMeters;

        // Move rig so that head reaches desiredHeadWorld
        Vector3 headPos = hmd.transform.position;
        Vector3 rigPos = transform.position;

        Vector3 headXZ = new Vector3(headPos.x, 0f, headPos.z);
        Vector3 rigXZ = new Vector3(rigPos.x, 0f, rigPos.z);
        Vector3 rigFromHeadXZ = rigXZ - headXZ;

        Vector3 newRigPos = new Vector3(desiredHeadWorld.x, 0f, desiredHeadWorld.z) + rigFromHeadXZ;
        newRigPos.y = keepRigY ? transform.position.y : rigPos.y;

        // TELEPORT
        transform.position = newRigPos;

        // Banner fix: treat teleport as if we physically crossed any banner trigger
        ForceBannerTriggerBetween(headBefore, desiredHeadWorld);
    }

    private void ForceBannerTriggerBetween(Vector3 fromHead, Vector3 toHeadExpected)
    {
        if (parkourCounter == null) return;

        // 1) If we land inside a banner trigger volume, trigger it
        if (TryTriggerBannerAtPosition(toHeadExpected)) return;

        // 2) Sweep between the points to detect skipped banners
        Vector3 delta = toHeadExpected - fromHead;
        float dist = delta.magnitude;
        if (dist < 0.001f) return;

        Vector3 dir = delta / dist;

        RaycastHit[] hits = Physics.SphereCastAll(
            fromHead,
            bannerSweepRadius,
            dir,
            dist,
            ~0,
            QueryTriggerInteraction.Collide // include triggers
        );

        if (hits == null || hits.Length == 0) return;

        float best = float.PositiveInfinity;
        Collider bestCol = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null) continue;
            if (!c.CompareTag("banner")) continue;

            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                bestCol = c;
            }
        }

        if (bestCol != null)
            TriggerBanner(bestCol.gameObject);
    }

    private bool TryTriggerBannerAtPosition(Vector3 headPos)
    {
        Collider[] cols = Physics.OverlapSphere(
            headPos,
            bannerOverlapRadius,
            ~0,
            QueryTriggerInteraction.Collide
        );

        if (cols == null || cols.Length == 0) return false;

        float best = float.PositiveInfinity;
        Collider bestCol = null;

        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null) continue;
            if (!c.CompareTag("banner")) continue;

            float d = (c.bounds.center - headPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestCol = c;
            }
        }

        if (bestCol == null) return false;

        TriggerBanner(bestCol.gameObject);
        return true;
    }

    private void TriggerBanner(GameObject bannerGO)
    {
        // Same effect as OnTriggerEnter("banner")
        stage = bannerGO.name;
        parkourCounter.isStageChange = true;
    }

    private void PlaceBallAtStart()
    {
        Vector3 headPos = hmd.transform.position;

        Vector3 right = hmd.transform.right; right.y = 0f; right.Normalize();
        Vector3 forward = hmd.transform.forward; forward.y = 0f; forward.Normalize();

        Vector3 targetXZ = headPos + right * startRightOffsetMeters + forward * startForwardOffsetMeters;
        Vector3 finalPos = FitBallToGroundAndDrop(targetXZ);

        golfBall.position = finalPos;
        golfBall.linearVelocity = Vector3.zero;
        golfBall.angularVelocity = Vector3.zero;
        golfBall.isKinematic = false;
        golfBall.useGravity = true;
        golfBall.constraints = RigidbodyConstraints.None;
        golfBall.WakeUp();

        _ignoreTeleportUntil = Time.time + ignoreTeleportAfterReset;
        ballHasMoved = false;
        stillTimer = 0f;
        _teleportArmed = false;
    }

    private void RestartBallNearPlayer()
    {
        if (golfBall == null || hmd == null)
        {
            Debug.LogWarning($"[LocomotionTechnique] RestartBallNearPlayer blocked. golfBall={(golfBall ? "OK" : "NULL")} hmd={(hmd ? "OK" : "NULL")}");
            return;
        }

        Vector3 headPos = hmd.transform.position;

        Vector3 right = hmd.transform.right; right.y = 0f; right.Normalize();
        Vector3 forward = hmd.transform.forward; forward.y = 0f; forward.Normalize();

        Vector3 targetXZ = headPos + right * ballRestartRightMeters + forward * ballRestartForwardMeters;
        Vector3 finalPos = FitBallToGroundAndDrop(targetXZ);

        golfBall.position = finalPos;
        golfBall.linearVelocity = Vector3.zero;
        golfBall.angularVelocity = Vector3.zero;
        golfBall.isKinematic = false;
        golfBall.useGravity = true;
        golfBall.constraints = RigidbodyConstraints.None;
        golfBall.WakeUp();

        // Reset should never instantly teleport
        _ignoreTeleportUntil = Time.time + ignoreTeleportAfterReset;
        ballHasMoved = false;
        stillTimer = 0f;
        _teleportArmed = false;
    }

    private void RecallGolfClubInFront()
    {
        if (golfClubRoot == null || hmd == null)
        {
            Debug.LogWarning($"[LocomotionTechnique] RecallGolfClubInFront blocked. golfClubRoot={(golfClubRoot ? "OK" : "NULL")} hmd={(hmd ? "OK" : "NULL")}");
            return;
        }

        Vector3 headPos = hmd.transform.position;

        Vector3 right = hmd.transform.right; right.y = 0f; right.Normalize();
        Vector3 forward = hmd.transform.forward; forward.y = 0f; forward.Normalize();

        Vector3 target = headPos + forward * clubForwardMeters + right * clubRightMeters + Vector3.up * clubUpMeters;
        Quaternion yaw = Quaternion.LookRotation(forward, Vector3.up);

        if (golfClubRb != null)
        {
            golfClubRb.position = target;
            golfClubRb.rotation = yaw;
            golfClubRb.linearVelocity = Vector3.zero;
            golfClubRb.angularVelocity = Vector3.zero;
            golfClubRb.WakeUp();
        }
        else
        {
            golfClubRoot.SetPositionAndRotation(target, yaw);
        }
    }

    private Vector3 FitBallToGroundAndDrop(Vector3 targetXZ)
    {
        float fallbackY = (golfBall != null) ? golfBall.position.y : 0f;

        Vector3 rayOrigin = targetXZ + Vector3.up * 5f;
        float rayLength = 30f;

        float radius = 0.0225f;
        if (golfBall != null)
        {
            var sphere = golfBall.GetComponent<SphereCollider>();
            if (sphere != null)
            {
                float scale = Mathf.Max(golfBall.transform.lossyScale.x, golfBall.transform.lossyScale.z);
                radius = sphere.radius * scale;
            }
        }

        Vector3 finalPos = targetXZ;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, ~0, QueryTriggerInteraction.Ignore))
            finalPos.y = hit.point.y + radius + ballDropHeight;
        else
            finalPos.y = fallbackY + ballDropHeight;

        return finalPos;
    }

    private bool IsBallGrounded(float extra = 0.06f)
    {
        if (golfBall == null) return false;

        float radius = 0.0225f;
        var sphere = golfBall.GetComponent<SphereCollider>();
        if (sphere != null)
        {
            float scale = Mathf.Max(golfBall.transform.lossyScale.x, golfBall.transform.lossyScale.z);
            radius = sphere.radius * scale;
        }

        Vector3 origin = golfBall.worldCenterOfMass + Vector3.up * 0.02f;
        float castDistance = radius + extra;
        float castRadius = radius * 0.9f;

        return Physics.SphereCast(origin, castRadius, Vector3.down, out _, castDistance, ~0, QueryTriggerInteraction.Ignore);
    }

    void OnTriggerEnter(Collider other)
    {
        // These are for the game mechanism.
        if (other.CompareTag("banner"))
        {
            stage = other.gameObject.name;
            parkourCounter.isStageChange = true;
        }
        else if (other.CompareTag("objectInteractionTask"))
        {
            selectionTaskMeasure.isTaskStart = true;
            selectionTaskMeasure.scoreText.text = "";
            selectionTaskMeasure.partSumErr = 0f;
            selectionTaskMeasure.partSumTime = 0f;
            // rotation: facing the user's entering direction
            float tempValueY = other.transform.position.y > 0 ? 12 : 0;
            Vector3 tmpTarget = new(hmd.transform.position.x, tempValueY, hmd.transform.position.z);
            selectionTaskMeasure.taskUI.transform.LookAt(tmpTarget);
            selectionTaskMeasure.taskUI.transform.Rotate(new Vector3(0, 180f, 0));
            selectionTaskMeasure.taskStartPanel.SetActive(true);
        }
        else if (other.CompareTag("coin"))
        {
            parkourCounter.coinCount += 1;
            GetComponent<AudioSource>().Play();
            other.gameObject.SetActive(false);
        }
        // These are for the game mechanism.
    }
}
