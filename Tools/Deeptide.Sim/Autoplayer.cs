using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core;

namespace Deeptide.Sim
{
    enum PlayStyle { Sane, Mad, Mixed }

    /// <summary>
    /// 自动出牌 AI（策划/07 §6.1）。默认用束搜索：每回合开始时在复制出的战斗（BattleEngine.Fork）上
    /// 搜索这一回合的出牌顺序（束宽 BeamWidth、最多 BeamDepth 张），按估值函数取最好的一条，然后照着打完、结束回合；
    /// 途中局面和预期不一致（比如用了药水）就重新规划。--ai greedy 换回原来的一步贪心（逐张比较「收益 / 费用」）。
    /// 预演会看到真实的抽牌结果，相当于知道牌序，所以只适合做相对比较。
    /// </summary>
    sealed class Autoplayer
    {
        readonly PlayStyle _style;

        public Autoplayer(PlayStyle style) { _style = style; }

        public static PlayStyle StyleFor(string deck) =>
            deck.StartsWith("B") ? PlayStyle.Mad : deck.StartsWith("C") || deck == "D" ? PlayStyle.Mixed : PlayStyle.Sane;

        public void PlayTurn(BattleEngine e)
        {
            while (e.State.Outcome == BattleOutcome.Ongoing &&
                   (e.State.Phase == BattlePhase.PlayerAction || e.State.Phase == BattlePhase.WaitingForChoice))
            {
                var cmd = Choose(e);
                if (!e.Execute(cmd, out var error)) throw new InvalidOperationException($"AI 选了非法命令 {cmd}：{error}");
                if (cmd.Kind == CommandKind.EndTurn) return;
            }
        }

        /// <summary>true = 束搜索（默认），false = 一步贪心。</summary>
        public static bool UseBeam = true;
        public static int BeamWidth = 6;
        public static int BeamDepth = 8;

        readonly List<BattleCommand> _plan = new List<BattleCommand>();
        BattleEngine _planFor;
        int _planHistory;

        public BattleCommand Choose(BattleEngine e)
        {
            if (e.State.Phase == BattlePhase.WaitingForChoice)
            {
                if (_planFor == e && _plan.Count > 0 && e.History.Count == _planHistory && _plan[0].Kind == CommandKind.Choose)
                    return NextPlanned();
                _plan.Clear();
                return BattleCommand.Choose(BestOption(e, out _));
            }
            if (!UseBeam) return ChooseGreedy(e);
            if (_planFor != e || _plan.Count == 0 || e.History.Count != _planHistory) PlanTurn(e);
            return NextPlanned();
        }

        BattleCommand NextPlanned()
        {
            var c = _plan[0];
            _plan.RemoveAt(0);
            _planHistory++;
            return c;
        }

        sealed class Node
        {
            public BattleEngine E;
            public List<BattleCommand> Seq;
            public double V;
        }

        /// <summary>束搜索这一回合的出牌顺序；计划以 end 结尾。中途打赢就直接用那条。</summary>
        void PlanTurn(BattleEngine e)
        {
            _planFor = e;
            _planHistory = e.History.Count;
            _plan.Clear();
            var root = new Node { E = e, Seq = new List<BattleCommand>(), V = Value(e) };
            var best = root;
            var beam = new List<Node> { root };
            for (int depth = 0; depth < BeamDepth && beam.Count > 0; depth++)
            {
                var children = new List<Node>();
                foreach (var node in beam)
                {
                    foreach (var cmd in Candidates(node.E))
                    {
                        var sim = node.E.Fork();
                        if (!sim.Execute(cmd, out _)) continue;
                        var seq = new List<BattleCommand>(node.Seq) { cmd };
                        if (sim.State.Phase == BattlePhase.WaitingForChoice)
                        {
                            var (resolved, extra) = ResolveChoices(sim, 3);
                            sim = resolved;
                            seq.AddRange(extra);
                        }
                        var child = new Node { E = sim, Seq = seq, V = Value(sim) };
                        if (sim.State.Outcome == BattleOutcome.Victory)
                        {
                            _plan.AddRange(seq);
                            return;
                        }
                        if (sim.State.Outcome == BattleOutcome.Ongoing && sim.State.Phase == BattlePhase.PlayerAction) children.Add(child);
                        if (child.V > best.V + 1e-9) best = child;
                    }
                }
                beam = children.OrderByDescending(c => c.V).Take(BeamWidth).ToList();
            }
            _plan.AddRange(best.Seq);
            if (best.E.State.Outcome == BattleOutcome.Ongoing) _plan.Add(BattleCommand.End());
        }

        /// <summary>候选命令：能打的牌 × 目标，同名同费同目标的只留一个（打哪一张结果一样）。</summary>
        static IEnumerable<BattleCommand> Candidates(BattleEngine e)
        {
            var seen = new HashSet<(string, int, int)>();
            var hand = e.Player.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                if (!e.CanPlay(i, out _)) continue;
                var card = hand[i];
                var targets = card.Def.Target == CardTarget.Enemy
                    ? e.State.Enemies.Where(x => !x.IsDead).Select(x => x.Index).ToArray()
                    : new[] { -1 };
                foreach (int t in targets)
                    if (seen.Add((card.DeckId, e.CostOf(card), t))) yield return BattleCommand.Play(i, t);
            }
        }

        /// <summary>局面停在选择上：逐个选项重放，取估值最高的；返回选完的局面和用到的选择命令。</summary>
        (BattleEngine, List<BattleCommand>) ResolveChoices(BattleEngine sim, int depth)
        {
            var extra = new List<BattleCommand>();
            while (sim.State.Phase == BattlePhase.WaitingForChoice && depth-- > 0)
            {
                int pick = BestOption(sim, out var resolved, 1);
                extra.Add(BattleCommand.Choose(pick));
                sim = resolved;
            }
            return (sim, extra);
        }

        BattleCommand ChooseGreedy(BattleEngine e)
        {

            double baseValue = Value(e);
            var best = BattleCommand.End();
            double bestScore = 0.05;
            var hand = e.Player.Hand;

            for (int i = 0; i < hand.Count; i++)
            {
                if (!e.CanPlay(i, out _)) continue;
                var card = hand[i];
                int cost = e.CostOf(card);
                var targets = card.Def.Target == CardTarget.Enemy
                    ? e.State.Enemies.Where(x => !x.IsDead).Select(x => x.Index).ToArray()
                    : new[] { -1 };

                foreach (int t in targets)
                {
                    var cmd = BattleCommand.Play(i, t);
                    var sim = Resolve(BattleEngine.Replay(e.Setup, e.History.Append(cmd)), 3);
                    double gain = Value(sim) - baseValue;
                    if (sim.State.Outcome == BattleOutcome.Ongoing)
                    {
                        int handDelta = sim.Player.Hand.Count - (hand.Count - 1);
                        int energyDelta = sim.Player.Energy - (e.Player.Energy - cost);
                        gain += 2.5 * handDelta + 3.5 * energyDelta;
                    }
                    double score = gain / Math.Max(card.Def.XCost ? e.Player.Energy : cost, 0.5);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = cmd;
                    }
                }
            }
            return best;
        }

        /// <summary>等待选择时，逐个选项重放、取估值最高的；返回选项下标，resolved 是选完之后的局面。</summary>
        int BestOption(BattleEngine e, out BattleEngine resolved, int depth = 3)
        {
            int best = 0;
            double bestValue = double.NegativeInfinity;
            resolved = e;
            for (int i = 0; i < e.State.Choice.Options.Count; i++)
            {
                var sim = BattleEngine.Replay(e.Setup, e.History.Append(BattleCommand.Choose(i)));
                if (depth > 1) sim = Resolve(sim, depth - 1);
                double v = Value(sim) + HandPotential(sim);
                if (v > bestValue)
                {
                    bestValue = v;
                    best = i;
                    resolved = sim;
                }
            }
            return best;
        }

        /// <summary>局面停在选择上时，替它选最好的一项，返回选完的局面。</summary>
        BattleEngine Resolve(BattleEngine sim, int depth)
        {
            if (sim.State.Phase != BattlePhase.WaitingForChoice || depth <= 0) return sim;
            BestOption(sim, out var resolved, depth);
            return resolved;
        }

        /// <summary>手牌的粗略潜力（只在比较选择时用）：能打的牌按费用算，诅咒 / 状态牌是负分。</summary>
        static double HandPotential(BattleEngine s) =>
            s.Player.Hand.Sum(c => !c.Playable || c.Def.Type == CardType.Status ? -2.0 : 1.0 + 0.3 * s.CostOf(c));

        double Value(BattleEngine s)
        {
            var p = s.Player;
            if (s.State.Outcome == BattleOutcome.Defeat) return -100000;
            if (s.State.Outcome == BattleOutcome.Victory) return 100000 + p.Hp * 10;

            double v = 1.2 * p.Hp;
            foreach (var en in s.State.Enemies)
            {
                if (en.IsDead) { v += en.Minion ? 3 : 8; continue; }   // 召唤物会被重新召唤，击杀价值低
                v -= 3.0 * en.Counter("Tide");   // Boss 潮位越高，大潮越近
                // 会做精神攻击的敌人优先击杀：每点精神攻击让它的剩余生命多算 25%
                int mindThreat = en.Behaviour.Moves.Max(m => m.SanityDamage);
                v -= en.Hp * (1 + 0.25 * mindThreat);
                v -= 2.0 * en.Statuses.Get(StatusId.Strength);
                if (!en.Mindless) v += 4.0 * en.Fear / Math.Max(1, en.Will);
            }

            v -= 1.3 * Math.Max(0, EstimatedIncoming(s) - p.Block);

            // 预计受到的精神攻击（心防每层挡一次）
            int ward = p.Statuses.Get(StatusId.Ward);
            double mind = 0;
            foreach (var en in s.State.AliveEnemies)
            {
                if (en.Stunned || en.Intent == null) continue;
                bool hidden = en.Behaviour.IntentHidden(s, en);
                if (!hidden && (en.Intent.Intent & IntentType.Mind) == 0) continue;
                if (ward > 0) { ward--; continue; }
                mind += hidden ? en.Behaviour.Moves.Average(m => m.SanityDamage)
                    : s.IntentsBlurred && !IgnoreBlur ? SameTypeMoves(en).Average(m => m.SanityDamage)
                    : en.Intent.SanityDamage;
            }
            mind += p.Hand.Count(c => c.Id == "S01");
            v += SanityValue(Math.Max(0, p.Sanity - (int)Math.Round(mind)), p);

            v += 3.0 * p.Statuses.Get(StatusId.Strength) + 1.5 * p.Statuses.Get(StatusId.Ward);
            // 能力牌的价值在之后的回合里兑现，一步贪心看不到；按「打出就有一笔固定收益」估计，否则 AI 永远不打能力
            v += 7.0 * p.Powers.Count;
            v -= 6.0 * s.State.Stats.CursesGained;   // 永久诅咒是局层代价
            return v;
        }

        /// <summary>调试开关（--peek 1）：癫狂时也偷看真实意图，用来量化意图模糊的信息代价。</summary>
        public static bool IgnoreBlur;

        /// <summary>
        /// AI 只用玩家能看到的信息：清醒时用真实意图数值；癫狂时只知道意图类型，
        /// 按该敌人「同类型招式」的平均伤害估计（玩家知道敌人会哪几招，但不知道这次是哪一招）；
        /// 意图完全隐藏的敌人（无面修女）按它全部招式的平均值估计。
        /// </summary>
        static double EstimatedIncoming(BattleEngine s)
        {
            double sum = 0;
            foreach (var en in s.State.AliveEnemies)
            {
                if (en.Stunned || en.Intent == null) continue;
                var b = en.Behaviour;
                bool hidden = b.IntentHidden(s, en);
                if (!hidden && (!s.IntentsBlurred || IgnoreBlur))
                {
                    sum += MoveDamage(s, en, en.Intent);
                    continue;
                }
                if (hidden) sum += b.Moves.Average(m => MoveDamage(s, en, m));
                else if ((en.Intent.Intent & IntentType.Attack) != 0)
                    sum += SameTypeMoves(en).Where(m => b.IntentDamage(s, en, m) > 0).Average(m => MoveDamage(s, en, m));
            }
            return sum;
        }

        static double MoveDamage(BattleEngine s, EnemyState en, Deeptide.Core.Enemies.EnemyMove m)
        {
            int dmg = en.Behaviour.IntentDamage(s, en, m);
            return dmg <= 0 ? 0 : DamageCalculator.Compute(s, en, s.Player, dmg) * en.Behaviour.IntentHits(s, en, m);
        }

        static System.Collections.Generic.List<Deeptide.Core.Enemies.EnemyMove> SameTypeMoves(EnemyState en) =>
            en.Behaviour.Moves.Where(m => m.Intent == en.Intent.Intent).ToList();

        double SanityValue(int sanity, PlayerState p)
        {
            bool lunatic = sanity <= p.LunacyThreshold;
            switch (_style)
            {
                case PlayStyle.Sane:
                    return 1.5 * sanity + (sanity >= p.ClarityThreshold ? 3 : 0) - (lunatic ? 4 : 0);
                case PlayStyle.Mad:
                    if (!lunatic) return 2 - 0.8 * (sanity - p.LunacyThreshold);
                    return sanity >= 2 ? 3 : sanity == 1 ? 1 : -2;
                default:
                    return 0.6 * sanity - (sanity <= 1 ? 3 : 0);
            }
        }
    }
}
