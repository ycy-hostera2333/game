using System;

namespace Deeptide.Core.Actions
{
    /// <summary>
    /// 失去理智（策划/02 §3.3）。来自敌人的损失先被心防抵消。
    /// 进入癫狂时通知触发器；归 0 时把崩溃插到队首——「先完整结算崩溃，再继续」。
    /// </summary>
    public sealed class LoseSanityAction : GameAction
    {
        readonly int _amount;
        readonly SanitySource _source;

        public LoseSanityAction(int amount, SanitySource source)
        {
            _amount = amount;
            _source = source;
        }

        public override void Execute(BattleEngine e)
        {
            var p = e.Player;
            if (_amount <= 0) return;

            if (_source == SanitySource.Enemy && p.Statuses.Get(StatusId.Ward) > 0)
            {
                p.Statuses.Add(StatusId.Ward, -1);
                e.Log.Add(BattleEventType.WardConsumed, $"心防抵消了 {_amount} 点理智损失（剩余心防 {p.Statuses.Get(StatusId.Ward)}）");
                return;
            }

            bool wasLunatic = p.IsLunatic;
            int before = p.Sanity;
            p.Sanity = Math.Max(0, before - _amount);
            int lost = before - p.Sanity;
            if (lost <= 0) return;

            var stats = e.State.Stats;
            stats.MinSanity = Math.Min(stats.MinSanity, p.Sanity);
            e.Log.Add(BattleEventType.SanityLost, $"失去 {lost} 理智（{SourceName(_source)}）→ {p.Sanity}/{p.MaxSanity}", amount: lost);
            e.Notify(l => l.OnSanityLost(e, lost, _source));

            if (!wasLunatic && p.IsLunatic)
            {
                if (stats.FirstLunacyTurn == 0) stats.FirstLunacyTurn = e.State.Turn;
                e.Log.Add(BattleEventType.LunacyEntered, "进入癫狂：双面牌翻到癫狂面，敌人意图变得模糊");
                e.Notify(l => l.OnLunacyChanged(e, true));
            }

            // 最后插队，保证崩溃排在其他触发效果之前
            if (p.Sanity == 0) e.Queue.AddToTop(new CollapseAction());
        }

        static string SourceName(SanitySource s)
        {
            switch (s)
            {
                case SanitySource.Card: return "代价";
                case SanitySource.Enemy: return "精神攻击";
                case SanitySource.Erosion: return "侵蚀";
                case SanitySource.Curse: return "疯语";
                default: return "效果";
            }
        }
    }

    /// <summary>恢复理智。恢复量按牌面计（含溢出），实际理智不超过上限。</summary>
    public sealed class RestoreSanityAction : GameAction
    {
        readonly int _amount;

        public RestoreSanityAction(int amount) { _amount = amount; }

        public override void Execute(BattleEngine e)
        {
            var p = e.Player;
            if (_amount <= 0) return;
            bool wasLunatic = p.IsLunatic;
            int actual = Math.Min(_amount, p.MaxSanity - p.Sanity);
            int overflow = _amount - actual;
            p.Sanity += actual;
            e.Log.Add(BattleEventType.SanityRestored,
                overflow > 0 ? $"恢复 {actual} 理智（溢出 {overflow}）→ {p.Sanity}/{p.MaxSanity}" : $"恢复 {actual} 理智 → {p.Sanity}/{p.MaxSanity}",
                amount: actual);
            e.Notify(l => l.OnSanityRestored(e, _amount, overflow));
            if (wasLunatic && !p.IsLunatic)
            {
                e.Log.Add(BattleEventType.LunacyLeft, "离开癫狂：双面牌翻回清醒面");
                e.Notify(l => l.OnLunacyChanged(e, false));
            }
        }
    }

    /// <summary>崩溃（策划/01 §2.4）：失去 10% 最大生命，1 张疯语永久加入牌组（本场放进弃牌堆），理智重置为 3。</summary>
    public sealed class CollapseAction : GameAction
    {
        public override void Execute(BattleEngine e)
        {
            var p = e.Player;
            var cfg = e.Config;
            int hpLoss = p.MaxHp * cfg.CollapseHpPercent / 100;
            var stats = e.State.Stats;
            stats.Collapses++;
            stats.CursesGained++;

            e.Log.Add(BattleEventType.Collapse, $"理智崩溃！失去 {hpLoss} 生命，1 张疯语永久加入牌组，理智重置为 {cfg.CollapseSanityReset}", amount: hpLoss);
            e.LoseHp(p, hpLoss);
            p.DiscardPile.Add(e.NewCard(Cards.CardIds.Whispers));

            bool wasLunatic = p.IsLunatic;
            p.Sanity = Math.Min(cfg.CollapseSanityReset, p.MaxSanity);
            if (wasLunatic && !p.IsLunatic)
            {
                e.Log.Add(BattleEventType.LunacyLeft, "离开癫狂：双面牌翻回清醒面");
                e.Notify(l => l.OnLunacyChanged(e, false));
            }
        }
    }
}
