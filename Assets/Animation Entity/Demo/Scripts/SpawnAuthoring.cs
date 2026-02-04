using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct PrefabModel : IComponentData
{
    public Entity Value;
}

public class SpawnAuthoring : MonoBehaviour
{
    public GameObject prefab;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            for (var i = 0; i < 10; i++)
            {
                for (var j = 0; j < 10; j++)
                {
                    var pos = new float3(i, 0f, j);
                    Instantiate(prefab, pos, Quaternion.identity);
                }
            }
        }
    }

    public class SpawnBaker : Baker<SpawnAuthoring>
    {
        public override void Bake(SpawnAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PrefabModel
            {
                Value = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
public partial struct SpawnSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            foreach (var prefab in SystemAPI.Query<RefRO<PrefabModel>>())
            {
                for (var i = 0; i < 10; i++)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        var pos = new float3(i, 0f, j);
                        var model = ecb.Instantiate(prefab.ValueRO.Value);
                        ecb.SetComponent(model, new LocalTransform()
                        {
                            Position = pos,
                            Rotation = quaternion.identity,
                            Scale = 1f
                        });
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}