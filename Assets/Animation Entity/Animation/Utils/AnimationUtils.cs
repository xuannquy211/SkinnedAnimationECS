using Unity.Collections;
using Unity.Entities;

public static class AnimationUtils
{
    public static void SetAnimation(this Entity e, int index)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<AnimationClipIndex>(e))
        {
            var animationClipIndex = em.GetComponentData<AnimationClipIndex>(e);
            animationClipIndex.Value = index;
            ecb.SetComponent(e, animationClipIndex);
            
            // Also reset time when changing animation? The original system seems to handle this in OnChangeAnimationIndexSystem by resetting bone time.
            // But we should probably reset the entity time as well if needed?
            // The OnChangeAnimationIndexSystem resets bone time, but maybe we should let the system handle it.
        }
        
        ecb.Playback(em);
        ecb.Dispose();
    }

    public static void SetAnimationTime(this Entity e, float time)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (em.HasComponent<AnimationRootTime>(e))
        {
            var animationTime = em.GetComponentData<AnimationRootTime>(e);
            animationTime.Value = time;
            ecb.SetComponent(e, animationTime);
        }
        
        ecb.Playback(em);
        ecb.Dispose();
    }
}
