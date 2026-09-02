#if UNITY_EDITOR
using UnityEngine;

namespace XSystem
{
    [DisallowMultipleComponent]
    internal sealed class GameObjectPoolDebugView : MonoBehaviour
    {
        [SerializeField]
        private bool _sortByIdleCountDescending;
    }
}
#endif
