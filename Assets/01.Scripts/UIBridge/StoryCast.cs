using System;
using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 오프닝 스토리에 나오는 인물(마왕·마왕군·용사)의 실제 게임 프리팹 목록. Resources/Story/StoryCast 에 두고 StoryIntro 가 읽는다.
    /// 프리팹을 복사하지 않고 원본을 그대로 가리킨다.
    /// </summary>
    public sealed class StoryCast : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] _entries;

        public GameObject Get(string id)
        {
            if (_entries == null) return null;
            foreach (var e in _entries)
                if (e.id == id) return e.prefab;
            return null;
        }
    }
}
