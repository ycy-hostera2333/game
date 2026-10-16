using System.Collections.Generic;
using Deeptide.Core.Cards;

namespace Deeptide.Core.Actions
{
    public sealed class DrawCardsAction : GameAction
    {
        readonly int _count;

        public DrawCardsAction(int count) { _count = count; }

        public override void Execute(BattleEngine e)
        {
            var drawn = new List<string>();
            for (int i = 0; i < _count; i++)
            {
                var card = e.DrawOne();
                if (card == null) break;
                drawn.Add(card.Name);
            }
            if (drawn.Count > 0)
                e.Log.Add(BattleEventType.CardsDrawn, $"抽 {drawn.Count} 张：{string.Join("、", drawn)}", amount: drawn.Count);
        }
    }

    public sealed class DiscardRandomAction : GameAction
    {
        readonly int _count;

        public DiscardRandomAction(int count) { _count = count; }

        public override void Execute(BattleEngine e)
        {
            var hand = e.Player.Hand;
            for (int i = 0; i < _count && hand.Count > 0; i++)
            {
                int idx = e.Rng.Misc.NextInt(hand.Count);
                var card = hand[idx];
                hand.RemoveAt(idx);
                e.Player.DiscardPile.Add(card);
                e.Log.Add(BattleEventType.CardDiscarded, $"随机弃掉 {card.Name}", target: card.Name);
            }
        }
    }

    /// <summary>把一张新牌放进指定牌堆（如敌人塞进来的疯语）。放进抽牌堆时插在随机位置。</summary>
    public sealed class AddCardToPileAction : GameAction
    {
        readonly string _cardId;
        readonly PileKind _pile;

        public AddCardToPileAction(string cardId, PileKind pile)
        {
            _cardId = cardId;
            _pile = pile;
        }

        public override void Execute(BattleEngine e)
        {
            var p = e.Player;
            var card = e.NewCard(_cardId);
            string where;
            switch (_pile)
            {
                case PileKind.Draw:
                    p.DrawPile.Insert(e.Rng.Misc.NextInt(p.DrawPile.Count + 1), card);
                    where = "抽牌堆";
                    break;
                case PileKind.Hand when p.Hand.Count < e.Config.HandLimit:
                    p.Hand.Add(card);
                    where = "手牌";
                    break;
                default:
                    p.DiscardPile.Add(card);
                    where = "弃牌堆";
                    break;
            }
            e.Log.Add(BattleEventType.CardAdded, $"1 张{card.Name}被放进你的{where}", target: card.Name);
        }
    }

    /// <summary>出牌第 ⑤ 步：能力牌移出战斗，「消耗」进消耗堆，其余进弃牌堆。</summary>
    public sealed class FinishCardAction : GameAction
    {
        readonly Card _card;

        public FinishCardAction(Card card) { _card = card; }

        public override void Execute(BattleEngine e)
        {
            var p = e.Player;
            if (_card.Def.Type == CardType.Power) return;
            if (_card.Def.Exhaust) p.ExhaustPile.Add(_card);
            else p.DiscardPile.Add(_card);
        }
    }
}
