using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deeptide.Core;
using Deeptide.Core.Content;
using Deeptide.Core.Data;
using Deeptide.Core.Enemies;
using Deeptide.Core.Relics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Deeptide.View.Battle
{
    /// <summary>一场战斗的配置：测试牌组名 / 遭遇 ID / 遗物 / 药水 / 种子（启动面板和测试用）。</summary>
    public sealed class BattleLaunch
    {
        /// <summary>TestDecks 的牌组名（starter / A / B / C / D）；空 = 用场景 Inspector 里的 Deck。</summary>
        public string DeckName = "";
        /// <summary>Data/encounters.csv 的遭遇 ID；空 = 用场景 Inspector 里的 Enemies。</summary>
        public string Encounter = "";
        public List<string> Relics = new List<string>();
        public List<string> Potions = new List<string>();
        public long Seed = 42;

        public BattleLaunch Clone() => new BattleLaunch
        {
            DeckName = DeckName, Encounter = Encounter, Relics = Relics.ToList(), Potions = Potions.ToList(), Seed = Seed,
        };
    }

    /// <summary>
    /// 灰盒战斗场景的入口：载入 CSV、按配置开一场战斗。
    /// 默认配置在 Inspector 里改；运行时点顶栏「选择对局」可以换成任意测试牌组 / 遭遇 / 遗物 / 药水（策划/09 阶段 3 验收「所有内容都能在测试场景里单独调出来」）。
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [Tooltip("Data/ 下的 CSV（cards、enemies、relics、potions、encounters、events），按文件名匹配")]
        public TextAsset[] DataFiles;

        public long Seed = 42;
        [Tooltip("测试牌组名（starter / A / B / C / D，见 Core/Content/TestDecks）；留空用下面的 Deck")]
        public string DeckName = "";
        public string[] Deck = { "B01", "B01", "B01", "B01", "B01", "B02", "B02", "B02", "B02", "B02" };
        [Tooltip("遭遇 ID（Data/encounters.csv）；留空用下面的 Enemies")]
        public string Encounter = "";
        public string[] Enemies = { "acolyte" };
        public string[] Relics = { RelicIds.InvestigatorNotes };
        public string[] Potions = { };

        public BattleScreen Screen { get; private set; }

        /// <summary>当前这场的配置（启动面板以它为初始选择）。</summary>
        public BattleLaunch Current => new BattleLaunch
        {
            DeckName = DeckName, Encounter = Encounter, Relics = Relics.ToList(), Potions = Potions.ToList(), Seed = Seed,
        };

        void Start()
        {
            EnsureEventSystem();
            LoadData();
            Begin();
        }

        /// <summary>同样的配置换下一个种子重开。</summary>
        public void Restart()
        {
            Seed++;
            Begin();
        }

        /// <summary>按新的配置开一场。</summary>
        public void Launch(BattleLaunch launch)
        {
            DeckName = launch.DeckName ?? "";
            Encounter = launch.Encounter ?? "";
            Relics = launch.Relics.ToArray();
            Potions = launch.Potions.ToArray();
            Seed = launch.Seed;
            Begin();
        }

        public void OpenLauncher()
        {
            if (Screen != null) LauncherPanel.Open((RectTransform)Screen.transform, Current, Launch);
        }

        void Begin()
        {
            if (Screen != null) Destroy(Screen.gameObject);
            Screen = BattleScreen.Create(Camera.main, BuildSetup(), Restart, OpenLauncher);
        }

        BattleSetup BuildSetup() => new BattleSetup
        {
            Seed = (ulong)Seed,
            Deck = string.IsNullOrEmpty(DeckName) ? Deck.ToList() : TestDecks.Get(DeckName),
            Enemies = string.IsNullOrEmpty(Encounter) ? Enemies.ToList() : EncounterLibrary.Get(Encounter).Enemies.ToList(),
            Relics = Relics.ToList(),
            Potions = Potions.ToList(),
        };

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
