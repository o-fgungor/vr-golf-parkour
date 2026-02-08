using UnityEngine;

public class LocomotionTechnique : MonoBehaviour
{
    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    [Range(0, 10)] public float translationGain = 0.5f;
    public GameObject hmd;

    public enum BallMode { TeleportBall, RedNoTeleport }

    [Header("BALL MODE (Toggle with Left Stick Click)")]
    [SerializeField] private BallMode currentBallMode = BallMode.TeleportBall;

    [Tooltip("Renderer of the golf ball to change its color. If empty, auto-find from golfBall.")]
    [SerializeField] private Renderer golfBallRenderer;

    [SerializeField] private Color teleportBallColor = Color.white;
    [SerializeField] private Color redNoTeleportColor = Color.red;
    private MaterialPropertyBlock _mpb;

    [Header("GOLF LOCOMOTION")]
    [SerializeField] private Rigidbody golfBall;
    [SerializeField] private bool requireBallMovementBeforeTeleport = true;

    [Header("STOP DETECTION (Teleport Trigger)")]
    [SerializeField] private float stopSpeedThreshold = 0.30f;
    [SerializeField] private float stopAngularThreshold = 1.50f;
    [SerializeField] private float stopHoldTime = 0.15f;

    [Header("TELEPORT OFFSETS")]
    [SerializeField] private float teleportLeftOffsetMeters = 0.50f;
    [SerializeField] private float teleportBackOffsetMeters = 0.10f;
    [SerializeField] private bool keepRigY = true;

    [Header("BALL PLACEMENT / DROP")]
    [SerializeField] private bool positionBallOnStart = true;
    [SerializeField] private float startRightOffsetMeters = 0.35f;
    [SerializeField] private float startForwardOffsetMeters = 0.80f;
    [SerializeField] private float ballDropHeight = 0.20f;
    [SerializeField] private float ignoreTeleportAfterReset = 0.60f;

    [Header("RECALL / RESET (X/Y)")]
    [SerializeField] private Vector3 clubRotationOffsetEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] private Transform golfClubRoot;
    [SerializeField] private Rigidbody golfClubRb;
    [SerializeField] private float clubForwardMeters = 0.80f;
    [SerializeField] private float clubRightMeters = 0.20f;
    [SerializeField] private float clubUpMeters = 0.00f;
    [SerializeField] private float ballRestartRightMeters = 0.35f;
    [SerializeField] private float ballRestartForwardMeters = 0.80f;

    [Header("BANNER TRIGGER FIX (Teleport may skip triggers)")]
    [SerializeField] private float bannerOverlapRadius = 0.35f;
    [SerializeField] private float bannerSweepRadius = 0.25f;

    [Header("RESPAWN (Stable)")]
    [SerializeField] private bool respawnKeepCurrentRigY = true;
    [SerializeField] private bool respawnSnapToGround = false;
    [SerializeField] private float respawnGroundRayUp = 3f;
    [SerializeField] private float respawnGroundRayDown = 10f;
    [SerializeField] private float respawnGroundYOffset = 0f;

    [Header("SIMPLE SPRINT BOOST (adds extra movement on top of existing locomotion)")]
    [SerializeField] private bool enableSprintBoost = true;
    [SerializeField] private float sprintExtraMultiplier = 2f;
    [SerializeField] private float stickDeadzone = 0.15f;
    [Range(0f, 1f)] [SerializeField] private float sprintTriggerThreshold = 0.2f;

    // stop/teleport state
    private float stillTimer;
    private bool ballHasMoved;
    private bool _teleportArmed = false;
    private float _ignoreTeleportUntil = 0f;

    /////////////////////////////////////////////////////////
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

        ApplyBallModeVisual();

        if (positionBallOnStart && golfBall != null && hmd != null)
            PlaceBallAtStart();
    }

    void Update()
    {
        // MODE TOGGLE: Left stick click
        if (OVRInput.GetDown(OVRInput.RawButton.LThumbstick))
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

        // TELEPORT: sadece TeleportBall modunda çalışsın
        if (currentBallMode == BallMode.TeleportBall)
        {
            RunTeleportLogic();
        }
        else
        {
            // No-teleport modunda state'i sıfır tut (yanlışlıkla teleport tetiklenmesin)
            stillTimer = 0f;
            ballHasMoved = false;
            _teleportArmed = false;
        }

        // Respawn (B)
        if (OVRInput.GetDown(OVRInput.RawButton.B))
        {
            if (parkourCounter != null && parkourCounter.parkourStart)
            {
                RespawnStableTo(parkourCounter.currentRespawnPos);
            }
        }

        // Sprint boost (LT + left stick)
        if (enableSprintBoost && hmd != null)
        {
            float lt = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger);
            if (lt >= sprintTriggerThreshold)
            {
                Vector2 stick = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
                if (stick.magnitude >= stickDeadzone)
                {
                    Vector3 fwd = hmd.transform.forward; fwd.y = 0f; fwd.Normalize();
                    Vector3 right = hmd.transform.right; right.y = 0f; right.Normalize();

                    Vector3 extraMove = (right * stick.x + fwd * stick.y) * (sprintExtraMultiplier * Time.deltaTime);
                    transform.position += extraMove;
                }
            }
        }
    }

    private void ToggleBallMode()
    {
        currentBallMode = (currentBallMode == BallMode.TeleportBall)
            ? BallMode.RedNoTeleport
            : BallMode.TeleportBall;

        ApplyBallModeVisual();

        // Mode değişince teleport state temizle
        stillTimer = 0f;
        ballHasMoved = false;
        _teleportArmed = false;
        _ignoreTeleportUntil = Time.time + 0.15f;

        Debug.Log($"[LocomotionTechnique] Ball mode -> {currentBallMode}");
    }

    private void ApplyBallModeVisual()
    {
        if (golfBallRenderer == null) return;

        Color col = (currentBallMode == BallMode.TeleportBall) ? teleportBallColor : redNoTeleportColor;

        golfBallRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", col);
        _mpb.SetColor("_BaseColor", col); // URP/Lit
        golfBallRenderer.SetPropertyBlock(_mpb);
    }

    private void RunTeleportLogic()
    {
        if (golfBall == null || hmd == null) return;

        bool ignoreTeleport = Time.time < _ignoreTeleportUntil;
        bool grounded = IsBallGrounded();

        float linearSpeed = golfBall.linearVelocity.magnitude;
        float angularSpeed = golfBall.angularVelocity.magnitude;

        if (linearSpeed > stopSpeedThreshold || angularSpeed > stopAngularThreshold)
        {
            ballHasMoved = true;
            stillTimer = 0f;
            if (!ignoreTeleport) _teleportArmed = true;
        }

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

    private void TeleportToBallLeftSide()
    {
        if (golfBall == null || hmd == null) return;

        Vector3 headBefore = hmd.transform.position;
        Vector3 ballPos = golfBall.position;

        Vector3 right = hmd.transform.right; right.y = 0f;
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();

        Vector3 forward = hmd.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 desiredHeadWorld = ballPos - right * teleportLeftOffsetMeters - forward * teleportBackOffsetMeters;

        Vector3 headPos = hmd.transform.position;
        Vector3 rigPos = transform.position;

        Vector3 headXZ = new Vector3(headPos.x, 0f, headPos.z);
        Vector3 rigXZ = new Vector3(rigPos.x, 0f, rigPos.z);
        Vector3 rigFromHeadXZ = rigXZ - headXZ;

        Vector3 newRigPos = new Vector3(desiredHeadWorld.x, 0f, desiredHeadWorld.z) + rigFromHeadXZ;
        newRigPos.y = keepRigY ? transform.position.y : rigPos.y;

        transform.position = newRigPos;

        ForceBannerTriggerBetween(headBefore, desiredHeadWorld);
    }

    private void ForceBannerTriggerBetween(Vector3 fromHead, Vector3 toHeadExpected)
    {
        if (parkourCounter == null) return;

        if (TryTriggerBannerAtPosition(toHeadExpected)) return;

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
            QueryTriggerInteraction.Collide
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
        if (golfBall == null || hmd == null) return;

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

        _ignoreTeleportUntil = Time.time + ignoreTeleportAfterReset;
        ballHasMoved = false;
        stillTimer = 0f;
        _teleportArmed = false;
    }

    private void RecallGolfClubInFront()
    {
        if (golfClubRoot == null || hmd == null) return;

        Vector3 headPos = hmd.transform.position;

        Vector3 right = hmd.transform.right; right.y = 0f; right.Normalize();
        Vector3 forward = hmd.transform.forward; forward.y = 0f; forward.Normalize();

        Vector3 target = headPos + forward * clubForwardMeters + right * clubRightMeters + Vector3.up * clubUpMeters;
        Quaternion yaw = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(clubRotationOffsetEuler);

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

            float tempValueY = other.transform.position.y > 0 ? 12 : 0;
            Vector3 tmpTarget = new(hmd.transform.position.x, tempValueY, hmd.transform.position.z);
            selectionTaskMeasure.taskUI.transform.LookAt(tmpTarget);
            selectionTaskMeasure.taskUI.transform.Rotate(new Vector3(-60, 180f, 0));
            selectionTaskMeasure.taskStartPanel.SetActive(true);
        }
    }

    private void RespawnStableTo(Vector3 respawnPoint)
    {
        if (hmd == null) return;

        Vector3 headPos = hmd.transform.position;
        Vector3 rigPos = transform.position;

        Vector3 headXZ = new Vector3(headPos.x, 0f, headPos.z);
        Vector3 rigXZ = new Vector3(rigPos.x, 0f, rigPos.z);
        Vector3 rigFromHeadXZ = rigXZ - headXZ;

        Vector3 newRigPos = new Vector3(respawnPoint.x, 0f, respawnPoint.z) + rigFromHeadXZ;

        newRigPos.y = respawnKeepCurrentRigY ? transform.position.y : respawnPoint.y;

        if (respawnSnapToGround)
        {
            Vector3 rayOrigin = new Vector3(newRigPos.x, newRigPos.y, newRigPos.z) + Vector3.up * respawnGroundRayUp;
            float rayLen = respawnGroundRayUp + respawnGroundRayDown;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLen, ~0, QueryTriggerInteraction.Ignore))
                newRigPos.y = hit.point.y + respawnGroundYOffset;
        }

        stillTimer = 0f;
        ballHasMoved = false;
        _teleportArmed = false;
        _ignoreTeleportUntil = Time.time + 0.15f;

        transform.position = newRigPos;
    }
}
