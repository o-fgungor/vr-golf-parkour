using UnityEngine;

public class LookAtHMDYaw : MonoBehaviour
{
    [SerializeField] private Transform hmd;

    void LateUpdate()
    {
        if (hmd == null) return;

        Vector3 targetPos = hmd.position;
        targetPos.y = transform.position.y;

        Vector3 dir = transform.position - targetPos;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir);
    }

    public void SetHMD(Transform hmdTransform)
    {
        hmd = hmdTransform;
    }
}
