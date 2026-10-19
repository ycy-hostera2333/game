using Deeptide.Core;
using Deeptide.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 一张牌的灰盒卡面：费用、名字、类型、文本。文本走 Card.Describe(engine)，和结算用同一条预览管线（策划/08 §9「动态数值」）。
    /// 输入（悬停、拖拽）转交给 HandView 处理，卡面自己不做规则判断。
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static readonly Vector2 Size = new Vector2(180, 250);

        public int Uid { get; private set; }
        public string CardId { get; private set; }
        public RectTransform Rect { get; private set; }
        public CanvasGroup Group { get; private set; }

        HandView _hand;
        Image _frame, _body;
        TextMeshProUGUI _cost, _name, _type, _text;

        public static CardView Create(Transform parent, HandView hand, int uid, string cardId)
        {
            var rt = Ui.Rect($"Card {cardId} #{uid}", parent);
            // HandView 的坐标原点在底边中点
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.sizeDelta = Size;
            var view = rt.gameObject.AddComponent<CardView>();
            view.Build(hand, uid, cardId);
            return view;
        }

        void Build(HandView hand, int uid, string cardId)
        {
            _hand = hand;
            Uid = uid;
            CardId = cardId;
            Rect = (RectTransform)transform;
            Group = gameObject.AddComponent<CanvasGroup>();

            // 外框接收射线，整张牌都能悬停和拖拽
            _frame = gameObject.AddComponent<Image>();
            _frame.color = Palette.PanelLight;
            _body = Ui.Box("Body", transform, Palette.Skill);
            _body.rectTransform.Fill(5);

            var art = Ui.Box("Art", _body.transform, new Color(0, 0, 0, 0.25f));
            art.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(150, 80));

            _name = Ui.Text("Name", _body.transform, "", 24, Palette.Text);
            _name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(10, -28), new Vector2(130, 34));

            // 纵向：名字 11–45、插图 52–132、类型 136–156、正文 162–232（从卡面顶边量，卡面内高 240）
            _type = Ui.Text("Type", _body.transform, "", 16, Palette.TextDim);
            _type.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -146), new Vector2(150, 20));

            _text = Ui.Text("Text", _body.transform, "", 19, Palette.Text, TextAlignmentOptions.Top);
            _text.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 43), new Vector2(156, 70));

            var orb = Ui.Box("Cost", transform, Palette.Energy);
            orb.rectTransform.Place(new Vector2(0, 1), new Vector2(18, -18), new Vector2(42, 42));
            _cost = Ui.Text("Value", orb.transform, "", 28, Palette.Background);
            _cost.fontStyle = FontStyles.Bold;
            _cost.rectTransform.Fill();
        }

        /// <summary>按内核当前状态刷新卡面。energy 是表现层此刻显示的能量（事件回放中途可能还没追上内核）。</summary>
        public void Refresh(BattleEngine engine, int energy, bool canAct)
        {
            var card = engine.FindCard(Uid);
            if (card == null) return;

            bool mad = engine.UsesMadFace(card);
            _name.text = card.Name;
            _type.text = TypeName(card.Def.Type) + (card.Def.Dual ? (mad ? " · 癫狂面" : " · 理智面") : "");
            _text.text = card.Describe(engine);
            _body.color = mad ? Palette.AccentDark : TypeColor(card.Def.Type);

            int cost = engine.CostOf(card);
            _cost.text = card.Def.XCost ? "X" : card.Playable ? cost.ToString() : "-";
            bool affordable = card.Playable && (card.Def.XCost || cost <= energy);
            _cost.color = affordable ? Palette.Background : Palette.Bad;
            _frame.color = canAct && affordable ? Palette.Accent : Palette.PanelLight;
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

        static Color TypeColor(CardType t)
        {
            switch (t)
            {
                case CardType.Attack: return Palette.Attack;
                case CardType.Skill: return Palette.Skill;
                case CardType.Power: return Palette.Power;
                case CardType.Curse: return Palette.Curse;
                default: return Palette.Status;
            }
        }

        public void OnPointerEnter(PointerEventData e) => _hand.Hover(this, true);
        public void OnPointerExit(PointerEventData e) => _hand.Hover(this, false);
        public void OnBeginDrag(PointerEventData e) => _hand.BeginDrag(this, e);
        public void OnDrag(PointerEventData e) => _hand.Drag(this, e);
        public void OnEndDrag(PointerEventData e) => _hand.EndDrag(this, e);
    }
}
