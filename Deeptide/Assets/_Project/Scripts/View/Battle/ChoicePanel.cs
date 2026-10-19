using System;
using Deeptide.Core;
using Deeptide.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 战斗中的选择（策划/09 M1.1）：查阅资料弃牌、禁书残页三选一、心灵之锚二选一等。
    /// 选项对应一张牌时（ChoiceOption.CardUid / CardId）画成卡牌，否则画成文字按钮。
    /// 卡牌用的是简化卡面（名字、费用、类型、正文），等 CardView 有了展示模式再换成正式卡面。
    /// </summary>
    public sealed class ChoicePanel : MonoBehaviour
    {
        static readonly Vector2 CardSize = new Vector2(200, 280);

        RectTransform _root;
        Action<int> _choose;
        public bool IsOpen => gameObject.activeSelf;
        public int OptionCount { get; private set; }
        /// <summary>画成卡牌的选项数（测试用）。</summary>
        public int CardOptions { get; private set; }

        public static ChoicePanel Create(RectTransform parent)
        {
            var box = Ui.Box("Choice", parent, new Color(0, 0, 0, 0.85f), true);
            box.rectTransform.Fill();
            var panel = box.gameObject.AddComponent<ChoicePanel>();
            panel._root = box.rectTransform;
            box.gameObject.SetActive(false);
            return panel;
        }

        public void Show(PendingChoice choice, BattleEngine engine, Action<int> choose)
        {
            _choose = choose;
            foreach (Transform child in _root) Destroy(child.gameObject);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            var prompt = Ui.Text("Prompt", _root, choice.Prompt, 40, Color.white);
            prompt.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 270), new Vector2(1400, 60));

            int n = choice.Options.Count;
            OptionCount = n;
            CardOptions = 0;
            for (int i = 0; i < n; i++)
            {
                int index = i;
                var option = choice.Options[i];
                var card = CardOf(option, engine);
                Action click = () => Pick(index);
                if (card != null)
                {
                    CardOptions++;
                    float x = (i - (n - 1) / 2f) * Mathf.Min(240, 1600f / Mathf.Max(1, n - 1));
                    MiniCard(card, engine, new Vector2(x, 40), click);
                    if (option.Label != card.Name)
                    {
                        var label = Ui.Text($"Label {i}", _root, option.Label, 22, Color.white);
                        label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(x, -130), new Vector2(230, 60));
                    }
                }
                else
                {
                    var btn = Ui.TextButton($"Option {i}", _root, option.Label, Palette.PanelLight, click);
                    btn.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 300, 40), new Vector2(280, 90));
                }
            }
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>选第 index 项（和点按钮一样：先关面板再把选择交给内核）。</summary>
        public void Pick(int index)
        {
            if (!IsOpen) return;
            Hide();
            _choose?.Invoke(index);
        }

        static Card CardOf(ChoiceOption option, BattleEngine engine)
        {
            if (option.CardUid != 0) return engine.FindCard(option.CardUid);
            if (string.IsNullOrEmpty(option.CardId)) return null;
            return CardLibrary.Create(option.CardId.TrimEnd('+'), 0);
        }

        void MiniCard(Card card, BattleEngine engine, Vector2 pos, Action click)
        {
            var frame = Ui.Box($"Card {card.Id}", _root, Palette.PanelLight, true);
            frame.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, CardSize);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(() => click());

            var body = Ui.Box("Body", frame.transform, Palette.Panel);
            body.rectTransform.Fill(5);
            var name = Ui.Text("Name", body.transform, card.Name, 24, Palette.Text);
            name.fontStyle = FontStyles.Bold;
            name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(12, -28), new Vector2(150, 34));

            bool inBattle = card.Uid != 0;
            bool mad = inBattle && engine.UsesMadFace(card);
            string type = TypeName(card.Def.Type) + (card.Def.Dual ? (mad ? " · 癫狂面" : " · 清醒面") : "");
            Ui.Text("Type", body.transform, type, 17, Palette.TextDim).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(170, 24));
            var text = Ui.Text("Text", body.transform, inBattle ? card.Describe(engine) : card.Describe(false), 19, Palette.Text, TextAlignmentOptions.Top);
            text.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 85), new Vector2(176, 150));

            var orb = Ui.Box("Cost", frame.transform, Palette.Energy);
            orb.rectTransform.Place(new Vector2(0, 1), new Vector2(20, -20), new Vector2(42, 42));
            string cost = card.Def.XCost ? "X" : card.Playable ? (inBattle ? engine.CostOf(card) : card.Cost).ToString() : "-";
            var c = Ui.Text("Value", orb.transform, cost, 26, Palette.Background);
            c.fontStyle = FontStyles.Bold;
            c.rectTransform.Fill();
        }

        static string TypeName(CardType t)
        {
            switch (t)
            {
                case CardType.Attack: return "攻击";
                case CardType.Skill: return "技能";
                case CardType.Power: return "能力";
                case CardType.Curse: return "诅咒";
                default: return "状态";
            }
        }
    }
}
