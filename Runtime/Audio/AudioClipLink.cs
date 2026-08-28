using UnityEngine;
using System.Reflection;

namespace XSystem
{
    [System.Serializable]
    public class AudioClipLink : AssetLink<AudioClip>
    {
        public AudioClipLink() { }

        public AudioClipLink(string guid) : base(guid) { }
    }
}

#if UNITY_EDITOR
namespace XSystem
{
    using UnityEditor;
    [CustomPropertyDrawer(typeof(AudioClipLink))]
    public class AudioClipLinkDrawer : AssetLinkDrawer
    {
        protected override void DrawProperty(Rect position, SerializedProperty property, GUIContent content)
        {
            var fieldRect = position;
            var playRect = position;
            var stopRect = position;

            fieldRect.width = position.width * 0.85f;
            playRect.x = fieldRect.xMax + 2f;
            playRect.width = (position.xMax - playRect.x - 2f) * 0.5f;
            stopRect.x = playRect.xMax + 2f;
            stopRect.width = position.xMax - stopRect.x;

            base.DrawProperty(fieldRect, property, content);

            if (GUI.Button(playRect, "▷"))
            {
                var path = GetAssetPath();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
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
