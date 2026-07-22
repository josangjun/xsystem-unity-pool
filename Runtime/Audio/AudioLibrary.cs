using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Audio;

namespace XSystem
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "XSystem/Audio Library")]
    public class AudioLibrary : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField][Page, Searchable]
        private AudioPreset[] _presets;

        public AudioPreset[] Presets => _presets;

        public AudioMixerGroup mixerGroup;

        public void OnBeforeSerialize()
        {
            // Custom logic before serialization (if needed)
        }

        public void OnAfterDeserialize()
        {
            _dict.Clear();

            foreach (var preset in _presets)
            {
                if (preset != null && !string.IsNullOrEmpty(preset.Name))
                    _dict[preset.Name] = preset;
            }
        }

        private IDictionary<string, AudioPreset> _dict =
            new Dictionary<string, AudioPreset>(System.StringComparer.Ordinal);

        public AudioPreset GetPreset(string name)
        {
            var preset = _dict.TryGetValue(name, out var result) ? result : null;
            if (preset == null)
            {
                Debug.LogWarning($"AudioPreset with name '{name}' not found in AudioLibrary '{this.name}'.");
                return null;
            }
            return preset;
        }
        
        public void Clear()
        {
            foreach (var preset in _presets)
            {
                if (preset.clip != null && preset.clip.OperationHandle.IsValid())
                    preset.clip.ReleaseAsset();
            }
        }

        [ContextMenu("Sort")]
        public void Sort()
        {
            _presets = _presets.OrderBy(p => p.Name).ToArray();
        }

        [ContextMenu("Load From Directory")]
        public void LoadFromDirectory()
        {
            #if UNITY_EDITOR
            var dirPath = UnityEditor.EditorUtility.OpenFolderPanel("Select Audio Clips", "", "");
            if (string.IsNullOrEmpty(dirPath))
                return;
            dirPath = dirPath.Replace(Application.dataPath, "Assets");
            var guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { dirPath });
            _presets = new AudioPreset[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                var guid = guids[i];
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var filename = System.IO.Path.GetFileNameWithoutExtension(path);
                var clip = new AudioClipLink(guid);
                Debug.Log($"Loading audio file: {path}");
                _presets[i] = new AudioPreset(guid, filename, 1f, 0f, 1f, AudioConcurrency.Overlap);
            }
            Sort();
            #endif
        }
    }
}
