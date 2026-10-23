using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;
using Deeptide.Core.Run;
using Deeptide.View.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Run
{
    /// <summary>
    /// 跑局的各个面板（策划/10 §8）：每个面板把 RunEngine.Options() 原样画出来，点了就 screen.Pick(下标)，
    /// 选项按钮 / 卡牌一律叫「Option 下标」。规则判断（能不能买、为什么不能选）全在内核的 RunOption 里，这里只负责摆放。
    /// 商店、篝火、事件的正式插图是 P2（10 §12.1），现在用节点图标和战斗背景代替。
    /// </summary>
    public static class RunPanels
    {
        public static RectTransform Build(RectTransform layer, RunScreen screen)
        {
            switch (screen.Run.State.Phase)
            {
                case RunPhase.Reward: return Reward(layer, screen);
                case RunPhase.Select: return Select(layer, screen);
                case RunPhase.Shop: return Shop(layer, screen);
                case RunPhase.Campfire: return Campfire(layer, screen);
                case RunPhase.Event: return Event(layer, screen);
                case RunPhase.Victory:
                case RunPhase.Defeat: return End(layer, screen);
                default: return null;
            }
        }

        static RectTransform Root(RectTransform layer, string name) => Ui.Rect(name, layer).Fill();

        /// <summary>点一个选项：不可选的弹出原因（内核写的），可选的交给跑局。</summary>
        static Action Pick(RunScreen screen, int index, RunOption option) => () =>
        {
            if (!option.Enabled) screen.Toast(option.Reason ?? "现在不能选这一项");
            else screen.Pick(index);
        };

        /// <summary>整屏的纸面底（商店、事件），挡住下面的地图。</summary>
        static void Paper(RectTransform root)
        {
            Ui.Box("Paper", root, Palette.Paper, true).rectTransform.Fill();
            var hatch = Ui.Rect("Hatch", root).Fill().gameObject.AddComponent<RawImage>();
            hatch.texture = Shapes.HatchTexture;
            hatch.uvRect = new Rect(0, 0, 1920 / 64f, 1080 / 64f);
            hatch.color = new Color(Palette.PaperShade.r, Palette.PaperShade.g, Palette.PaperShade.b, 0.35f);
            hatch.raycastTarget = false;
        }

        /// <summary>左边一个方形图标（遗物、药水、金币……），有正式图标就用，没有用汉字。</summary>
        static void Icon(Transform parent, string spriteId, string glyph, Color color, Vector2 anchor, Vector2 pos, float size)
        {
            var icon = Ui.Box("Icon", parent, color);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = anchor;
            icon.rectTransform.anchoredPosition = pos;
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            icon.raycastTarget = false;
            if (spriteId != null && ArtSprites.Apply(icon, spriteId, color)) return;
            icon.color = new Color(0, 0, 0, 0);
            var t = Ui.Text("Glyph", icon.transform, glyph, size * 0.62f, color);
            t.font = Fonts.Serif;
            t.fontStyle = FontStyles.Bold;
            t.rectTransform.Fill();
        }

        // ------------------------------------------------------------------ 奖励

        /// <summary>战利品（10 §8「纸面卷轴」）：一项一行，点了领取；卡牌那一行打开三选一。最下面「继续前进」。</summary>
        static RectTransform Reward(RectTransform layer, RunScreen screen)
        {
            var root = Root(layer, "Reward");
            RunUi.Dim(root, 0.55f);
            var options = screen.Run.Options();
            int rows = options.Count - 1;
            float h = 190 + Mathf.Max(1, rows) * 96;
            var panel = RunUi.Panel(root, "Scroll", new Vector2(0, 30), new Vector2(760, h)).transform;
            RunUi.Title(panel, "战利品", new Vector2(0, h / 2 - 62), new Vector2(600, 70), 52);
            if (rows == 0) RunUi.Label(panel, "都拿完了。", new Vector2(0, h / 2 - 150), new Vector2(600, 40), 26, Palette.InkMid);
            for (int i = 0; i < rows; i++) RewardRow(panel, screen, i, options[i], h / 2 - 150 - i * 96);
            int last = options.Count - 1;
            RunUi.Button(root, $"Option {last}", "继续前进", new Vector2(0, 30 - h / 2 - 70), new Vector2(280, 76), Pick(screen, last, options[last]));
            return root;
        }

        static void RewardRow(Transform parent, RunScreen screen, int index, RunOption o, float y)
        {
            string tag = o.Tag ?? "", text = o.Label, glyph = "?", sprite = null, tip = null;
            var color = Palette.Ink;
            if (tag == "gold")
            {
                glyph = "◎";
                color = Palette.Gold;
            }
            else if (tag.StartsWith("relic:", StringComparison.Ordinal))
            {
                var relic = RelicLibrary.Create(tag.Substring(6));
                text = $"遗物 · {relic.Name}";
                sprite = "relic_" + relic.Id;
                glyph = relic.Name.Substring(0, 1);
                tip = $"{relic.Name}\n{relic.Description}";
            }
            else if (tag.StartsWith("potion:", StringComparison.Ordinal))
            {
                var potion = PotionLibrary.Create(tag.Substring(7));
                text = $"药水 · {potion.Name}";
                glyph = potion.Name.Substring(0, 1);
                tip = $"{potion.Name}\n{potion.Description}";
            }
            else if (tag == "cards")
            {
                text = "选 1 张牌加入牌组";
                glyph = "牌";
            }
            var btn = RunUi.Button(parent, $"Option {index}", text, new Vector2(0, y), new Vector2(640, 80), Pick(screen, index, o), o.Enabled, o.Reason);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(100, 4, 20, 4);
            Icon(btn.transform, sprite, glyph, color, new Vector2(0, 0.5f), new Vector2(52, 0), 54);
            if (tip != null) TooltipTrigger.Add((Image)btn.targetGraphic, () => tip);
        }

        // ------------------------------------------------------------------ 选择

        static RectTransform Select(RectTransform layer, RunScreen screen)
        {
            var sel = screen.Run.State.Select;
            var options = screen.Run.Options();
            var root = Root(layer, "Select");
            RunUi.Dim(root, 0.84f);
            switch (sel.Purpose)
            {
                case SelectPurpose.AddCard:
                    CardChoice(root, screen, sel.Title, options);
                    break;
                case SelectPurpose.RemoveCard:
                case SelectPurpose.UpgradeCard:
                    DeckChoice(root, screen, sel, options);
                    break;
                default:
                    PotionChoice(root, screen, sel.Title, options);
                    break;
            }
            return root;
        }

        /// <summary>几张牌里选一张（奖励、事件）：一排大卡，双面牌悬停看癫狂面；下面「跳过」。</summary>
        static void CardChoice(RectTransform root, RunScreen screen, string title, List<RunOption> options)
        {
            RunUi.Title(root, title, new Vector2(0, 340), new Vector2(1400, 70), 46, Palette.Pale);
            var cards = Enumerable.Range(0, options.Count).Where(i => options[i].CardId != null).ToList();
            float w = Mathf.Min(300, (1700 - (cards.Count - 1) * 40f) / Mathf.Max(1, cards.Count));
            for (int k = 0; k < cards.Count; k++)
            {
                int i = cards[k];
                float x = (k - (cards.Count - 1) / 2f) * (w + 40);
                var tile = CardTile.Create(root, options[i].CardId, new Vector2(x, 20), w, Pick(screen, i, options[i]), options[i].Enabled);
                tile.name = $"Option {i}";
            }
            for (int i = 0; i < options.Count; i++)
                if (options[i].CardId == null)
                    RunUi.Button(root, $"Option {i}", options[i].Label, new Vector2(0, -330), new Vector2(240, 70), Pick(screen, i, options[i]));
        }

        /// <summary>从牌组里选一张（删卡、锻造）：整副牌的网格；锻造时悬停在旁边显示升级后的样子。</summary>
        static void DeckChoice(RectTransform root, RunScreen screen, SelectState sel, List<RunOption> options)
        {
            bool upgrade = sel.Purpose == SelectPurpose.UpgradeCard;
            RunUi.Title(root, sel.Title, new Vector2(0, 400), new Vector2(1400, 70), 46, Palette.Pale);
            var cards = new List<GridCard>();
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                if (o.CardId == null)
                {
                    RunUi.Button(root, $"Option {i}", o.Label, new Vector2(800, 400), new Vector2(180, 64), Pick(screen, i, o));
                    continue;
                }
                cards.Add(new GridCard { Id = o.CardId, Name = $"Option {i}", Click = Pick(screen, i, o), Alt = upgrade ? o.CardId + "+" : null });
            }
            CardGrid(root, new Vector2(0, -60), new Vector2(1640, 820), cards, upgrade ? "升级后" : null);
        }

        static void PotionChoice(RectTransform root, RunScreen screen, string title, List<RunOption> options)
        {
            float h = 160 + options.Count * 86;
            var panel = RunUi.Panel(root, "Box", Vector2.zero, new Vector2(700, h)).transform;
            RunUi.Title(panel, title, new Vector2(0, h / 2 - 60), new Vector2(640, 60), 32);
            for (int i = 0; i < options.Count; i++)
                RunUi.Button(panel, $"Option {i}", options[i].Label, new Vector2(0, h / 2 - 140 - i * 86), new Vector2(560, 72), Pick(screen, i, options[i]));
        }

        public struct GridCard
        {
            public string Id, Name, Alt;
            public Action Click;
        }

        /// <summary>可以卷动的卡牌网格（牌组、删卡、锻造），每行 7 张，按给定顺序排。</summary>
        public static RectTransform CardGrid(RectTransform parent, Vector2 center, Vector2 area, IList<GridCard> cards, string altLabel)
        {
            const int Columns = 7;
            const float W = 190, GapX = 34, GapY = 34;
            float h = W * CardFace.H / CardFace.W;

            var scrollRt = Ui.Rect("Grid", parent).Place(new Vector2(0.5f, 0.5f), center, area);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 50;
            var viewport = Ui.Box("Viewport", scrollRt, new Color(0, 0, 0, 0), true);
            viewport.rectTransform.Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport.rectTransform;

            int rows = (cards.Count + Columns - 1) / Columns;
            var content = Ui.Rect("Content", viewport.rectTransform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(area.x, Mathf.Max(area.y, GapY + rows * (h + GapY)));
            scroll.content = content;

            for (int i = 0; i < cards.Count; i++)
            {
                int r = i / Columns, c = i % Columns;
                float x = (c - (Columns - 1) / 2f) * (W + GapX);
                float y = -(GapY + r * (h + GapY) + h / 2);
                var card = cards[i];
                var tile = CardTile.Create(content, card.Id, Vector2.zero, W, card.Click, true, card.Alt, altLabel);
                var rt = (RectTransform)tile.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
                rt.anchoredPosition = new Vector2(x, y);
                tile.name = card.Name;
            }
            return scrollRt;
        }

        // ------------------------------------------------------------------ 商店

        /// <summary>
        /// 商店（策划/06 §5、10 §8）：左边商贩，上排 5 张牌，下排遗物和药水，右边删卡服务和「离开」。
        /// 价格用清明金，买不起的标血色；卖掉的位置留一个「已售出」。
        /// </summary>
        static RectTransform Shop(RectTransform layer, RunScreen screen)
        {
            var run = screen.Run;
            var shop = run.State.Shop;
            var options = run.Options();
            var root = Root(layer, "Shop");
            Paper(root);
            RunUi.Title(root, "商店", new Vector2(-700, 390), new Vector2(400, 80), 64);
            RunUi.Label(root, "雾里传来铃声，商贩掀开了车上的油布。", new Vector2(-700, 330), new Vector2(460, 40), 22, Palette.InkMid);
            Icon(root, "node_shop", "商", Palette.Ink, new Vector2(0.5f, 0.5f), new Vector2(-700, 90), 260);
            RunUi.Label(root, "雾中商贩", new Vector2(-700, -70), new Vector2(300, 44), 30).font = Fonts.Serif;
            RunUi.Label(root, $"删卡价格每用一次 +25", new Vector2(-700, -120), new Vector2(360, 36), 20, Palette.InkMid);

            int card = 0, item = 0;
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                string tag = o.Tag ?? "";
                var shopItem = i < shop.Items.Count ? shop.Items[i] : null;
                if (shopItem != null && shopItem.Kind == ShopItemKind.Card)
                    ShopCard(root, screen, i, o, shopItem, new Vector2(-300 + card++ * 250, 170));
                else if (shopItem != null)
                    ShopGood(root, screen, i, o, shopItem, new Vector2(-300 + item++ * 172, -250));
                else if (tag == "remove")
                {
                    string label = run.State.Shop.RemovalUsed ? "删卡服务\n<size=20>（这次已经用过）</size>" : $"删卡服务\n<color={Ui.Hex(Palette.Gold)}>◎ {run.RemovalPrice}</color>";
                    var btn = RunUi.Button(root, $"Option {i}", label, new Vector2(790, -250), new Vector2(220, 150), Pick(screen, i, o), o.Enabled, o.Reason);
                    TooltipTrigger.Add((Image)btn.targetGraphic, () => "从牌组里删掉 1 张牌（每次进店只能用一次）");
                }
                else if (tag == "leave")
                    RunUi.Button(root, $"Option {i}", "离开商店", new Vector2(790, -430), new Vector2(220, 76), Pick(screen, i, o));
            }
            return root;
        }

        static string Price(RunScreen screen, ShopItem it)
        {
            int price = screen.Run.PriceOf(it.Price);
            var color = screen.Run.State.Gold >= price ? Palette.Gold : Palette.Blood;
            return $"<color={Ui.Hex(color)}>◎ {price}</color>";
        }

        static void ShopCard(RectTransform root, RunScreen screen, int index, RunOption o, ShopItem it, Vector2 pos)
        {
            const float W = 210;
            float h = W * CardFace.H / CardFace.W;
            if (it.Sold)
            {
                RunUi.Label(root, "已售出", pos, new Vector2(W, 50), 28, Palette.InkMid).name = $"Option {index}";
                return;
            }
            var tile = CardTile.Create(root, it.Id, pos, W, Pick(screen, index, o), o.Enabled);
            tile.name = $"Option {index}";
            RunUi.Label(root, Price(screen, it), pos + new Vector2(0, -h / 2 - 26), new Vector2(W, 40), 28).fontStyle = FontStyles.Bold;
        }

        /// <summary>遗物 / 药水：方形牌匾，图标 + 名字，下面价格；悬停看说明。</summary>
        static void ShopGood(RectTransform root, RunScreen screen, int index, RunOption o, ShopItem it, Vector2 pos)
        {
            string name, desc, sprite = null;
            if (it.Kind == ShopItemKind.Relic)
            {
                var relic = RelicLibrary.Create(it.Id);
                name = relic.Name;
                desc = relic.Description;
                sprite = "relic_" + relic.Id;
            }
            else
            {
                var potion = PotionLibrary.Create(it.Id);
                name = potion.Name;
                desc = potion.Description;
            }
            if (it.Sold)
            {
                RunUi.Label(root, "已售出", pos, new Vector2(150, 50), 24, Palette.InkMid).name = $"Option {index}";
                return;
            }
            var btn = RunUi.Button(root, $"Option {index}", name, pos, new Vector2(150, 150), Pick(screen, index, o), true, null, 22);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Bottom;
            label.margin = new Vector4(8, 4, 8, 12);
            Icon(btn.transform, sprite, name.Substring(0, 1), it.Kind == ShopItemKind.Relic ? Palette.Ink : Palette.Teal,
                new Vector2(0.5f, 0.5f), new Vector2(0, 18), 70);
            string kind = it.Kind == ShopItemKind.Relic ? "遗物" : "药水";
            TooltipTrigger.Add((Image)btn.targetGraphic, () => $"{kind} · {name}\n{desc}" + (o.Enabled ? "" : $"\n（{o.Reason}）"));
            if (!o.Enabled) btn.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
            RunUi.Label(root, Price(screen, it), pos + new Vector2(0, -100), new Vector2(150, 40), 26).fontStyle = FontStyles.Bold;
        }

        // ------------------------------------------------------------------ 篝火

        /// <summary>篝火（策划/06 §6）：夜色里一盆火，下面一排选项。选项文字按「：」拆成标题和说明。</summary>
        static RectTransform Campfire(RectTransform layer, RunScreen screen)
        {
            var root = Root(layer, "Campfire");
            Ui.Box("Night", root, Palette.Abyss, true).rectTransform.Fill();
            var glow = Ui.Box("Glow", root, new Color(Palette.GoldLight.r, Palette.GoldLight.g, Palette.GoldLight.b, 0.12f));
            glow.sprite = Shapes.Circle;
            glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(640, 640));
            Icon(root, "node_rest", "火", Palette.GoldLight, new Vector2(0.5f, 0.5f), new Vector2(0, 150), 280);
            RunUi.Title(root, "篝火", new Vector2(0, 400), new Vector2(600, 80), 64, Palette.Pale);
            RunUi.Label(root, "火光照不到的地方，有什么东西在水里动。", new Vector2(0, -60), new Vector2(900, 40), 24,
                new Color(Palette.Pale.r, Palette.Pale.g, Palette.Pale.b, 0.7f));

            var options = screen.Run.Options();
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                float x = (i - (options.Count - 1) / 2f) * 370;
                RunUi.Button(root, $"Option {i}", TwoLines(o.Label), new Vector2(x, -240), new Vector2(330, 170), Pick(screen, i, o), o.Enabled, o.Reason, 36);
            }
            return root;
        }

        /// <summary>「休息：恢复 21 生命」→ 大字「休息」+ 小字说明（代价上色）。</summary>
        static string TwoLines(string label)
        {
            int k = label.IndexOf('：');
            if (k < 0) return label;
            return $"<b>{label.Substring(0, k)}</b>\n<size=60%>{RunUi.ColorCosts(label.Substring(k + 1))}</size>";
        }

        // ------------------------------------------------------------------ 事件

        /// <summary>事件（10 §8）：左边插图（事件插图是 P2，先用码头街道的背景截一块），右边纸页写正文和选项。</summary>
        static RectTransform Event(RectTransform layer, RunScreen screen)
        {
            var run = screen.Run;
            var def = EventLibrary.Get(run.State.Event.Id);
            var root = Root(layer, "Event");
            Paper(root);

            var frame = Ui.Box("Illustration", root, Palette.Ink);
            frame.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-450, 0), new Vector2(880, 620));
            var tex = ArtDatabase.Background("docks");
            var shader = Shader.Find("Deeptide/UI/Backdrop");
            var art = Ui.Rect("Art", frame.transform).Fill(6).gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            art.color = Palette.Paper;
            if (tex != null && shader != null)
            {
                var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
                mat.SetColor("_InkColor", Palette.InkMid);
                mat.SetColor("_MadColor", Palette.Teal);
                art.texture = tex;
                art.material = mat;
                // 画框 868 × 608（约 1.43:1），从 16:9 的背景中间截一块
                art.uvRect = new Rect(0.1f, 0.05f, 0.75f, 0.9f);
                art.gameObject.AddComponent<MaterialOwner>().Material = mat;
            }
            else
            {
                art.texture = Shapes.HatchTexture;
                art.uvRect = new Rect(0, 0, 868 / 36f, 608 / 36f);
                art.color = Palette.PaperShade;
            }

            var title = RunUi.Label(root, def.Name, new Vector2(500, 330), new Vector2(780, 80), 56, null, TextAlignmentOptions.Left);
            title.font = Fonts.Serif;
            title.fontStyle = FontStyles.Bold;
            RunUi.Label(root, run.Description, new Vector2(500, 200), new Vector2(780, 160), 28, Palette.Text, TextAlignmentOptions.TopLeft);

            var options = run.Options();
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                string text = RunUi.ColorCosts(o.Label) + (o.Enabled ? "" : $"<color={Ui.Hex(Palette.InkMid)}>（{o.Reason}）</color>");
                var btn = RunUi.Button(root, $"Option {i}", text, new Vector2(500, 40 - i * 100), new Vector2(780, 84), Pick(screen, i, o), o.Enabled, o.Reason, 26);
                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Left;
                label.margin = new Vector4(32, 4, 20, 4);
            }
            return root;
        }

        // ------------------------------------------------------------------ 结算

        /// <summary>Demo 结算（10 §8）：通关 / 倒下，叠一份本局统计；「回到标题」。</summary>
        static RectTransform End(RectTransform layer, RunScreen screen)
        {
            var run = screen.Run;
            var s = run.State;
            bool won = s.Phase == RunPhase.Victory;
            var root = Root(layer, "End");
            RunUi.Dim(root, 0.88f);
            RunUi.Title(root, won ? "通关" : "调查员倒下了", new Vector2(0, 230), new Vector2(1200, 130), 96, won ? Palette.GoldLight : Palette.BloodLight);
            RunUi.Label(root, run.Title, new Vector2(0, 120), new Vector2(1200, 50), 30, Palette.Pale);

            int elites = s.Stats.Battles.Count(b => b.Room == RoomType.Elite);
            var lines = new List<string>
            {
                $"到达第 {s.Floor} 层，打了 {s.Stats.Battles.Count} 场战斗（精英 {elites} 场）",
                $"牌组 {s.Deck.Count} 张 · 遗物 {s.Relics.Count} 件 · 一共获得 {s.Stats.GoldEarned} 金币",
                $"种子 {s.Seed}",
            };
            if (!won && !string.IsNullOrEmpty(s.Stats.DeathEncounter))
                lines.Insert(0, $"倒在：{EncounterLibrary.Get(s.Stats.DeathEncounter).Name}");
            RunUi.Label(root, string.Join("\n", lines), new Vector2(0, -10), new Vector2(1200, 180), 28,
                new Color(Palette.Pale.r, Palette.Pale.g, Palette.Pale.b, 0.85f));
            RunUi.Button(root, "Back", "回到标题", new Vector2(0, -230), new Vector2(300, 80), () => screen.ExitToTitle(true));
            return root;
        }
    }

    /// <summary>面板销毁时顺带销毁它自己建的材质。</summary>
    public sealed class MaterialOwner : MonoBehaviour
    {
        public Material Material;

        void OnDestroy()
        {
            if (Material != null) Destroy(Material);
        }
    }
}
