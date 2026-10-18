using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Cards;

namespace Deeptide.Core.Actions
{
    public sealed class DrawCardsAction : GameAction
    {
        readonly int _count;

        public DrawCardsAction(int count) { _count = count; }

        public override void Execute(BattleEngine e) => DrawAndLog(e, _count);

        internal static void DrawAndLog(BattleEngine e, int count)
        {
            var drawn = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var card = e.DrawOne();
                if (card == null) break;
                drawn.Add(card.Name);
            }
            if (drawn.Count > 0)
                e.Log.Add(BattleEventType.CardsDrawn, $"抽 {drawn.Count} 张：{string.Join("、", drawn)}", amount: drawn.Count);
        }
    }

    /// <summary>
    /// 回合开始的抽牌：每回合抽牌数 − 束缚 + 监听器加成（第三只眼、守夜人提灯）。
    /// 数量在执行时才计算，所以同一回合开始时被侵蚀推入癫狂，第三只眼也能生效。
    /// </summary>
    public sealed class TurnDrawAction : GameAction
    {
        readonly int _extra;

        public TurnDrawAction(int extra) { _extra = extra; }

        public override void Execute(BattleEngine e)
        {
            int bound = e.Player.Statuses.Get(StatusId.Bound);
            if (bound > 0) e.SetStatus(e.Player, StatusId.Bound, 0);
            int count = e.Config.DrawPerTurn + _extra - bound;
            foreach (var l in e.Listeners) count += l.BonusDraw(e);
            DrawCardsAction.DrawAndLog(e, Math.Max(0, count));
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
                e.MoveCard(card, PileKind.Discard);
                e.Log.Add(BattleEventType.CardDiscarded, $"随机弃掉 {card.Name}", target: card.Name).CardUid = card.Uid;
            }
        }
    }

    /// <summary>
    /// 把一张新牌放进指定牌堆（如敌人塞进来的疯语、缠网，禁书残页选中的牌）。
    /// 放进抽牌堆时插在随机位置；手牌满了就改放弃牌堆。tempCost ≥ 0 时设置本回合费用。
    /// </summary>
    public sealed class AddCardToPileAction : GameAction
    {
        readonly string _cardId;
        readonly PileKind _pile;
        readonly int _tempCost;

        public AddCardToPileAction(string cardId, PileKind pile, int tempCost = -1)
        {
            _cardId = cardId;
            _pile = pile;
            _tempCost = tempCost;
        }

        public override void Execute(BattleEngine e)
        {
            var p = e.Player;
            var card = e.NewCard(_cardId);
            string where;
            switch (_pile)
            {
                case PileKind.Draw:
                    e.MoveCard(card, PileKind.Draw, e.Rng.Misc.NextInt(p.DrawPile.Count + 1));
                    where = "抽牌堆";
                    break;
                case PileKind.Hand when p.Hand.Count < e.Config.HandLimit:
                    e.MoveCard(card, PileKind.Hand);
                    if (_tempCost >= 0) e.SetTempCost(card, _tempCost);
                    where = "手牌";
                    break;
                default:
                    e.MoveCard(card, PileKind.Discard);
                    where = "弃牌堆";
                    break;
            }
            e.Log.Add(BattleEventType.CardAdded, $"1 张{card.Name}被放进你的{where}", target: card.Name).CardUid = card.Uid;
        }
    }

    /// <summary>把一张已有的牌移到指定牌堆（选择弃牌、消耗等）。</summary>
    public sealed class MoveCardAction : GameAction
    {
        readonly Card _card;
        readonly PileKind _to;
        readonly string _verb;

        public MoveCardAction(Card card, PileKind to, string verb)
        {
            _card = card;
            _to = to;
            _verb = verb;
        }

        public override void Execute(BattleEngine e)
        {
            if (e.Locate(_card) == _to) return;
            e.MoveCard(_card, _to);
            var type = _to == PileKind.Discard ? BattleEventType.CardDiscarded : BattleEventType.CardAdded;
            e.Log.Add(type, $"{_verb} {_card.Name}", target: _card.Name).CardUid = _card.Uid;
        }
    }

    /// <summary>出牌第 ⑤ 步：能力牌移出战斗，「消耗」进消耗堆，其余进弃牌堆。</summary>
    public sealed class FinishCardAction : GameAction
    {
        readonly Card _card;

        public FinishCardAction(Card card) { _card = card; }

        public override void Execute(BattleEngine e)
        {
            if (e.Locate(_card) != PileKind.Play) return;   // 结算途中已经被移走
            if (_card.Def.Type == CardType.Power) e.MoveCard(_card, PileKind.None);
            else e.MoveCard(_card, _card.Exhaust ? PileKind.Exhaust : PileKind.Discard);
        }
    }

    /// <summary>
    /// 让玩家从若干选项里选一个（策划/08 §3.1）。选项在执行到这一步时才生成（比如「抽 3 弃 1」要等抽完才知道手牌）。
    /// 没有选项就跳过；只有一个选项且不允许跳过时自动选它，不打断玩家。
    /// </summary>
    public sealed class ChooseAction : GameAction
    {
        readonly string _prompt;
        readonly Func<BattleEngine, List<ChoiceOption>> _options;
        readonly Action<BattleEngine, int> _onChosen;

        public ChooseAction(string prompt, Func<BattleEngine, List<ChoiceOption>> options, Action<BattleEngine, int> onChosen)
        {
            _prompt = prompt;
            _options = options;
            _onChosen = onChosen;
        }

        public override void Execute(BattleEngine e)
        {
            var options = _options(e);
            if (options.Count == 0) return;
            if (options.Count == 1)
            {
                e.Log.Add(BattleEventType.ChoiceMade, $"{_prompt}：只有「{options[0].Label}」可选");
                _onChosen(e, 0);
                return;
            }
            var choice = new PendingChoice { Prompt = _prompt, OnChosen = _onChosen };
            choice.Options.AddRange(options);
            e.RequestChoice(choice);
        }

        /// <summary>把手牌列成选项。</summary>
        public static List<ChoiceOption> HandOptions(BattleEngine e) =>
            e.Player.Hand.Select(c => new ChoiceOption { Label = c.Name, CardUid = c.Uid, CardId = c.DeckId }).ToList();
    }
}
