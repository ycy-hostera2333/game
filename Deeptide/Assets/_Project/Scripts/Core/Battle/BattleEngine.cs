using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Actions;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Relics;

namespace Deeptide.Core
{
    /// <summary>
    /// 战斗内核：持有状态、动作队列和随机流，对外只接受 BattleCommand（策划/08 §2–3）。
    /// 逻辑瞬时结算并写入 Log，表现层按顺序回放 Log 播放动画。
    /// </summary>
    public sealed class BattleEngine
    {
        public readonly BattleSetup Setup;
        public readonly BattleConfig Config;
        public readonly BattleState State = new BattleState();
        public readonly ActionQueue Queue = new ActionQueue();
        public readonly BattleLog Log = new BattleLog();
        public readonly BattleRng Rng;
        /// <summary>成功执行过的命令。Setup + History 可以完整复现这场战斗。</summary>
        public readonly List<BattleCommand> History = new List<BattleCommand>();

        readonly List<BattleListener> _listeners = new List<BattleListener>();
        int _nextUid = 1;

        public PlayerState Player => State.Player;

        /// <summary>癫狂时敌人意图只显示类型、隐藏数值（策划/01 §2.7）。</summary>
        public bool IntentsBlurred => Player.IsLunatic;

        BattleEngine(BattleSetup setup)
        {
            Setup = setup;
            Config = setup.Config ?? new BattleConfig();
            Rng = new BattleRng(setup.Seed);
        }

        public static BattleEngine Start(BattleSetup setup)
        {
            var e = new BattleEngine(setup);
            e.StartCombat();
            return e;
        }

        public static BattleEngine Replay(BattleSetup setup, IEnumerable<BattleCommand> commands)
        {
            var e = Start(setup);
            foreach (var cmd in commands)
                if (!e.Execute(cmd, out var error))
                    throw new InvalidOperationException($"回放失败：{cmd}（{error}）");
            return e;
        }

        // ------------------------------------------------------------------ 命令

        public bool Execute(BattleCommand cmd, out string error)
        {
            bool ok = cmd.Kind == CommandKind.EndTurn
                ? TryEndTurn(out error)
                : TryPlayCard(cmd.HandIndex, cmd.TargetIndex, out error);
            if (ok) History.Add(cmd);
            return ok;
        }

        public bool CanPlay(int handIndex, out string error)
        {
            error = null;
            if (State.Outcome != BattleOutcome.Ongoing) { error = "战斗已结束"; return false; }
            if (State.Phase != BattlePhase.PlayerAction) { error = "现在不能出牌"; return false; }
            if (handIndex < 0 || handIndex >= Player.Hand.Count) { error = "没有这张牌"; return false; }
            var card = Player.Hand[handIndex];
            if (!card.Playable) { error = $"{card.Name}不能打出"; return false; }
            if (card.Cost > Player.Energy) { error = "能量不足"; return false; }
            return true;
        }

        /// <summary>出牌，按 策划/02 §3.5 的五步结算。</summary>
        bool TryPlayCard(int handIndex, int targetIndex, out string error)
        {
            if (!CanPlay(handIndex, out error)) return false;
            var card = Player.Hand[handIndex];

            EnemyState target = null;
            if (card.Def.Target == CardTarget.Enemy)
            {
                if (targetIndex < 0 || targetIndex >= State.Enemies.Count || State.Enemies[targetIndex].IsDead)
                {
                    error = "目标无效";
                    return false;
                }
                target = State.Enemies[targetIndex];
            }

            State.Phase = BattlePhase.Resolving;
            Player.Hand.RemoveAt(handIndex);

            bool mad = UsesMadFace(card);            // ① 锁定卡面
            Player.Energy -= card.Cost;              // ② 能量
            State.Stats.CardsPlayed++;
            Log.Add(BattleEventType.CardPlayed,
                $"打出 {card.Name}{(card.Def.Dual ? (mad ? "（癫狂面）" : "（清醒面）") : "")}{(target != null ? " → " + target.Name : "")}",
                "调查员", target?.Name);

            int sanityCost = card.SanityCost(mad);   // ③ 代价
            if (sanityCost > 0) Queue.AddToBottom(new LoseSanityAction(sanityCost, SanitySource.Card));

            card.Play(new PlayContext(this, card, target, mad));   // ④ 效果
            Queue.AddToBottom(new FinishCardAction(card));         // ⑤ 去向
            Notify(l => l.OnCardPlayed(this, card, mad));

            ProcessQueue();
            if (State.Outcome == BattleOutcome.Ongoing) State.Phase = BattlePhase.PlayerAction;
            return true;
        }

        bool TryEndTurn(out string error)
        {
            error = null;
            if (State.Outcome != BattleOutcome.Ongoing) { error = "战斗已结束"; return false; }
            if (State.Phase != BattlePhase.PlayerAction) { error = "现在不能结束回合"; return false; }

            State.Phase = BattlePhase.Resolving;
            foreach (var c in Player.Hand.ToList()) c.OnTurnEndInHand(this);
            Notify(l => l.OnPlayerTurnEnd(this));
            ProcessQueue();
            if (State.Outcome != BattleOutcome.Ongoing) return true;

            if (Player.IsClarity) State.Stats.ClarityTurns++;
            foreach (var c in Player.Hand)
                (c.Def.Ethereal ? Player.ExhaustPile : Player.DiscardPile).Add(c);
            Player.Hand.Clear();
            Player.Statuses.TickTurnEnd();

            RunEnemyTurn();
            if (State.Outcome == BattleOutcome.Ongoing) StartPlayerTurn();
            return true;
        }

        public bool UsesMadFace(Card card) => card.Def.Dual && Player.IsLunatic;

        // ------------------------------------------------------------------ 回合流程（策划/02 §2）

        void StartCombat()
        {
            var p = new PlayerState
            {
                Name = "调查员",
                MaxHp = Setup.MaxHp,
                Hp = Setup.Hp,
                MaxSanity = Setup.MaxSanity,
                Sanity = Setup.MaxSanity,
                LunacyThreshold = Config.LunacyThreshold,
            };
            State.Player = p;
            State.Stats.MinSanity = p.Sanity;

            foreach (var id in Setup.Deck) p.DrawPile.Add(NewCard(id));
            Rng.Shuffle.Shuffle(p.DrawPile);

            foreach (var id in Setup.Relics)
            {
                var relic = RelicLibrary.Create(id);
                p.Relics.Add(relic);
                _listeners.Add(relic);
            }

            var counts = Setup.Enemies.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < Setup.Enemies.Count; i++)
            {
                string id = Setup.Enemies[i];
                var def = EnemyLibrary.Get(id);
                seen[id] = seen.TryGetValue(id, out var k) ? k + 1 : 1;
                int hp = Rng.Misc.Range(def.HpMin, def.HpMax);
                State.Enemies.Add(new EnemyState
                {
                    Index = i,
                    Def = def,
                    Name = counts[id] > 1 ? $"{def.Name} {(char)('A' + seen[id] - 1)}" : def.Name,
                    Behaviour = def.Factory(),
                    MaxHp = hp,
                    Hp = hp,
                    Will = def.Will,
                    Mindless = def.Will <= 0,
                });
            }

            Log.Add(BattleEventType.CombatStarted,
                $"战斗开始：{string.Join("、", State.Enemies.Select(x => $"{x.Name}（{x.Hp}）"))}");
            Notify(l => l.OnCombatStart(this));
            ProcessQueue();

            foreach (var en in State.Enemies) RollIntent(en);
            StartPlayerTurn();
        }

        void StartPlayerTurn()
        {
            State.Turn++;
            Log.CurrentTurn = State.Turn;
            State.Phase = BattlePhase.Resolving;
            Log.Add(BattleEventType.TurnStarted, $"—— 第 {State.Turn} 回合 ——", amount: State.Turn);

            if (State.Turn >= Config.ErosionStartTurn)
                Queue.AddToBottom(new LoseSanityAction(Config.ErosionAmount, SanitySource.Erosion));
            Player.Block = 0;
            Player.Energy = Config.EnergyPerTurn;
            Notify(l => l.OnPlayerTurnStart(this));

            int bound = Player.Statuses.Get(StatusId.Bound);
            if (bound > 0) Player.Statuses.Set(StatusId.Bound, 0);
            Queue.AddToBottom(new DrawCardsAction(Math.Max(0, Config.DrawPerTurn - bound)));

            ProcessQueue();
            if (State.Outcome != BattleOutcome.Ongoing) return;
            if (Player.IsLunatic) State.Stats.LunacyTurns++;
            State.Phase = BattlePhase.PlayerAction;
        }

        void RunEnemyTurn()
        {
            foreach (var en in State.Enemies.ToList())
            {
                if (en.IsDead) continue;
                en.Block = 0;
                if (en.Stunned)
                {
                    en.Stunned = false;
                    Log.Add(BattleEventType.EnemyStunSkipped, $"{en.Name} 被震慑，跳过行动", en.Name);
                }
                else
                {
                    var move = en.Intent;
                    en.MoveHistory.Add(move.Id);
                    en.MoveUses[move.Id] = en.UsesOf(move.Id) + 1;
                    Log.Add(BattleEventType.EnemyMove, $"{en.Name} 使用「{move.Name}」", en.Name);
                    en.Behaviour.Perform(this, en, move);
                    ProcessQueue();
                    if (State.Outcome != BattleOutcome.Ongoing) return;
                }
                en.Statuses.TickTurnEnd();
                if (!en.IsDead) RollIntent(en);
            }
        }

        void RollIntent(EnemyState en)
        {
            en.Intent = en.Behaviour.ChooseNext(this, en);
            Log.Add(BattleEventType.IntentChosen, $"{en.Name} 的意图：{en.Intent.Name}", en.Name);
        }

        // ------------------------------------------------------------------ 结算工具（供动作调用）

        internal void ProcessQueue()
        {
            while (Queue.Count > 0)
            {
                if (State.Outcome != BattleOutcome.Ongoing)
                {
                    Queue.Clear();
                    return;
                }
                Queue.Pop().Execute(this);
                CheckOutcome();
            }
        }

        void CheckOutcome()
        {
            if (State.Outcome != BattleOutcome.Ongoing) return;
            if (Player.IsDead)
            {
                State.Outcome = BattleOutcome.Defeat;
                State.Phase = BattlePhase.Ended;
                Log.Add(BattleEventType.Defeat, "调查员倒下了……");
            }
            else if (State.Enemies.All(x => x.IsDead))
            {
                State.Outcome = BattleOutcome.Victory;
                State.Phase = BattlePhase.Ended;
                Log.Add(BattleEventType.Victory, $"战斗胜利！剩余生命 {Player.Hp}/{Player.MaxHp}");
            }
        }

        internal void Notify(Action<BattleListener> fn)
        {
            foreach (var l in _listeners.OrderBy(x => x.Priority).ToList()) fn(l);
        }

        public Card NewCard(string id) => CardLibrary.Create(id, _nextUid++);

        internal void ApplyDamage(Combatant source, Combatant target, int amount)
        {
            int absorbed = Math.Min(target.Block, amount);
            target.Block -= absorbed;
            int hpLoss = amount - absorbed;
            target.Hp = Math.Max(0, target.Hp - hpLoss);
            string src = source?.Name ?? "效果";
            Log.Add(BattleEventType.Damage,
                absorbed > 0
                    ? $"{src} 对 {target.Name} 造成 {amount} 伤害（格挡吸收 {absorbed}）→ 生命 {target.Hp}"
                    : $"{src} 对 {target.Name} 造成 {amount} 伤害 → 生命 {target.Hp}",
                src, target.Name, amount);
            if (target is EnemyState && target.IsDead)
                Log.Add(BattleEventType.EnemyDied, $"{target.Name} 被击倒", target: target.Name);
        }

        internal void LoseHp(Combatant target, int amount)
        {
            if (amount <= 0 || target.IsDead) return;
            target.Hp = Math.Max(0, target.Hp - amount);
            Log.Add(BattleEventType.HpLost, $"{target.Name} 失去 {amount} 生命 → {target.Hp}", target: target.Name, amount: amount);
            if (target is EnemyState && target.IsDead)
                Log.Add(BattleEventType.EnemyDied, $"{target.Name} 被击倒", target: target.Name);
        }

        /// <summary>抽一张牌；抽牌堆空了就洗弃牌堆。手牌满时这张牌直接进弃牌堆。没有牌可抽时返回 null。</summary>
        internal Card DrawOne()
        {
            var p = Player;
            if (p.DrawPile.Count == 0)
            {
                if (p.DiscardPile.Count == 0) return null;
                p.DrawPile.AddRange(p.DiscardPile);
                p.DiscardPile.Clear();
                Rng.Shuffle.Shuffle(p.DrawPile);
                Log.Add(BattleEventType.Shuffled, "弃牌堆洗回抽牌堆");
            }
            var card = p.DrawPile[p.DrawPile.Count - 1];
            p.DrawPile.RemoveAt(p.DrawPile.Count - 1);
            if (p.Hand.Count >= Config.HandLimit)
            {
                p.DiscardPile.Add(card);
                Log.Add(BattleEventType.CardDiscarded, $"手牌已满，{card.Name} 进入弃牌堆", target: card.Name);
            }
            else p.Hand.Add(card);
            return card;
        }

        // ------------------------------------------------------------------ 查询（供表现层 / 控制台）

        public IntentView GetIntent(EnemyState en)
        {
            var m = en.Intent;
            var view = new IntentView
            {
                Type = m?.Intent ?? IntentType.Unknown,
                MoveName = m?.Name ?? "?",
                Hits = m?.Hits ?? 0,
                SanityDamage = m?.SanityDamage ?? 0,
                Block = m?.Block ?? 0,
                Note = m?.Note,
                Stunned = en.Stunned,
                Blurred = IntentsBlurred,
            };
            if (m != null && m.Damage > 0) view.Damage = DamageCalculator.Compute(this, en, Player, m.Damage);
            return view;
        }

        /// <summary>本回合敌人预计造成的攻击伤害总和（不含被震慑的敌人）。</summary>
        public int IncomingDamage()
        {
            int sum = 0;
            foreach (var en in State.AliveEnemies)
            {
                if (en.Stunned || en.Intent == null || en.Intent.Damage <= 0) continue;
                sum += DamageCalculator.Compute(this, en, Player, en.Intent.Damage) * en.Intent.Hits;
            }
            return sum;
        }
    }

    public sealed class IntentView
    {
        public IntentType Type;
        public string MoveName;
        public int Damage;
        public int Hits;
        public int SanityDamage;
        public int Block;
        public string Note;
        public bool Stunned;
        public bool Blurred;
    }
}
