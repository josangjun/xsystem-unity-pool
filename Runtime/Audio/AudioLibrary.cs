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
            foreach (var preset in _presets)
            {
                _dict[preset.Hash] = preset;
            }
        }

        private IDictionary<int, AudioPreset> _dict = new Dictionary<int, AudioPreset>();

        public AudioPreset GetPreset(string name)
        {
            var h = AudioPreset.StringToHash(name);
            var preset = GetPreset(h);
            if (preset == null)
            {
                Debug.LogWarning($"AudioPreset with name '{name}' not found in AudioLibrary '{this.name}'.");
                return null;
            }
            return preset;
        }
        
        public AudioPreset GetPreset(int hash)
        {
            if (_dict.TryGetValue(hash, out var preset))
                return preset;
            
            return null;
        }
        
        public void Clear()
        {
            foreach (var preset in _presets)
            {
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