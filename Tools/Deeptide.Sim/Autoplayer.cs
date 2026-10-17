using System;
using System.Linq;
using Deeptide.Core;

namespace Deeptide.Sim
{
    enum PlayStyle { Sane, Mad, Mixed }

    /// <summary>
    /// 一步贪心的自动出牌 AI（策划/07 §6.1 的简化版）。
    /// 对每个候选命令，用「Setup + 历史命令 + 候选」重放出结果局面，按估值函数打分，取「收益 / 费用」最高的那张；
    /// 没有正收益的牌就结束回合。重放会看到真实的抽牌结果，相当于知道牌序，所以只适合做相对比较。
    /// </summary>
    sealed class Autoplayer
    {
        readonly PlayStyle _style;

        public Autoplayer(PlayStyle style) { _style = style; }

        public static PlayStyle StyleFor(string deck) =>
            deck == "B" ? PlayStyle.Mad : deck == "C" ? PlayStyle.Mixed : PlayStyle.Sane;

        public void PlayTurn(BattleEngine e)
        {
            while (e.State.Outcome == BattleOutcome.Ongoing && e.State.Phase == BattlePhase.PlayerAction)
            {
                var cmd = Choose(e);
                if (!e.Execute(cmd, out var error)) throw new InvalidOperationException($"AI 选了非法命令 {cmd}：{error}");
                if (cmd.Kind == CommandKind.EndTurn) return;
            }
        }

        public BattleCommand Choose(BattleEngine e)
        {
            double baseValue = Value(e);
            var best = BattleCommand.End();
            double bestScore = 0.05;
            var hand = e.Player.Hand;

            for (int i = 0; i < hand.Count; i++)
            {
                if (!e.CanPlay(i, out _)) continue;
                var card = hand[i];
                var targets = card.Def.Target == CardTarget.Enemy
                    ? e.State.Enemies.Where(x => !x.IsDead).Select(x => x.Index).ToArray()
                    : new[] { -1 };

                foreach (int t in targets)
                {
                    var cmd = BattleCommand.Play(i, t);
                    var sim = BattleEngine.Replay(e.Setup, e.History.Append(cmd));
                    double gain = Value(sim) - baseValue;
                    if (sim.State.Outcome == BattleOutcome.Ongoing)
                    {
                        int handDelta = sim.Player.Hand.Count - (hand.Count - 1);
                        int energyDelta = sim.Player.Energy - (e.Player.Energy - card.Cost);
                        gain += 2.5 * handDelta + 3.5 * energyDelta;
                    }
                    double score = gain / Math.Max(card.Cost, 0.5);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = cmd;
                    }
                }
            }
            return best;
        }

        double Value(BattleEngine s)
        {
            var p = s.Player;
            if (s.State.Outcome == BattleOutcome.Defeat) return -100000;
            if (s.State.Outcome == BattleOutcome.Victory) return 100000 + p.Hp * 10;

            double v = 1.2 * p.Hp;
            foreach (var en in s.State.Enemies)
            {
                if (en.IsDead) { v += 8; continue; }
                // 会做精神攻击的敌人优先击杀：每点精神攻击让它的剩余生命多算 25%
                int mindThreat = en.Behaviour.Moves.Max(m => m.SanityDamage);
                v -= en.Hp * (1 + 0.25 * mindThreat);
                v -= 2.0 * en.Statuses.Get(StatusId.Strength);
                if (!en.Mindless) v += 4.0 * en.Fear / Math.Max(1, en.Will);
            }

            v -= 1.3 * Math.Max(0, s.IncomingDamage() - p.Block);

            // 预计受到的精神攻击（心防每层挡一次）
            int ward = p.Statuses.Get(StatusId.Ward);
            int mind = 0;
            foreach (var en in s.State.AliveEnemies)
            {
                if (en.Stunned || en.Intent == null || en.Intent.SanityDamage <= 0) continue;
                if (ward > 0) { ward--; continue; }
                mind += en.Intent.SanityDamage;
            }
            mind += p.Hand.Count(c => c.Id == "S01");
            v += SanityValue(Math.Max(0, p.Sanity - mind), p);

            v += 3.0 * p.Statuses.Get(StatusId.Strength) + 1.5 * p.Statuses.Get(StatusId.Ward);
            v -= 6.0 * s.State.Stats.CursesGained;   // 永久诅咒是局层代价
            return v;
        }

        double SanityValue(int sanity, PlayerState p)
        {
            bool lunatic = sanity <= p.LunacyThreshold;
            switch (_style)
            {
                case PlayStyle.Sane:
                    return 1.5 * sanity + (sanity >= p.MaxSanity ? 3 : 0) - (lunatic ? 4 : 0);
                case PlayStyle.Mad:
                    if (!lunatic) return 2 - 0.8 * (sanity - p.LunacyThreshold);
                    return sanity >= 2 ? 3 : sanity == 1 ? 1 : -2;
                default:
                    return 0.6 * sanity - (sanity <= 1 ? 3 : 0);
            }
        }
    }
}
