using UnityEngine;

public class ControllerLegendVisibility : MonoBehaviour
{
    [Header("Legend Roots")]
    [SerializeField] private GameObject leftLegend;
    [SerializeField] private GameObject rightLegend;

    [Header("Thresholds")]
    [SerializeField] private float triggerThreshold = 0.1f;
    [SerializeField] private float stickThreshold = 0.2f;

    void Update()
    {
        bool anyInputActive = AnyControllerInputActive();

        if (leftLegend && leftLegend.activeSelf == anyInputActive)
            leftLegend.SetActive(!anyInputActive);

        if (rightLegend && rightLegend.activeSelf == anyInputActive)
            rightLegend.SetActive(!anyInputActive);
    }

    private bool AnyControllerInputActive()
    {
        if (OVRInput.Get(OVRInput.RawButton.Any))
            return true;

        if (OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger) > triggerThreshold)
            return true;

        if (OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger) > triggerThreshold)
            return true;

        if (OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger) > triggerThreshold)
            return true;

        if (OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger) > triggerThreshold)
            return true;

        Vector2 lStick = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        Vector2 rStick = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);

        if (lStick.magnitude > stickThreshold)
            return true;

        if (rStick.magnitude > stickThreshold)
            return true;

        return false;
    }
}
