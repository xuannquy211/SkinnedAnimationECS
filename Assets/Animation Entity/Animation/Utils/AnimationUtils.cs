using Unity.Entities;

public static class AnimationUtils
{
    public static void SetAnimation(EntityManager em, Entity e, int index)
    {
        if (em.HasComponent<AnimationClipIndex>(e))
        {
            var animationClipIndex = em.GetComponentData<AnimationClipIndex>(e);
            animationClipIndex.Value = index;
            em.SetComponentData(e, animationClipIndex);
            
            // Also reset time when changing animation? The original system seems to handle this in OnChangeAnimationIndexSystem by resetting bone time.
            // But we should probably reset the entity time as well if needed?
            // The OnChangeAnimationIndexSystem resets bone time, but maybe we should let the system handle it.
        }
    }

    public static void SetAnimationTime(EntityManager em, Entity e, float time)
    {
        if (em.HasComponent<AnimationTime>(e))
        {
            var animationTime = em.GetComponentData<AnimationTime>(e);
            animationTime.Value = time;
            em.SetComponentData(e, animationTime);
        }
    }
}
