using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deeptide.View
{
    /// <summary>
    /// 美术资源映射（策划/08 §4、10 §11.3）：内容 ID → 打包贴图。Core 只认 ID，表现层在这里查图。
    /// 资源放在 <c>Art/Resources/ArtDatabase.asset</c>，由 ArtPacker 打包时自动创建和更新；还没有资源时所有查询返回 null，卡面和角色继续用灰盒。
    /// 五张表：卡图（卡牌 ID）、角色（敌人定义 ID，调查员是 <see cref="PlayerId"/>）、战斗背景（打包贴图）、
    /// 插图（事件、篝火、主视觉：全对比度的打包贴图，10 §8）、单色精灵（图标、框饰、UI 墨线、特效序列帧：白色 + alpha，由 Image 着色，10 §11.3）。
    /// Demo 规模下直接引用贴图，不上 Addressables。
    /// </summary>
    [CreateAssetMenu(menuName = "Deeptide/ArtDatabase", fileName = "ArtDatabase")]
    public sealed class ArtDatabase : ScriptableObject
    {
        public const string ResourcePath = "ArtDatabase";
        /// <summary>调查员在角色表里的 ID。</summary>
        public const string PlayerId = "player";

        [Serializable]
        public struct Entry
        {
            public string Id;
            public Texture2D Texture;
        }

        [Tooltip("卡图：卡牌 ID（不带升级的 +）→ 打包贴图")]
        public List<Entry> Cards = new List<Entry>();

        [Tooltip("角色：敌人定义 ID（Data/enemies.csv）或 player → 打包贴图（A 通道是轮廓）")]
        public List<Entry> Actors = new List<Entry>();

        [Tooltip("战斗背景：docks / chapel … → 打包贴图（R 淡墨、B 癫狂层）")]
        public List<Entry> Backgrounds = new List<Entry>();

        [Tooltip("插图：event_<事件 ID> / campfire / keyart → 打包贴图（R 墨、G 深渊遮罩、B 癫狂层），用 Deeptide/UI/Etching 着色")]
        public List<Entry> Illustrations = new List<Entry>();

        [Tooltip("单色精灵：intent_attack / status_fear / node_elite / frame_rare / ui_panel / vfx_hit_a_0 … → 白色 + alpha 的贴图")]
        public List<Entry> Sprites = new List<Entry>();

        static ArtDatabase _instance;
        static bool _loaded;
        static Dictionary<string, Texture2D> _cards, _actors, _backgrounds, _illustrations, _sprites;
        static readonly Dictionary<string, Texture2D> Overrides = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Texture2D> ActorOverrides = new Dictionary<string, Texture2D>();

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

        /// <summary>一个角色的图；没有时返回 null（继续用灰盒剪影）。</summary>
        public static Texture2D Actor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (ActorOverrides.TryGetValue(id, out var o)) return o;
            if (_actors == null)
            {
                _actors = new Dictionary<string, Texture2D>();
                var db = Instance;
                if (db != null && db.Actors != null)
                    foreach (var e in db.Actors)
                        if (!string.IsNullOrEmpty(e.Id) && e.Texture != null) _actors[e.Id] = e.Texture;
            }
            return _actors.TryGetValue(id, out var t) ? t : null;
        }

        /// <summary>一张战斗背景（打包贴图）；没有时返回 null。</summary>
        public static Texture2D Background(string id) => Lookup(id, ref _backgrounds, db => db.Backgrounds);

        /// <summary>一张插图（打包贴图）；没有时返回 null。</summary>
        public static Texture2D Illustration(string id) => Lookup(id, ref _illustrations, db => db.Illustrations);

        /// <summary>一张单色贴图（白色 + alpha）；没有时返回 null。做成精灵用 <see cref="ArtSprites"/>。</summary>
        public static Texture2D SpriteTexture(string id) => Lookup(id, ref _sprites, db => db.Sprites);

        static Texture2D Lookup(string id, ref Dictionary<string, Texture2D> cache, Func<ArtDatabase, List<Entry>> list)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache == null)
            {
                cache = new Dictionary<string, Texture2D>();
                var db = Instance;
                var entries = db != null ? list(db) : null;
                if (entries != null)
                    foreach (var e in entries)
                        if (!string.IsNullOrEmpty(e.Id) && e.Texture != null) cache[e.Id] = e.Texture;
            }
            return cache.TryGetValue(id, out var t) ? t : null;
        }

        /// <summary>运行时临时替换某个角色的图（测试、预览用）。</summary>
        public static void SetActorOverride(string id, Texture2D texture)
        {
            if (texture == null) ActorOverrides.Remove(id);
            else ActorOverrides[id] = texture;
        }

        /// <summary>运行时临时替换某张牌的卡图（测试、预览用），不改资源。</summary>
        public static void SetOverride(string id, Texture2D texture)
        {
            if (texture == null) Overrides.Remove(id);
            else Overrides[id] = texture;
        }

        public static void ClearOverrides()
        {
            Overrides.Clear();
            ActorOverrides.Clear();
        }

        /// <summary>编辑器里改了资源之后让缓存失效。</summary>
        public static void Invalidate()
        {
            _loaded = false;
            _instance = null;
            _cards = null;
            _actors = null;
            _backgrounds = null;
            _illustrations = null;
            _sprites = null;
            ArtSprites.Invalidate();
        }

        /// <summary>添加或替换一张卡图（ArtPacker 用）。</summary>
        public void SetCard(string id, Texture2D texture)
        {
            Set(Cards, id, texture);
            _cards = null;
        }

        /// <summary>添加或替换一个角色的图（ArtPacker 用）。</summary>
        public void SetActor(string id, Texture2D texture)
        {
            if (Actors == null) Actors = new List<Entry>();
            Set(Actors, id, texture);
            _actors = null;
        }

        /// <summary>添加或替换一张战斗背景（ArtPacker 用）。</summary>
        public void SetBackground(string id, Texture2D texture)
        {
            if (Backgrounds == null) Backgrounds = new List<Entry>();
            Set(Backgrounds, id, texture);
            _backgrounds = null;
        }

        /// <summary>添加或替换一张插图（ArtPacker 用）。</summary>
        public void SetIllustration(string id, Texture2D texture)
        {
            if (Illustrations == null) Illustrations = new List<Entry>();
            Set(Illustrations, id, texture);
            _illustrations = null;
        }

        /// <summary>添加或替换一张单色贴图（ArtPacker 用）。</summary>
        public void SetSprite(string id, Texture2D texture)
        {
            if (Sprites == null) Sprites = new List<Entry>();
            Set(Sprites, id, texture);
            _sprites = null;
            ArtSprites.Invalidate();
        }

        static void Set(List<Entry> list, string id, Texture2D texture)
        {
            int i = list.FindIndex(e => e.Id == id);
            var entry = new Entry { Id = id, Texture = texture };
            if (i >= 0) list[i] = entry;
            else list.Add(entry);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }
    }
}
