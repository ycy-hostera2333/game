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
    /// 选项对应一张牌时（ChoiceOption.CardUid / CardId）画成卡牌（和手牌同一套 CardFace，策划/10 §4），否则画成文字按钮。
    /// </summary>
    public sealed class ChoicePanel : MonoBehaviour
    {
        /// <summary>选择面板里的卡最大比手牌大一号（手牌 240 × 336 的 1.25 倍）；选项多时按屏幕宽度缩小，保证不重叠。</summary>
        const float MaxCardWidth = 300, RowWidth = 1760, CardGap = 16;

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
            int cards = 0;
            foreach (var option in choice.Options)
                if (CardOf(option, engine) != null) cards++;
            float w = Mathf.Min(MaxCardWidth, (RowWidth - (cards - 1) * CardGap) / Mathf.Max(1, cards));
            var size = new Vector2(w, w * CardFace.H / CardFace.W);
            for (int i = 0; i < n; i++)
            {
                int index = i;
                var option = choice.Options[i];
                var card = CardOf(option, engine);
                Action click = () => Pick(index);
                if (card != null)
                {
                    CardOptions++;
                    float x = (i - (n - 1) / 2f) * (w + CardGap);
                    CardOption(card, engine, new Vector2(x, 20), size, click);
                    if (option.Label != card.Name)
                    {
                        var label = Ui.Text($"Label {i}", _root, option.Label, 22, Color.white);
                        label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(x, 20 - size.y / 2 - 30), new Vector2(w, 50));
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

        /// <summary>一张可点的卡：透明的点击区 + 正式卡面。战斗中的牌按当前生效的那一面画，发现出来的新牌（不在战斗里）画清醒面。</summary>
        void CardOption(Card card, BattleEngine engine, Vector2 pos, Vector2 size, Action click)
        {
            var hit = Ui.Box($"Card {card.Id}", _root, new Color(0, 0, 0, 0), true);
            hit.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, size);
            var btn = hit.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.onClick.AddListener(() => click());

            bool inBattle = card.Uid != 0;
            bool mad = inBattle && engine.UsesMadFace(card);
            var face = CardFace.Create(hit.transform, size.x / CardFace.W);
            face.Apply(CardView.Describe(card, engine, mad, engine.Player.Energy, inBattle));
        }
    }
}
