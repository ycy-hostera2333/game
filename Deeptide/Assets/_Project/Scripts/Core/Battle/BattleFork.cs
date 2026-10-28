using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Cards;
using Deeptide.Core.Relics;

namespace Deeptide.Core
{
    public sealed partial class BattleEngine
    {
        /// <summary>
        /// 在命令边界（玩家行动阶段，两条队列都为空）复制出一份完全独立的战斗，供 AI 预演用：
        /// 随机流状态、牌堆顺序、遗物 / 能力 / 敌人 AI 的内部状态都一起复制，所以复制品接下来的结算和原战斗一模一样。
        /// 不复制日志。等待选择时（选项里有闭包）不能复制，改用 Replay。
        /// </summary>
        public BattleEngine Fork()
        {
            if (State.Phase != BattlePhase.PlayerAction || Queue.Count > 0 || _flow.Count > 0)
                throw new InvalidOperationException("只能在玩家行动阶段复制战斗");

            var f = new BattleEngine(Setup, Rng.Copy())
            {
                Config = Config,   // 开战后不再改，可以共用
                _nextUid = _nextUid,
                _innateExtraDraw = _innateExtraDraw,
            };
            f.History.AddRange(History);
            f.Log.CurrentTurn = Log.CurrentTurn;

            var s = State;
            var t = f.State;
            t.Turn = s.Turn;
            t.Phase = s.Phase;
            t.Outcome = s.Outcome;
            t.EnemyTurn = s.EnemyTurn;
            t.CardsPlayedThisTurn = s.CardsPlayedThisTurn;
            t.AttacksPlayedThisTurn = s.AttacksPlayedThisTurn;
            t.IntentsRevealedThisTurn = s.IntentsRevealedThisTurn;
            t.TransformedThisRound = s.TransformedThisRound;
            t.RevertedThisRound = s.RevertedThisRound;
            t.TransformsThisRound = s.TransformsThisRound;
            t.RevertsThisRound = s.RevertsThisRound;
            CopyStats(s.Stats, t.Stats);
            t.AttacksThisTurn.AddRange(s.AttacksThisTurn);   // 记下之后不再改，可以共用

            var p = s.Player;
            var q = new PlayerState
            {
                Name = p.Name,
                Hp = p.Hp,
                MaxHp = p.MaxHp,
                Block = p.Block,
                Energy = p.Energy,
                Sanity = p.Sanity,
                MaxSanity = p.MaxSanity,
                LunacyThreshold = p.LunacyThreshold,
                ClarityMargin = p.ClarityMargin,
                EnergyNextTurn = p.EnergyNextTurn,
            };
            CopyStatuses(p.Statuses, q.Statuses);
            q.DrawPile.AddRange(p.DrawPile.Select(CloneCard));
            q.Hand.AddRange(p.Hand.Select(CloneCard));
            q.DiscardPile.AddRange(p.DiscardPile.Select(CloneCard));
            q.ExhaustPile.AddRange(p.ExhaustPile.Select(CloneCard));
            q.Potions.AddRange(p.Potions);   // 药水实例没有状态，可以共享

            var map = new Dictionary<BattleListener, BattleListener>();
            foreach (var r in p.Traits)
            {
                var c = (Relic)r.CloneListener();
                q.Traits.Add(c);
                map[r] = c;
            }
            foreach (var r in p.Relics)
            {
                var c = (Relic)r.CloneListener();
                q.Relics.Add(c);
                map[r] = c;
            }
            foreach (var pw in p.Powers)
            {
                var c = (Power)pw.CloneListener();
                q.Powers.Add(c);
                map[pw] = c;
            }
            foreach (var l in _listeners) f._listeners.Add(map[l]);
            t.Player = q;

            foreach (var en in s.Enemies)
            {
                var c = new EnemyState
                {
                    Index = en.Index,
                    Def = en.Def,
                    Name = en.Name,
                    Behaviour = en.Behaviour.CloneBehaviour(),
                    Hp = en.Hp,
                    MaxHp = en.MaxHp,
                    Block = en.Block,
                    Mindless = en.Mindless,
                    Will = en.Will,
                    Fear = en.Fear,
                    Stunned = en.Stunned,
                    Minion = en.Minion,
                    Intent = en.Intent,   // 招式对象只读，复制出的 AI 共享同一张招式表
                };
                CopyStatuses(en.Statuses, c.Statuses);
                c.MoveHistory.AddRange(en.MoveHistory);
                foreach (var kv in en.MoveUses) c.MoveUses[kv.Key] = kv.Value;
                foreach (var kv in en.Counters) c.Counters[kv.Key] = kv.Value;
                t.Enemies.Add(c);
            }
            return f;
        }

        static Card CloneCard(Card card)
        {
            var c = card.Def.Factory();
            c.Init(card.Def, card.Uid, card.Upgraded);
            c.CostThisTurn = card.CostThisTurn;
            c.Pile = card.Pile;
            return c;
        }

        static void CopyStatuses(StatusSet from, StatusSet to)
        {
            foreach (var kv in from.Ordered()) to.Set(kv.Key, kv.Value);
        }

        static void CopyStats(BattleStats a, BattleStats b)
        {
            b.Collapses = a.Collapses;
            b.CursesGained = a.CursesGained;
            b.MaxHpGained = a.MaxHpGained;
            b.Transforms = a.Transforms;
            b.Reverts = a.Reverts;
            b.FirstLunacyTurn = a.FirstLunacyTurn;
            b.LunacyTurns = a.LunacyTurns;
            b.ClarityTurns = a.ClarityTurns;
            b.MinSanity = a.MinSanity;
            b.CardsPlayed = a.CardsPlayed;
            b.PotionsUsed = a.PotionsUsed;
            foreach (var kv in a.PlaysByCard) b.PlaysByCard[kv.Key] = kv.Value;
        }
    }
}
