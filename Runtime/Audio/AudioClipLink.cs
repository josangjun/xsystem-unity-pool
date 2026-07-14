using UnityEngine;
using System.Reflection;

namespace XSystem
{
    [System.Serializable]
    public class AudioClipLink : SoftLink<AudioClip>
    {
        public AudioClipLink(string guid) : base(guid) { }
    }
}

#if UNITY_EDITOR
namespace XSystem
{
    using UnityEditor;
    [CustomPropertyDrawer(typeof(AudioClipLink))]
    public class AudioClipLinkDrawer : SoftLinkDrawer
    {
        protected override System.Type GetFieldType()
        {
            return typeof(AudioClip);
        }

        protected override void DrawProperty(Rect position, SerializedProperty property, GUIContent content)
        {
            var fieldRect = position;
            var playRect = position;
            var stopRect = position;

            fieldRect.width = position.width * 0.85f;
            playRect.xMin = fieldRect.xMax + 2f;
            playRect.width = playRect.width * 0.5f - 1f;
            stopRect.xMin = playRect.xMax + 2f;

            base.DrawProperty(fieldRect, property, content);

            if (GUI.Button(playRect, "▷"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(pathProp.stringValue);
                if (clip)
                    PlayClip(clip);
            }
            if (GUI.Button(stopRect, "□"))
            {
                StopAllClips();
            }
        }

        public static void PlayClip(AudioClip clip, int startSample = 0, bool loop = false)
        {
            StopAllClips();
            var unityEditorAssembly = typeof(AudioImporter).Assembly;

            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtilClass.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null
            );

            Debug.Log($"PlayClip:{clip.name}");
            method.Invoke(
                null,
                new object[] { clip, startSample, loop }
            );
        }

        public static void StopAllClips()
        {
            var unityEditorAssembly = typeof(AudioImporter).Assembly;

            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtilClass.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new System.Type[] { },
                null
            );

            method.Invoke(
                null,
                new object[] { }
            );
        }
    }
}
#endif
