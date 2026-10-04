using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

//one-shot migration: token clips were authored against SpriteRenderer.m_Sprite, but tokens are now UI Images.
//moves every such sprite curve onto Image.m_Sprite, keeping the path and keyframes. safe to re-run
public static class RebindSpriteClipsToImage
{
    [MenuItem("Tools/Rebind Sprite Clips To UI Image")]
    static void Rebind()
    {
        int clipCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip == null) continue;

            bool changed = false;
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;

                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                EditorCurveBinding imageBinding = EditorCurveBinding.PPtrCurve(binding.path, typeof(Image), "m_Sprite");

                Undo.RecordObject(clip, "Rebind sprite clips to Image");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                AnimationUtility.SetObjectReferenceCurve(clip, imageBinding, keys);
                changed = true;
            }

            if (!changed) continue;
            EditorUtility.SetDirty(clip);
            clipCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"RebindSpriteClipsToImage: rebound {clipCount} clip(s) to Image.m_Sprite");
    }
}
