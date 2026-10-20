using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deeptide.View
{
    /// <summary>
    /// 美术资源映射（策划/08 §4、10 §11.3）：内容 ID → 打包贴图。Core 只认 ID，表现层在这里查图。
    /// 资源放在 <c>Art/Resources/ArtDatabase.asset</c>，由 ArtPacker 打包卡图时自动创建和更新；还没有资源时所有查询返回 null，卡面继续用灰盒。
    /// Demo 规模下直接引用贴图，不上 Addressables。
    /// </summary>
    [CreateAssetMenu(menuName = "Deeptide/ArtDatabase", fileName = "ArtDatabase")]
    public sealed class ArtDatabase : ScriptableObject
    {
        public const string ResourcePath = "ArtDatabase";

        [Serializable]
        public struct Entry
        {
            public string Id;
            public Texture2D Texture;
        }

        [Tooltip("卡图：卡牌 ID（不带升级的 +）→ 打包贴图")]
        public List<Entry> Cards = new List<Entry>();

        static ArtDatabase _instance;
        static bool _loaded;
        static Dictionary<string, Texture2D> _cards;
        static readonly Dictionary<string, Texture2D> Overrides = new Dictionary<string, Texture2D>();

        public static ArtDatabase Instance
        {
            get
            {
                if (_loaded) return _instance;
                _loaded = true;
                _instance = Resources.Load<ArtDatabase>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>一张牌的卡图；没有时返回 null。升级标记（C06+）会被忽略。</summary>
        public static Texture2D Card(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            id = id.TrimEnd('+');
            if (Overrides.TryGetValue(id, out var o)) return o;
            if (_cards == null)
            {
                _cards = new Dictionary<string, Texture2D>();
                var db = Instance;
                if (db != null)
                    foreach (var e in db.Cards)
                        if (!string.IsNullOrEmpty(e.Id) && e.Texture != null) _cards[e.Id] = e.Texture;
            }
            return _cards.TryGetValue(id, out var t) ? t : null;
        }

        /// <summary>运行时临时替换某张牌的卡图（测试、预览用），不改资源。</summary>
        public static void SetOverride(string id, Texture2D texture)
        {
            if (texture == null) Overrides.Remove(id);
            else Overrides[id] = texture;
        }

        public static void ClearOverrides() => Overrides.Clear();

        /// <summary>编辑器里改了资源之后让缓存失效。</summary>
        public static void Invalidate()
        {
            _loaded = false;
            _instance = null;
            _cards = null;
        }

        /// <summary>添加或替换一条（ArtPacker 用）。</summary>
        public void SetCard(string id, Texture2D texture)
        {
            int i = Cards.FindIndex(e => e.Id == id);
            var entry = new Entry { Id = id, Texture = texture };
            if (i >= 0) Cards[i] = entry;
            else Cards.Add(entry);
            Cards.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            _cards = null;
        }
    }
}
