using System;

namespace Deeptide.Core.Actions
{
    /// <summary>
    /// 失去理智（策划/02 §3.3）。精神攻击先过祝圣银铃，再过心防；光环只过心防。
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

            if (_source == SanitySource.Enemy)
                foreach (var l in e.Listeners)
                    if (l.InterceptMindAttack(e, _amount)) return;

            if ((_source == SanitySource.Enemy || _source == SanitySource.Aura) && p.Statuses.Get(StatusId.Ward) > 0)
            {
                e.AddStatus(p, StatusId.Ward, -1);
                e.Log.Add(BattleEventType.WardConsumed, $"心防抵消了 {_amount} 点理智损失（剩余心防 {p.Statuses.Get(StatusId.Ward)}）");
                return;
            }

            bool wasLunatic = p.IsLunatic;
            int before = p.Sanity;
            int floor = 0;   // 永夜：理智不会被扣到 1 以下
            foreach (var l in e.Listeners) floor = Math.Max(floor, l.SanityFloor(e));
            e.SetSanity(Math.Max(before - _amount, Math.Min(floor, before)));
            int lost = before - p.Sanity;
            if (lost <= 0) return;

            var stats = e.State.Stats;
            stats.MinSanity = Math.Min(stats.MinSanity, p.Sanity);
            var ev = e.Log.Add(BattleEventType.SanityLost, $"失去 {lost} 理智（{SourceName(_source)}）→ {p.Sanity}/{p.MaxSanity}", amount: lost);
            ev.Value = p.Sanity;
            e.Notify(l => l.OnSanityLost(e, lost, _source));

            if (!wasLunatic && p.IsLunatic) e.LunacyCrossed(true);

            // 最后插队，保证崩溃排在其他触发效果之前
            if (p.Sanity == 0) e.Queue.AddToTop(new CollapseAction());
        }

        static string SourceName(SanitySource s)
        {
            switch (s)
            {
                case SanitySource.Card: return "代价";
                case SanitySource.Enemy: return "精神攻击";
                case SanitySource.Aura: return "光环";
                case SanitySource.Erosion: return "侵蚀";
                case SanitySource.Curse: return "疯语";
                default: return "效果";
            }
        }
    }

    /// <summary>恢复理智。恢复量按牌面计（含溢出，先经过遗物修正），实际理智不超过上限。</summary>
    public sealed class RestoreSanityAction : GameAction
    {
        readonly int _amount;

        public RestoreSanityAction(int amount) { _amount = amount; }

        public override void Execute(BattleEngine e)
        {
            var p = e.Player;
            if (_amount <= 0) return;   // 恢复 0 点不算恢复（满理智时打默祷），不触发破旧祈祷书
            int amount = _amount;
            foreach (var l in e.Listeners) amount = l.ModifyRestore(e, amount);
            if (amount <= 0) return;
            bool wasLunatic = p.IsLunatic;
            int actual = Math.Min(amount, p.MaxSanity - p.Sanity);
            int overflow = amount - actual;
            e.SetSanity(p.Sanity + actual);
            var ev = e.Log.Add(BattleEventType.SanityRestored,
                actual == 0 ? $"理智已满（溢出 {overflow}）"
                : overflow > 0 ? $"恢复 {actual} 理智（溢出 {overflow}）→ {p.Sanity}/{p.MaxSanity}"
                : $"恢复 {actual} 理智 → {p.Sanity}/{p.MaxSanity}",
                amount: actual);
            ev.Value = p.Sanity;
            ev.Value2 = overflow;
            e.Notify(l => l.OnSanityRestored(e, amount, overflow));
            if (wasLunatic && !p.IsLunatic) e.LunacyCrossed(false);
        }
    }

    /// <summary>
    /// 把理智直接设成某个值（迷幻剂）：高于目标就按「效果」失去，低于目标就恢复。
    /// 走失去 / 恢复两条正常路径，所以癫狂切换、崩溃和相关触发器都照常结算。
    /// </summary>
    public sealed class SetSanityAction : GameAction
    {
        readonly int _value;

        public SetSanityAction(int value) { _value = value; }

        public override void Execute(BattleEngine e)
        {
            int diff = e.Player.Sanity - _value;
            if (diff > 0) e.Queue.AddToTop(new LoseSanityAction(diff, SanitySource.Effect));
            else if (diff < 0) e.Queue.AddToTop(new RestoreSanityAction(-diff));
        }
    }

    /// <summary>崩溃（策划/01 §2.4）：失去 10% 最大生命，1 张疯语永久加入牌组（本场放进弃牌堆），理智重置为 3。</summary>
    public sealed class CollapseAction : GameAction
    {
        public override void Execute(BattleEngine e)
        {
            var p = e.Player;
            var cfg = e.Config;
            var cost = new CollapseCost { HpLoss = p.MaxHp * cfg.CollapseHpPercent / 100 };
            foreach (var l in e.Listeners) l.ModifyCollapse(e, cost);
            var stats = e.State.Stats;
            stats.Collapses++;
            if (cost.AddCurse) stats.CursesGained++;

            string curse = cost.AddCurse ? "1 张疯语永久加入牌组" : "没有获得疯语";
            e.Log.Add(BattleEventType.Collapse, $"理智崩溃！失去 {cost.HpLoss} 生命，{curse}，理智重置为 {cfg.CollapseSanityReset}", amount: cost.HpLoss);
            e.LoseHp(p, cost.HpLoss);
            if (cost.AddCurse) e.MoveCard(e.NewCard(Cards.CardIds.Whispers), PileKind.Discard);

            bool wasLunatic = p.IsLunatic;
            e.SetSanity(Math.Min(cfg.CollapseSanityReset, p.MaxSanity));
            if (wasLunatic && !p.IsLunatic) e.LunacyCrossed(false, counts: false);   // 重置不算复归（策划/14 §1.2）
        }
    }
}
