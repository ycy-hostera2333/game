using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Deeptide.Core.Cards;
using Deeptide.Core.Content;
using Deeptide.Core.Enemies;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 「选择对局」面板：测试牌组 × 遭遇 × 遗物 × 药水 × 种子，点「开始」换一场。
    /// 只用于灰盒测试场景（策划/09 阶段 3 验收「所有内容都能在测试场景里单独调出来」），正式流程里的战斗由跑局决定。
    /// </summary>
    public sealed class LauncherPanel : MonoBehaviour
    {
        public const int MaxPotions = 3;
        static readonly string[] DeckNames = { "starter", "A", "B", "C", "D" };
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        /// <summary>当前打开的面板（测试用）；没打开时为 null。</summary>
        public static LauncherPanel Current { get; private set; }

        BattleLaunch _launch;
        Action<BattleLaunch> _onStart;
        TextMeshProUGUI _seedText, _hint;
        readonly List<(Outline Mark, Func<bool> On)> _toggles = new List<(Outline, Func<bool>)>();

        public BattleLaunch Selection => _launch;

        public static LauncherPanel Open(RectTransform root, BattleLaunch current, Action<BattleLaunch> onStart)
        {
            if (Current != null) Destroy(Current.gameObject);
            var box = Ui.Box("Launcher", root, Palette.Background, true);
            box.rectTransform.Fill();
            var panel = box.gameObject.AddComponent<LauncherPanel>();
            panel._launch = current.Clone();
            panel._onStart = onStart;
            panel.Build();
            Current = panel;
            return panel;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ------------------------------------------------------------------ 操作（按钮和测试共用）

        public void PickDeck(string name)
        {
            _launch.DeckName = name;
            Refresh();
        }

        public void PickEncounter(string id)
        {
            _launch.Encounter = id;
            Refresh();
        }

        public void ToggleRelic(string id)
        {
            if (!_launch.Relics.Remove(id)) _launch.Relics.Add(id);
            Refresh();
        }

        public void TogglePotion(string id)
        {
            if (_launch.Potions.Remove(id)) { Refresh(); return; }
            if (_launch.Potions.Count >= MaxPotions)
            {
                _hint.text = $"药水栏只有 {MaxPotions} 格";
                return;
            }
            _launch.Potions.Add(id);
            Refresh();
        }

        public void SetSeed(long seed)
        {
            _launch.Seed = Math.Max(0, seed);
            Refresh();
        }

        public void Begin()
        {
            var launch = _launch.Clone();
            Destroy(gameObject);
            _onStart?.Invoke(launch);
        }

        public void Cancel() => Destroy(gameObject);

        // ------------------------------------------------------------------ 搭建

        void Build()
        {
            var root = (RectTransform)transform;
            Label(root, "选择对局", new Vector2(-460, 470), 44, new Vector2(800, 60), TextAlignmentOptions.Left);
            _hint = Label(root, "", new Vector2(300, 470), 24, new Vector2(800, 40), TextAlignmentOptions.Right);
            _hint.color = Palette.Bad;

            // 左栏：牌组、遭遇
            Label(root, "牌组", new Vector2(-860, 390), 28, new Vector2(120, 40), TextAlignmentOptions.Left);
            var decks = new List<string>(DeckNames) { "" };
            for (int i = 0; i < decks.Count; i++)
            {
                string name = decks[i];
                var btn = Toggle(root, name == "" ? "场景默认" : TestDecks.Describe(name), new Vector2(-750 + i * 150, 340), new Vector2(140, 50),
                    () => _launch.DeckName == name, () => PickDeck(name));
                if (name != "") TooltipTrigger.Add(btn, () => DeckTooltip(name));
            }

            Label(root, "遭遇", new Vector2(-860, 270), 28, new Vector2(120, 40), TextAlignmentOptions.Left);
            float y = 220;
            foreach (var pool in new[] { EncounterPool.Easy, EncounterPool.Hard, EncounterPool.Elite, EncounterPool.Boss })
            {
                Label(root, PoolName(pool), new Vector2(-860, y), 22, new Vector2(120, 40), TextAlignmentOptions.Left).color = Palette.TextDim;
                var list = EncounterLibrary.InPool(pool).ToList();
                if (pool == EncounterPool.Boss) list.Add(null);
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0 && i % 4 == 0) y -= 58;
                    var enc = list[i];
                    string id = enc?.Id ?? "";
                    var btn = Toggle(root, enc?.Name ?? "场景默认", new Vector2(-690 + (i % 4) * 205, y), new Vector2(195, 50),
                        () => _launch.Encounter == id, () => PickEncounter(id));
                    if (enc != null) TooltipTrigger.Add(btn, () => string.Join("、", enc.Enemies.Select(e => EnemyLibrary.Get(e).Name)));
                }
                y -= 70;
            }

            // 右栏：遗物、药水、种子
            Label(root, "遗物", new Vector2(160, 390), 28, new Vector2(300, 40), TextAlignmentOptions.Left);
            var relics = RelicLibrary.All;
            for (int i = 0; i < relics.Count; i++)
            {
                var def = relics[i];
                var btn = Toggle(root, def.Name, new Vector2(230 + (i % 4) * 185, 340 - (i / 4) * 56), new Vector2(175, 48),
                    () => _launch.Relics.Contains(def.Id), () => ToggleRelic(def.Id), 20);
                TooltipTrigger.Add(btn, () => $"{def.Name}\n{Fill(def.Text, def.Vars)}");
            }

            float py = 340 - ((relics.Count + 3) / 4) * 56 - 30;
            Label(root, $"药水（最多 {MaxPotions} 瓶）", new Vector2(160, py), 28, new Vector2(400, 40), TextAlignmentOptions.Left);
            var potions = PotionLibrary.All;
            for (int i = 0; i < potions.Count; i++)
            {
                var def = potions[i];
                var btn = Toggle(root, def.Name, new Vector2(230 + (i % 4) * 185, py - 50 - (i / 4) * 56), new Vector2(175, 48),
                    () => _launch.Potions.Contains(def.Id), () => TogglePotion(def.Id), 20);
                TooltipTrigger.Add(btn, () => $"{def.Name}\n{Fill(def.Text, def.Vars)}");
            }

            float sy = py - 50 - ((potions.Count + 3) / 4) * 56 - 30;
            _seedText = Label(root, "", new Vector2(250, sy), 28, new Vector2(220, 50), TextAlignmentOptions.Left);
            Button(root, "−", new Vector2(420, sy), new Vector2(60, 50), () => SetSeed(_launch.Seed - 1));
            Button(root, "+", new Vector2(490, sy), new Vector2(60, 50), () => SetSeed(_launch.Seed + 1));
            Button(root, "随机", new Vector2(590, sy), new Vector2(110, 50), () => SetSeed(UnityEngine.Random.Range(1, 100000)));

            Button(root, "开始", new Vector2(620, -440), new Vector2(200, 70), Begin);
            Button(root, "取消", new Vector2(830, -440), new Vector2(160, 70), Cancel);
            Refresh();
        }

        void Refresh()
        {
            foreach (var (mark, on) in _toggles) mark.enabled = on();
            _seedText.text = $"种子 {_launch.Seed}";
            _hint.text = "";
        }

        Image Toggle(Transform parent, string label, Vector2 pos, Vector2 size, Func<bool> on, Action click, float fontSize = 22)
        {
            var btn = Button(parent, label, pos, size, click, fontSize);
            var img = (Image)btn.targetGraphic;
            _toggles.Add((Ui.Border(img, Palette.Accent, 4), on));
            return img;
        }

        static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Action click, float fontSize = 26)
        {
            var btn = Ui.TextButton(label, parent, label, Palette.PanelLight, click);
            btn.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), pos, size);
            var text = btn.GetComponentInChildren<TextMeshProUGUI>();
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = 14;
            text.margin = new Vector4(6, 0, 6, 0);
            return btn;
        }

        static TextMeshProUGUI Label(Transform parent, string text, Vector2 pos, float size, Vector2 box, TextAlignmentOptions align)
        {
            var t = Ui.Text("Label", parent, text, size, Palette.Text, align);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, box);
            return t;
        }

        static string PoolName(EncounterPool pool)
        {
            switch (pool)
            {
                case EncounterPool.Easy: return "简单";
                case EncounterPool.Hard: return "困难";
                case EncounterPool.Elite: return "精英";
                default: return "Boss";
            }
        }

        static string DeckTooltip(string name)
        {
            var groups = TestDecks.Get(name).GroupBy(id => id).Select(g => $"{CardLibrary.Get(g.Key.TrimEnd('+')).Name} ×{g.Count()}");
            return $"{TestDecks.Describe(name)}\n{string.Join("、", groups)}";
        }

        static string Fill(string text, Dictionary<string, int> vars) =>
            Placeholder.Replace(text ?? "", m => vars.TryGetValue(m.Groups[1].Value, out var v) ? v.ToString() : m.Value);
    }
}
