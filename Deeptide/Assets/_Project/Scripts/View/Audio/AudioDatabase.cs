using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deeptide.View.Audio
{
    /// <summary>
    /// 音频映射（策划/13 §6.1）：音效 ID → 片段。资源放在 <c>Audio/Resources/AudioDatabase.asset</c>，由 AudioPacker 打包时自动创建和更新。
    /// 条目的 Id 就是源文件名（不带扩展名）：<c>U04_attack_2</c>、<c>R01_4</c>。
    /// 查一个 ID 时，先找同名的条目，再找 <c>ID_1</c> … <c>ID_9</c>（变体），所以 <c>Variants("U04_attack")</c> 拿到三个、<c>Variants("R01_4")</c> 拿到一个。
    /// **没有素材的 ID 返回空**，播放端直接不出声、不报错，可以边做边接（和 ArtDatabase 没图就走灰盒一样）。
    /// </summary>
    [CreateAssetMenu(menuName = "Deeptide/AudioDatabase", fileName = "AudioDatabase")]
    public sealed class AudioDatabase : ScriptableObject
    {
        public const string ResourcePath = "AudioDatabase";

        [Serializable]
        public struct Entry
        {
            public string Id;
            public AudioClip Clip;
        }

        [Tooltip("音效：源文件名（U04_attack_2、R01_4 …）→ 片段")]
        public List<Entry> Sfx = new List<Entry>();

        static AudioDatabase _instance;
        static bool _loaded;
        static Dictionary<string, AudioClip> _clips;
        static readonly Dictionary<string, AudioClip[]> Cache = new Dictionary<string, AudioClip[]>();
        static readonly AudioClip[] None = new AudioClip[0];

        public static AudioDatabase Instance
        {
            get
            {
                if (_loaded) return _instance;
                _loaded = true;
                _instance = Resources.Load<AudioDatabase>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>一个 ID 的全部变体；没有素材时是空数组。</summary>
        public static AudioClip[] Variants(string id)
        {
            if (string.IsNullOrEmpty(id)) return None;
            if (Cache.TryGetValue(id, out var hit)) return hit;
            if (_clips == null)
            {
                _clips = new Dictionary<string, AudioClip>();
                var db = Instance;
                if (db != null)
                    foreach (var e in db.Sfx)
                        if (!string.IsNullOrEmpty(e.Id) && e.Clip != null) _clips[e.Id] = e.Clip;
            }
            var list = new List<AudioClip>();
            if (_clips.TryGetValue(id, out var exact)) list.Add(exact);
            else
                for (int i = 1; i <= 9; i++)
                    if (_clips.TryGetValue($"{id}_{i}", out var v)) list.Add(v);
            var result = list.Count > 0 ? list.ToArray() : None;
            Cache[id] = result;
            return result;
        }

        /// <summary>编辑器打包后清掉缓存。</summary>
        public static void Reload()
        {
            _loaded = false;
            _instance = null;
            _clips = null;
            Cache.Clear();
        }

#if UNITY_EDITOR
        public void SetSfx(string id, AudioClip clip)
        {
            int i = Sfx.FindIndex(e => e.Id == id);
            var entry = new Entry { Id = id, Clip = clip };
            if (i >= 0) Sfx[i] = entry;
            else Sfx.Add(entry);
        }
#endif
    }
}
