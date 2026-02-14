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

    public static void PlayCross(this Entity e, int index, float duration = -1)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (em.HasComponent<AnimationCrossFade>(e))
        {
            var crossFade = em.GetComponentData<AnimationCrossFade>(e);
            
            // Set duration
            if (duration < 0)
            {
                if (em.HasComponent<AnimationCrossSecond>(e))
                {
                    crossFade.Duration = em.GetComponentData<AnimationCrossSecond>(e).Value;
                }
                else
                {
                    crossFade.Duration = 0.25f; // Default fallback
                }
            }
            else
            {
                crossFade.Duration = duration;
            }

            crossFade.TargetAnimationIndex = index;
            crossFade.Timer = 0f;
            crossFade.Initialized = false; // Trigger capture of current pose

            ecb.SetComponent(e, crossFade);
            ecb.SetComponentEnabled<AnimationCrossFade>(e, true);
        }
        else
        {
            // Fallback for immediate play if component missing
            SetAnimation(e, index);
        }

        ecb.Playback(em);
        ecb.Dispose();
    }
}
