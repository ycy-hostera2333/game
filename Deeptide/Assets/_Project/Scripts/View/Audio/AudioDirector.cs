using System.Collections.Generic;
using UnityEngine;

namespace Deeptide.View.Audio
{
    /// <summary>
    /// 常驻的声音入口（策划/13 §6.1）：16 个 AudioSource 的池、同一 ID 80 ms 内最多 2 个、随机挑变体、音高随机 ±3%（R01 / R02 不动，它们的音高就是信息）。
    /// 第一次用到时自己建一个跨场景的物体，带上 AudioListener（两个场景里都没有摄像机自带的那个）。
    /// 现在只有音效；音乐、叠夜、环境和 AudioMixer 等曲子定稿后再接，所以总线先在这里用音量乘出来：主音量 = AudioListener.volume，音效音量乘在每个声源上。
    /// 没有素材的 ID 照样记进 <see cref="Requested"/>（测试看它），只是不出声。
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const int PoolSize = 16;
        public const float ThrottleWindow = 0.08f;
        public const int ThrottleMax = 2;
        const float PitchJitter = 0.03f;
        const int LogLimit = 256;

        public enum Volume { Master, Music, Sfx, Ambience }

        /// <summary>设置里「声音」一档一档切（13 §6.5；分总线的滑条等音乐接进来再做）。</summary>
        public static readonly float[] MasterSteps = { 1f, 0.6f, 0.3f, 0f };

        static AudioDirector _instance;

        public static AudioDirector Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("AudioDirector");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AudioDirector>();
                }
                return _instance;
            }
        }

        /// <summary>通过节流、真正请求播放的音效 ID（不带变体号），按时间顺序，测试用。</summary>
        public readonly List<string> Requested = new List<string>();
        /// <summary>有素材、真的出声了的次数。</summary>
        public int Played { get; private set; }
        /// <summary>被节流掉的次数。</summary>
        public int Throttled { get; private set; }

        readonly List<AudioSource> _pool = new List<AudioSource>();
        readonly float[] _busyUntil = new float[PoolSize];
        readonly Dictionary<string, List<float>> _recent = new Dictionary<string, List<float>>();

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0;
                _pool.Add(src);
            }
            AudioListener.volume = GetVolume(Volume.Master);
        }

        // ------------------------------------------------------------------ 音量（PlayerPrefs）

        static string Pref(Volume v) => "Deeptide.Volume." + v;

        public static float GetVolume(Volume v) => PlayerPrefs.GetFloat(Pref(v), 1f);

        public static void SetVolume(Volume v, float value)
        {
            PlayerPrefs.SetFloat(Pref(v), Mathf.Clamp01(value));
            PlayerPrefs.Save();
            if (v == Volume.Master) AudioListener.volume = Mathf.Clamp01(value);
        }

        /// <summary>主音量切到下一档，返回新的值。</summary>
        public static float CycleMaster()
        {
            float now = GetVolume(Volume.Master);
            int i = System.Array.FindIndex(MasterSteps, s => Mathf.Abs(s - now) < 0.01f);
            float next = MasterSteps[(i + 1) % MasterSteps.Length];
            SetVolume(Volume.Master, next);
            return next;
        }

        public static string MasterLabel()
        {
            float v = GetVolume(Volume.Master);
            return v <= 0 ? "声音：关" : $"声音：{Mathf.RoundToInt(v * 100)}%";
        }

        // ------------------------------------------------------------------ 播放

        /// <summary>界面音效的简写（U 系列）。</summary>
        public static void Ui(string id) => Instance.Play(id, SfxBus.Ui);

        /// <summary>一条战斗事件的全部音效。延迟按战斗倍速缩短，音高不变（13 §5.1）。</summary>
        public void Play(IEnumerable<Cue> cues, float speed = 1f)
        {
            foreach (var c in cues) Play(c.Id, c.Bus, c.Delay / Mathf.Max(0.01f, speed));
        }

        /// <summary>播一个音效 ID（不带变体号）。被节流时返回 false；没有素材时返回 true 但不出声。</summary>
        public bool Play(string id, SfxBus bus = SfxBus.Ui, float delay = 0)
        {
            if (string.IsNullOrEmpty(id)) return false;
            float at = Time.unscaledTime + Mathf.Max(0, delay);
            if (!PassThrottle(id, at))
            {
                Throttled++;
                return false;
            }
            Requested.Add(id);
            if (Requested.Count > LogLimit) Requested.RemoveRange(0, Requested.Count - LogLimit);

            var clips = AudioDatabase.Variants(id);
            if (clips.Length == 0) return true;
            var clip = clips[Random.Range(0, clips.Length)];
            bool pitched = !id.StartsWith("R01") && !id.StartsWith("R02");
            float pitch = pitched ? 1 + Random.Range(-PitchJitter, PitchJitter) : 1;

            int slot = FreeSlot(at);
            var src = _pool[slot];
            src.Stop();
            src.clip = clip;
            src.pitch = pitch;
            src.volume = GetVolume(Volume.Sfx);
            if (delay > 0) src.PlayDelayed(delay);
            else src.Play();
            _busyUntil[slot] = at + clip.length / pitch;
            Played++;
            return true;
        }

        public void ClearLog()
        {
            Requested.Clear();
            Played = 0;
            Throttled = 0;
            _recent.Clear();
        }

        bool PassThrottle(string id, float at)
        {
            if (!_recent.TryGetValue(id, out var times)) _recent[id] = times = new List<float>();
            times.RemoveAll(t => t <= at - ThrottleWindow);
            if (times.Count >= ThrottleMax) return false;
            times.Add(at);
            return true;
        }

        /// <summary>空闲的声源；都在响时抢最早会结束的那个。</summary>
        int FreeSlot(float at)
        {
            int best = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_busyUntil[i] <= at && !_pool[i].isPlaying) return i;
                if (_busyUntil[i] < _busyUntil[best]) best = i;
            }
            return best;
        }
    }
}
