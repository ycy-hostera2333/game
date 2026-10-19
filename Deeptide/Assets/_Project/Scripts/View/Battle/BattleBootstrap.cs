using System.IO;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 灰盒战斗场景的入口：载入 CSV、按 Inspector 里的牌组和敌人开一场战斗。
    /// 阶段 2 第一步只用打击 + 防御（策划/09 §2），牌组和敌人都可以在 Inspector 里改。
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [Tooltip("Data/ 下的 CSV（cards、enemies、relics、potions、encounters、events），按文件名匹配")]
        public TextAsset[] DataFiles;

        public long Seed = 42;
        public string[] Deck = { "B01", "B01", "B01", "B01", "B01", "B02", "B02", "B02", "B02", "B02" };
        public string[] Enemies = { "acolyte" };

        public BattleScreen Screen { get; private set; }

        void Start()
        {
            EnsureEventSystem();
            LoadData();
            Begin(Seed);
        }

        public void Restart()
        {
            Seed++;
            Begin(Seed);
        }

        void Begin(long seed)
        {
            if (Screen != null) Destroy(Screen.gameObject);
            var setup = new BattleSetup
            {
                Seed = (ulong)seed,
                Deck = Deck.ToList(),
                Enemies = Enemies.ToList(),
            };
            Screen = BattleScreen.Create(Camera.main, setup, Restart);
        }

        void LoadData()
        {
            if (GameData.IsLoaded) return;
            var source = DataSource.From(fileName =>
            {
                string key = Path.GetFileNameWithoutExtension(fileName);
                var asset = DataFiles.FirstOrDefault(a => a != null && a.name == key);
                if (asset == null) throw new FileNotFoundException($"BattleBootstrap 缺少数据文件 {fileName}");
                return asset.text;
            });
            try
            {
                GameData.Load(source);
            }
            catch (DataValidationException ex)
            {
                foreach (var error in ex.Errors) Debug.LogError(error);
                throw;
            }
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
