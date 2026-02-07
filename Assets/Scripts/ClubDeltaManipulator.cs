using UnityEngine;

public class ClubDeltaManipulator : MonoBehaviour
{
    [Header("Smoothing")]
    [Range(0f, 30f)] public float positionLerp = 12f; // 0 = anında 
    [Range(0f, 30f)] public float rotationLerp = 12f; // 0 = anında

    [Header("References")]
    [Tooltip("Golf club üzerinde referans alacağımız nokta (ObjectTManipPoint).")]
    public Transform clubPoint;

    [Header("Input (Right hand trigger)")]
    public bool useAnalog = false;
    public OVRInput.RawButton holdButton = OVRInput.RawButton.RIndexTrigger;
    [Range(0f, 1f)] public float analogThreshold = 0.2f;

    [Header("Options")]
    public bool freezeY = false;

    private bool _isHolding;

    private Vector3 _clubPos0;
    private Quaternion _clubRot0;

    private Vector3 _objPos0;
    private Quaternion _objRot0;

    private void Update()
    {
        if (clubPoint == null) return;

        bool pressed = useAnalog
            ? OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger) >= analogThreshold
            : OVRInput.Get(holdButton);

        if (pressed && !_isHolding)
            BeginHold();
        else if (!pressed && _isHolding)
            EndHold();

        if (_isHolding)
            ApplyDelta();
    }

    private void BeginHold()
    {
        _isHolding = true;

        // Club başlangıç pozu
        _clubPos0 = clubPoint.position;
        _clubRot0 = clubPoint.rotation;

        // ObjectT başlangıç pozu (trigger’a basıldığı an)
        _objPos0 = transform.position;
        _objRot0 = transform.rotation;
    }

    private void EndHold()
    {
        _isHolding = false;
        // bırakınca obje olduğu yerde kalır
    }

    private void ApplyDelta()
    {
        // Club’ın trigger’dan beri yaptığı dönüş farkı
        Quaternion deltaRot = clubPoint.rotation * Quaternion.Inverse(_clubRot0);

        // Club’ın trigger’dan beri yaptığı hareket farkı
        Vector3 deltaPos = clubPoint.position - _clubPos0;

        Quaternion newRot = deltaRot * _objRot0;
        Vector3 newPos = _objPos0 + deltaPos;

        if (freezeY)
            newPos.y = _objPos0.y;

        if (positionLerp <= 0f)
        {
            transform.position = newPos;
        }
        else
        {
            float t = 1f - Mathf.Exp(-positionLerp * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, newPos, t);
        }

        if (rotationLerp <= 0f)
        {
            transform.rotation = newRot;
        }
        else
        {
            float t = 1f - Mathf.Exp(-rotationLerp * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, newRot, t);
        }


    }
}
