using UnityEngine;

public class BowAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerShooting target;

    private void Awake()
    {
        if (target == null)
            target = GetComponentInParent<PlayerShooting>();
    }

    public void ReleaseArrow()
    {
        if (target != null) target.ReleaseArrow();
    }

    public void AnimationEvent_Fire()
    {
        if (target != null) target.ReleaseArrow();
    }

    public void AE_Fire()
    {
        if (target != null) target.ReleaseArrow();
    }

    public void ShootArrow()
    {
        if (target != null) target.ReleaseArrow();
    }
}
