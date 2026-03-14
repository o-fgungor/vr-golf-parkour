using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GolfBallRollingResistance : MonoBehaviour
{
    [SerializeField] private Rigidbody ball;

    [Header("Ground Detection")]
    [SerializeField] private bool onlyApplyWhenGrounded = true;

    [SerializeField] private LayerMask groundMask = ~0;

    [Range(0f, 1f)]
    [SerializeField] private float groundNormalMinY = 0.5f;

    [Header("Damping Mode")]
    [SerializeField] private bool useTwoStageDamping = true;

    [Header("Standard Damping (Per Second)")]
    [Min(0f)]
    [SerializeField] private float highSpeedDamping = 0.05f;

    [Min(0f)]
    [SerializeField] private float lowSpeedDamping = 0.9f;

    [Min(0.01f)]
    [SerializeField] private float speedForHighDampingBlend = 3.0f;

    [Min(0f)]
    [SerializeField] private float angularDamping = 0.6f;

    [Header("Two-Stage Damping")]
    [Min(0f)]
    [SerializeField] private float twoStageSpeedThreshold = 1.5f;

    [Min(0f)]
    [SerializeField] private float fastTravelDamping = 0.02f;

    [Min(0f)]
    [SerializeField] private float quickStopDamping = 3.5f;

    [Min(0f)]
    [SerializeField] private float quickStopAngularDamping = 2.0f;

    [Header("Safety Caps")]
    [Min(0f)]
    [SerializeField] private float maxLinearSpeed = 25f;

    private int _groundContacts;

    private void Reset()
    {
        ball = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (ball == null)
            ball = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!CountsAsGround(collision)) return;
        _groundContacts++;
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!CountsAsGround(collision)) return;
        _groundContacts = Mathf.Max(0, _groundContacts - 1);
    }

    private bool CountsAsGround(Collision collision)
    {
        if (collision == null || collision.collider == null) return false;

        if (((1 << collision.collider.gameObject.layer) & groundMask.value) == 0)
            return false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            var cp = collision.GetContact(i);
            if (cp.normal.y >= groundNormalMinY)
                return true;
        }

        return false;
    }

    private void FixedUpdate()
    {
        if (ball == null) return;

        if (onlyApplyWhenGrounded && _groundContacts <= 0)
            return;

        Vector3 v = ball.linearVelocity;
        float speed = v.magnitude;

        if (speed > maxLinearSpeed)
        {
            ball.linearVelocity = v.normalized * maxLinearSpeed;
            speed = maxLinearSpeed;
        }

        float damping;
        float currentAngularDamping;

        if (useTwoStageDamping)
        {
            if (speed > twoStageSpeedThreshold)
            {
                damping = fastTravelDamping;
                currentAngularDamping = angularDamping;
            }
            else
            {
                damping = quickStopDamping;
                currentAngularDamping = quickStopAngularDamping;
            }
        }
        else
        {
            float t = Mathf.Clamp01(speed / speedForHighDampingBlend);
            damping = Mathf.Lerp(lowSpeedDamping, highSpeedDamping, t);
            currentAngularDamping = angularDamping;
        }

        float linearFactor = Mathf.Clamp01(1f - damping * Time.fixedDeltaTime);
        ball.linearVelocity *= linearFactor;

        float angularFactor = Mathf.Clamp01(1f - currentAngularDamping * Time.fixedDeltaTime);
        ball.angularVelocity *= angularFactor;
    }
}
