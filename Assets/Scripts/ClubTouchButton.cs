using UnityEngine;
using UnityEngine.Events;

public class ClubTouchButton : MonoBehaviour
{
    [Header("Only this tag can press")]
    [SerializeField] private string allowedTag = "GolfClub";

    [Header("What happens when pressed")]
    public UnityEvent onPressed;

    [Header("Optional")]
    [SerializeField] private bool disableOnPress = true;

    private bool _pressed;

    private void OnEnable()
    {
        // Panel yeniden görünür olunca tekrar basılabilsin
        _pressed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_pressed) return;
        if (!other.CompareTag(allowedTag)) return;

        _pressed = true;
        onPressed?.Invoke();

        if (disableOnPress)
            gameObject.SetActive(false);
    }
}
