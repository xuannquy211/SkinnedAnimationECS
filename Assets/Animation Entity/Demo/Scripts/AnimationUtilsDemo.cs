using UnityEngine;
using Unity.Entities;
using Unity.Collections;

public class AnimationUtilsDemo : MonoBehaviour
{
    [Header("Testing Parameters")]
    public int TargetAnimationIndex = 0;
    public float TargetTime = 0f;
    public float CrossDuration = 0.25f;

    [Header("Runtime Debug")]
    public string TargetEntityInfo = "None";
    private Entity _targetEntity = Entity.Null;

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Label("Animation Utils Demo");

        if (GUILayout.Button("Find Target Entity"))
        {
            FindTargetEntity();
        }

        GUILayout.Label($"Target: {TargetEntityInfo}");
        GUILayout.Space(10);

        if (_targetEntity != Entity.Null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Animation"))
            {
                _targetEntity.SetAnimation(TargetAnimationIndex);
            }
            GUILayout.TextField(TargetAnimationIndex.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Time"))
            {
                _targetEntity.SetAnimationTime(TargetTime);
            }
            GUILayout.TextField(TargetTime.ToString("F2"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play Cross"))
            {
                _targetEntity.PlayCross(TargetAnimationIndex, CrossDuration);
            }
            GUILayout.TextField(CrossDuration.ToString("F2"));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Allow manual input
            GUILayout.Label("Input Parameters:");
            string animIndexStr = GUILayout.TextField(TargetAnimationIndex.ToString());
            if (int.TryParse(animIndexStr, out int idx)) TargetAnimationIndex = idx;
            
            string timeStr = GUILayout.TextField(TargetTime.ToString());
            if (float.TryParse(timeStr, out float t)) TargetTime = t;
            
            string durStr = GUILayout.TextField(CrossDuration.ToString());
            if (float.TryParse(durStr, out float d)) CrossDuration = d;
        }
        else
        {
            GUILayout.Label("Please find a target entity first.");
        }

        GUILayout.EndArea();
    }

    private void FindTargetEntity()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            TargetEntityInfo = "No Default World";
            return;
        }

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(typeof(AnimationClipIndex));
        
        if (query.IsEmptyIgnoreFilter)
        {
            TargetEntityInfo = "No Entity Found";
            return;
        }

        using (var entities = query.ToEntityArray(Allocator.Temp))
        {
            if (entities.Length > 0)
            {
                _targetEntity = entities[0];
                TargetEntityInfo = $"Entity Index: {_targetEntity.Index}";
            }
        }
    }
}
