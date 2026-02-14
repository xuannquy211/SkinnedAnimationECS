using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif



[System.Serializable]
public struct AnimationPropertyCurve {
    public string propertyName;
    public PropertyType propertyType;
    public Keyframe[] keyframes;

    public AnimationPropertyCurve(string name, Keyframe[] keys) {
        propertyName = name;
        propertyType = MapPropertyType(name);
        keyframes = keys;
    }

    private static PropertyType MapPropertyType(string propName) {
        switch (propName) {
            case "m_LocalPosition.x": return PropertyType.PositionX;
            case "m_LocalPosition.y": return PropertyType.PositionY;
            case "m_LocalPosition.z": return PropertyType.PositionZ;

            case "m_LocalRotation.x": return PropertyType.RotationX;
            case "m_LocalRotation.y": return PropertyType.RotationY;
            case "m_LocalRotation.z": return PropertyType.RotationZ;
            case "m_LocalRotation.w": return PropertyType.RotationW;

            case "localEulerAnglesRaw.x": return PropertyType.EulerX;
            case "localEulerAnglesRaw.y": return PropertyType.EulerY;
            case "localEulerAnglesRaw.z": return PropertyType.EulerZ;

            case "m_LocalScale.x": return PropertyType.ScaleX;
            case "m_LocalScale.y": return PropertyType.ScaleY;
            case "m_LocalScale.z": return PropertyType.ScaleZ;

            default: return PropertyType.Unknown;
        }
    }

    public float Evaluate(float time) {
        if (keyframes == null || keyframes.Length == 0)
            return 0f;

        if (keyframes.Length == 1)
            return keyframes[0].value;

        if (time <= keyframes[0].time)
            return keyframes[0].value;

        // Nếu time nằm sau keyframe cuối
        if (time >= keyframes[^1].time)
            return keyframes[^1].value;

        for (int i = 0; i < keyframes.Length - 1; i++) {
            var k0 = keyframes[i];
            var k1 = keyframes[i + 1];

            if (time >= k0.time && time <= k1.time) {
                float t = (time - k0.time) / (k1.time - k0.time);

                // Hermite interpolation theo Unity
                float m0 = k0.outTangent * (k1.time - k0.time);
                float m1 = k1.inTangent * (k1.time - k0.time);

                float t2 = t * t;
                float t3 = t2 * t;

                float h00 = 2 * t3 - 3 * t2 + 1;
                float h10 = t3 - 2 * t2 + t;
                float h01 = -2 * t3 + 3 * t2;
                float h11 = t3 - t2;

                return h00 * k0.value + h10 * m0 + h01 * k1.value + h11 * m1;
            }
        }

        return 0f;
    }
}

[System.Serializable]
public struct TransformCurveEntity {
    public string transformPath; // Tên/path transform
    public Transform transformTarget;
    public AnimationPropertyCurve[] curves; // Các curve thuộc transform này
}

[System.Serializable]
public struct AnimationClipEntity {
    public string name;
    public float length;
    public float frameRate;
    public int frameCount;
    public TransformCurveEntity[] transformCurves; // Gom nhóm theo transform
}

public class AnimationClipAuthoring : MonoBehaviour {
    public AnimationClip[] animationClips;
    [HideInInspector] public AnimationClipEntity[] animationClipEntities;

#if UNITY_EDITOR
    private void OnValidate() {
        if (animationClips == null) {
            animationClipEntities = null;
            return;
        }

        animationClipEntities = new AnimationClipEntity[animationClips.Length];

        for (int i = 0; i < animationClips.Length; i++) {
            AnimationClip clip = animationClips[i];
            if (clip == null) {
                animationClipEntities[i] = new AnimationClipEntity {
                    name = "",
                    length = 0f,
                    frameRate = 0f,
                    frameCount = 0,
                    transformCurves = new TransformCurveEntity[0]
                };
                continue;
            }

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var groupedCurves = new Dictionary<string, List<AnimationPropertyCurve>>();

            foreach (var binding in bindings) {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (!groupedCurves.ContainsKey(binding.path))
                    groupedCurves[binding.path] = new List<AnimationPropertyCurve>();

                groupedCurves[binding.path].Add(new AnimationPropertyCurve(binding.propertyName,
                    curve != null ? curve.keys : new Keyframe[0]));
            }

            // Chuyển Dictionary thành array
            TransformCurveEntity[] transformCurveEntities = new TransformCurveEntity[groupedCurves.Count];
            int idx = 0;
            foreach (var kvp in groupedCurves) {
                transformCurveEntities[idx] = new TransformCurveEntity {
                    transformPath = kvp.Key, transformTarget = transform.Find(kvp.Key), curves = kvp.Value.ToArray()
                };
                idx++;
            }

            animationClipEntities[i] = new AnimationClipEntity {
                name = clip.name,
                length = clip.length,
                frameRate = clip.frameRate,
                frameCount = Mathf.RoundToInt(clip.length * clip.frameRate),
                transformCurves = transformCurveEntities
            };
        }
    }
#endif
}