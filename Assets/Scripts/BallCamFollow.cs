using UnityEngine;

public class BallCamFollow : MonoBehaviour
{
    [Header("References")]
    public Rigidbody golfBall;

    [Tooltip("SADECE BallCam panel objesi (RawImage parent). Canvas root vermeyeceksin.")]
    public GameObject ballCamPanelRoot;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 1.0f, -2.5f);
    public float followSmooth = 12f;
    public float lookSmooth = 16f;

    [Header("Auto Show/Hide")]
    public float showSpeedThreshold = 0.6f;
    public float hideSpeedThreshold = 0.25f;
    public float hideDelay = 0.35f;

    private float _stillTimer = 0f;
    private bool _showing = false;

    void Start()
    {
        if (ballCamPanelRoot != null)
            ballCamPanelRoot.SetActive(false);
    }

    void LateUpdate()
    {
        if (golfBall == null) return;

        // Follow camera position
        Vector3 desiredPos = golfBall.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));

        // Look at ball
        Vector3 dir = (golfBall.position - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-lookSmooth * Time.deltaTime));
        }

        // Show/hide logic
        float speed = golfBall.linearVelocity.magnitude;

        if (speed >= showSpeedThreshold)
        {
            _stillTimer = 0f;
            SetBallCamPanel(true);
        }
        else if (speed <= hideSpeedThreshold)
        {
            _stillTimer += Time.deltaTime;
            if (_stillTimer >= hideDelay)
                SetBallCamPanel(false);
        }
        else
        {
            _stillTimer = 0f;
        }
    }

    private void SetBallCamPanel(bool on)
    {
        if (_showing == on) return;
        _showing = on;

        if (ballCamPanelRoot != null)
            ballCamPanelRoot.SetActive(on);
    }
}
