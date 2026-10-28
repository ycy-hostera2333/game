using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Actions;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Potions;
using Deeptide.Core.Relics;

namespace Deeptide.Core
{
    /// <summary>
    /// 战斗内核：持有状态、动作队列和随机流，对外只接受 BattleCommand（策划/08 §2–3）。
    /// 逻辑瞬时结算并写入 Log，表现层按顺序回放 Log 播放动画。
    ///
    /// 结算模型：动作队列（GameAction）之外还有一条「流程步骤」队列。回合流程被拆成若干步骤，
    /// 每一步只在动作队列清空之后才执行。需要玩家选择时（WaitingForChoice）两条队列都原地暂停，
    /// 收到 ChooseOption 命令后从断点继续——所以选择可以出现在出牌中途、回合开始等任何位置（策划/09 M1.1）。
    ///
    /// 所有状态修改都经过本类的 Set... / MoveCard 等方法，它们会写出状态事件，
    /// 表现层只读事件流就能重建整个战斗状态（策划/09 M1.7）。
    /// </summary>
    public sealed partial class BattleEngine
    {
        public readonly BattleSetup Setup;
        /// <summary>这场战斗的规则参数：开战时从 Setup.Config 复制，再经监听器的 ModifyConfig 修改（回响）。</summary>
        public BattleConfig Config { get; private set; }
        public readonly BattleState State = new BattleState();
        public readonly ActionQueue Queue = new ActionQueue();
        public readonly BattleLog Log = new BattleLog();
        public readonly BattleRng Rng;
        /// <summary>成功执行过的命令。Setup + History 可以完整复现这场战斗。</summary>
        public readonly List<BattleCommand> History = new List<BattleCommand>();

        readonly List<BattleListener> _listeners = new List<BattleListener>();
        readonly LinkedList<Action> _flow = new LinkedList<Action>();
        int _nextUid = 1;
        int _innateExtraDraw;

        public PlayerState Player => State.Player;

        /// <summary>遗物和能力，按 Priority 排好序（同优先级按加入顺序）。</summary>
        public IReadOnlyList<BattleListener> Listeners => _listeners;

        /// <summary>癫狂时敌人意图只显示类型、隐藏数值（策划/01 §2.7）；点亮提灯、鲸油灯可以看清。</summary>
        public bool IntentsBlurred =>
            Player.IsLunatic && !State.IntentsRevealedThisTurn && !_listeners.Any(l => l.RevealsIntents(this));

        BattleEngine(BattleSetup setup) : this(setup, new BattleRng(setup.Seed)) { }

        BattleEngine(BattleSetup setup, BattleRng rng)
        {
            Setup = setup;
            Config = setup.Config ?? new BattleConfig();
            Rng = rng;
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
            bool ok;
            switch (cmd.Kind)
            {
                case CommandKind.EndTurn: ok = TryEndTurn(out error); break;
                case CommandKind.Choose: ok = TryChoose(cmd.Index, out error); break;
                case CommandKind.UsePotion: ok = TryUsePotion(cmd.Index, cmd.TargetIndex, out error); break;
                default: ok = TryPlayCard(cmd.Index, cmd.TargetIndex, out error); break;
            }
            if (ok) History.Add(cmd);
            return ok;
        }

        bool CheckActionPhase(out string error, string what)
        {
            error = null;
            if (State.Outcome != BattleOutcome.Ongoing) { error = "战斗已结束"; return false; }
            if (State.Phase == BattlePhase.WaitingForChoice) { error = "请先做出选择"; return false; }
            if (State.Phase != BattlePhase.PlayerAction) { error = $"现在不能{what}"; return false; }
            return true;
        }

        public bool CanPlay(int handIndex, out string error)
        {
            if (!CheckActionPhase(out error, "出牌")) return false;
            if (handIndex < 0 || handIndex >= Player.Hand.Count) { error = "没有这张牌"; return false; }
            var card = Player.Hand[handIndex];
            if (!card.Playable) { error = $"{card.Name}不能打出"; return false; }
            if (!card.Def.XCost && CostOf(card) > Player.Energy) { error = "能量不足"; return false; }
            return true;
        }

        /// <summary>当前费用：本回合临时费用（禁书残页）→ 监听器修正。X 费牌返回当前能量。</summary>
        public int CostOf(Card card)
        {
            if (card.Def.XCost) return Player.Energy;
            int cost = card.CostThisTurn >= 0 ? card.CostThisTurn : card.Cost;
            foreach (var l in _listeners) cost = l.ModifyCost(this, card, cost);
            return Math.Max(0, cost);
        }

        public bool UsesMadFace(Card card) =>
            card.Def.Dual && (Player.IsLunatic || _listeners.Any(l => l.ForcesMadFace(this, card)));

        /// <summary>出牌，按 策划/02 §3.5 的五步结算。</summary>
        bool TryPlayCard(int handIndex, int targetIndex, out string error)
        {
            if (!CanPlay(handIndex, out error)) return false;
            var card = Player.Hand[handIndex];

            EnemyState target = null;
            if (card.Def.Target == CardTarget.Enemy)
            {
                target = TargetAt(targetIndex);
                if (target == null) { error = "目标无效"; return false; }
            }

            State.Phase = BattlePhase.Resolving;
            bool mad = UsesMadFace(card);            // ① 锁定卡面
            bool both = card.Def.Dual && _listeners.Any(l => l.ResolvesBothFaces(this, card));   // 人狼同体：两面都结算
            int cost = CostOf(card);
            int x = card.Def.XCost ? Player.Energy : 0;
            MoveCard(card, PileKind.Play);
            SetEnergy(Player.Energy - cost);         // ② 能量
            State.CardsPlayedThisTurn++;
            if (card.Def.Type == CardType.Attack) State.AttacksPlayedThisTurn++;
            State.Stats.CardsPlayed++;
            State.Stats.PlaysByCard[card.Id] = State.Stats.PlaysByCard.TryGetValue(card.Id, out var plays) ? plays + 1 : 1;
            string face = !card.Def.Dual ? "" : both ? "（两面）" : mad ? "（癫狂面）" : "（清醒面）";
            var ev = Log.Add(BattleEventType.CardPlayed,
                $"打出 {card.Name}{face}{(card.Def.XCost ? $"（X={x}）" : "")}{(target != null ? " → " + target.Name : "")}",
                Player.Name, target?.Name);
            ev.CardUid = card.Uid;
            ev.Id = card.DeckId;
            ev.Source = BattleEvent.PlayerEntity;
            ev.Entity = target?.Index ?? BattleEvent.NoEntity;
            ev.Flag = mad || both;
            ev.Value = cost;
            ev.Value2 = x;

            if (both)
            {
                // 先清醒面（人形面）再癫狂面（兽形面），每一面照常 ③ 付代价 ④ 结算（策划/14 §2.5）。
                // 兽形的 +2、格挡减半看结算那一刻的形态，所以人形面把你推进兽形之后，兽形面就吃得到加成
                PayAndPlay(card, target, false, x);
                PayAndPlay(card, target, true, x);
            }
            else PayAndPlay(card, target, mad, x);
            Queue.AddToBottom(new FinishCardAction(card));            // ⑤ 去向
            Notify(l => l.OnCardPlayed(this, card, mad || both));

            Settle();
            return true;
        }

        /// <summary>出牌的第 ③ 步（代价）和第 ④ 步（效果）。</summary>
        void PayAndPlay(Card card, EnemyState target, bool mad, int x)
        {
            int sanityCost = card.SanityCost(mad, x);   // ③ 代价
            if (sanityCost > 0) Queue.AddToBottom(new LoseSanityAction(sanityCost, SanitySource.Card));
            card.Play(new PlayContext(this, card, target, mad, x));   // ④ 效果
        }

        EnemyState TargetAt(int index) =>
            index >= 0 && index < State.Enemies.Count && !State.Enemies[index].IsDead ? State.Enemies[index] : null;

        bool TryEndTurn(out string error)
        {
            if (!CheckActionPhase(out error, "结束回合")) return false;
            State.Phase = BattlePhase.Resolving;
            _flow.AddLast(EndTurnTriggers);
            _flow.AddLast(EndTurnCleanup);
            _flow.AddLast(BeginEnemyTurn);
            Settle();
            return true;
        }

        bool TryChoose(int option, out string error)
        {
            error = null;
            if (State.Outcome != BattleOutcome.Ongoing) { error = "战斗已结束"; return false; }
            if (State.Phase != BattlePhase.WaitingForChoice || State.Choice == null) { error = "现在没有需要做的选择"; return false; }
            if (option < 0 || option >= State.Choice.Options.Count) { error = "没有这个选项"; return false; }

            var choice = State.Choice;
            State.Choice = null;
            State.Phase = BattlePhase.Resolving;
            var ev = Log.Add(BattleEventType.ChoiceMade, $"选择：{choice.Options[option].Label}");
            ev.Value = option;
            ev.CardUid = choice.Options[option].CardUid;
            choice.OnChosen(this, option);
            Settle();
            return true;
        }

        public bool CanUsePotion(int slot, out string error)
        {
            if (!CheckActionPhase(out error, "用药水")) return false;
            if (slot < 0 || slot >= Player.Potions.Count || Player.Potions[slot] == null) { error = "这个药水栏是空的"; return false; }
            return true;
        }

        bool TryUsePotion(int slot, int targetIndex, out string error)
        {
            if (!CanUsePotion(slot, out error)) return false;
            var potion = Player.Potions[slot];
            EnemyState target = null;
            if (potion.Def.Target == CardTarget.Enemy)
            {
                target = TargetAt(targetIndex);
                if (target == null) { error = "目标无效"; return false; }
            }

            State.Phase = BattlePhase.Resolving;
            SetPotion(slot, null);
            State.Stats.PotionsUsed++;
            var ev = Log.Add(BattleEventType.PotionUsed, $"使用药水 {potion.Name}{(target != null ? " → " + target.Name : "")}", Player.Name, target?.Name);
            ev.Id = potion.Id;
            ev.Value = slot;
            ev.Entity = target?.Index ?? BattleEvent.NoEntity;
            potion.Use(new PotionContext(this, potion, target));
            Settle();
            return true;
        }

        // ------------------------------------------------------------------ 结算循环

        /// <summary>跑动作队列和流程步骤，直到全部结算完、需要玩家选择、或战斗结束。</summary>
        void Pump()
        {
            while (State.Outcome == BattleOutcome.Ongoing && State.Phase != BattlePhase.WaitingForChoice)
            {
                if (Queue.Count > 0) Queue.Pop().Execute(this);
                else if (_flow.Count > 0)
                {
                    var step = _flow.First.Value;
                    _flow.RemoveFirst();
                    step();
                }
                else break;
                CheckOutcome();
            }
        }

        /// <summary>结算到底；全部结算完且没有在等选择时回到玩家行动阶段。</summary>
        void Settle()
        {
            Pump();
            if (State.Outcome == BattleOutcome.Ongoing && State.Phase == BattlePhase.Resolving && Queue.Count == 0 && _flow.Count == 0)
                State.Phase = BattlePhase.PlayerAction;
        }

        /// <summary>按顺序插到流程队列最前面。</summary>
        void PushFlow(params Action[] steps)
        {
            for (int i = steps.Length - 1; i >= 0; i--) _flow.AddFirst(steps[i]);
        }

        internal void RequestChoice(PendingChoice choice)
        {
            State.Choice = choice;
            State.Phase = BattlePhase.WaitingForChoice;
            var ev = Log.Add(BattleEventType.ChoiceRequested, $"请选择：{choice.Prompt}（{string.Join(" / ", choice.Options.Select(o => o.Label))}）");
            ev.Value = choice.Options.Count;
        }

        void CheckOutcome()
        {
            if (State.Outcome != BattleOutcome.Ongoing) return;
            if (Player.IsDead)
            {
                State.Outcome = BattleOutcome.Defeat;
                State.Phase = BattlePhase.Ended;
                Log.Add(BattleEventType.Defeat, "调查员倒下了……");
                EndCombat(false);
            }
            else if (State.Enemies.Where(x => !x.Minion).All(x => x.IsDead))
            {
                State.Outcome = BattleOutcome.Victory;
                State.Phase = BattlePhase.Ended;
                Log.Add(BattleEventType.Victory, $"战斗胜利！剩余生命 {Player.Hp}/{Player.MaxHp}");
                EndCombat(true);
            }
        }

        void EndCombat(bool victory)
        {
            Queue.Clear();
            _flow.Clear();
            State.Choice = null;
            Notify(l => l.OnCombatEnd(this, victory));
            // 战斗结束时的效果（如渔夫的护身符回复生命）立即结算，不再判定胜负
            while (Queue.Count > 0) Queue.Pop().Execute(this);
        }

        internal void Notify(Action<BattleListener> fn)
        {
            foreach (var l in _listeners.ToList()) fn(l);
        }

        internal void AddListener(BattleListener listener)
        {
            int i = _listeners.Count;
            while (i > 0 && _listeners[i - 1].Priority > listener.Priority) i--;
            _listeners.Insert(i, listener);
        }

        // ------------------------------------------------------------------ 回合流程（策划/02 §2）

        void StartCombat()
        {
            var p = new PlayerState
            {
                Name = Run.CharacterLibrary.Exists(Setup.Character) ? Run.CharacterLibrary.Get(Setup.Character).Name : "调查员",
                MaxHp = Setup.MaxHp,
                Hp = Setup.Hp,
                MaxSanity = Setup.MaxSanity,
                Sanity = Setup.MaxSanity,
                LunacyThreshold = Config.LunacyThreshold,
                ClarityMargin = Config.ClarityMargin,
            };
            State.Player = p;
            State.Stats.MinSanity = p.Sanity;
            var init = Log.State(BattleEventType.PlayerInit, BattleEvent.PlayerEntity, p.Hp, value2: p.MaxHp);
            init.Amount = p.MaxSanity;

            foreach (var id in Setup.Deck) MoveCard(NewCard(id), PileKind.Draw);
            Rng.Shuffle.Shuffle(p.DrawPile);
            PutInnateOnTop();

            foreach (var id in Setup.Traits)
            {
                var trait = RelicLibrary.Create(id);
                p.Traits.Add(trait);
                AddListener(trait);
            }
            foreach (var id in Setup.Echoes)
            {
                var echo = EchoLibrary.Create(id);
                p.Traits.Add(echo);
                AddListener(echo);
            }
            foreach (var id in Setup.Relics)
            {
                var relic = RelicLibrary.Create(id);
                p.Relics.Add(relic);
                AddListener(relic);
            }
            ApplyConfigModifiers();

            for (int i = 0; i < Math.Max(Setup.PotionSlots, Setup.Potions.Count); i++)
            {
                p.Potions.Add(null);
                string id = i < Setup.Potions.Count ? Setup.Potions[i] : null;
                SetPotion(i, id != null ? PotionLibrary.Create(id) : null);   // 空位也写事件，表现层据此知道药水栏有几格
            }

            var counts = Setup.Enemies.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var seen = new Dictionary<string, int>();
            foreach (var id in Setup.Enemies)
            {
                var def = EnemyLibrary.Get(id);
                seen[id] = seen.TryGetValue(id, out var k) ? k + 1 : 1;
                SpawnEnemy(id, false, counts[id] > 1 ? $"{def.Name} {(char)('A' + seen[id] - 1)}" : def.Name);
            }

            Log.Add(BattleEventType.CombatStarted,
                $"战斗开始：{string.Join("、", State.Enemies.Select(x => $"{x.Name}（{x.Hp}）"))}");
            State.Phase = BattlePhase.Resolving;
            _flow.AddLast(() => Notify(l => l.OnCombatStart(this)));
            _flow.AddLast(() => { foreach (var en in State.Enemies) RollIntent(en); });
            _flow.AddLast(StartPlayerTurn);
            Settle();
        }

        /// <summary>
        /// 规则参数交给监听器改（回响，策划/15 §7.1），再按改过的参数定下玩家的癫狂线、清明线和开战理智。
        /// 开战理智低于上限时写一条理智变化的状态事件，表现层照常重建；这不算「进入癫狂」，不触发变身、调查笔记。
        /// </summary>
        void ApplyConfigModifiers()
        {
            Config = Config.Clone();
            foreach (var l in _listeners) l.ModifyConfig(Config);
            var p = Player;
            p.LunacyThreshold = Config.LunacyThreshold;
            p.ClarityMargin = Config.ClarityMargin;
            if (Config.StartSanityDeficit > 0) SetSanity(Math.Max(1, p.MaxSanity - Config.StartSanityDeficit));
            State.Stats.MinSanity = p.Sanity;
        }

        /// <summary>「固有」的牌放到抽牌堆顶；固有牌多于每回合抽牌数时，第一回合多抽。</summary>
        void PutInnateOnTop()
        {
            var draw = Player.DrawPile;
            var innate = draw.Where(c => c.Has(CardKeywords.Innate)).ToList();
            if (innate.Count == 0) return;
            foreach (var c in innate) draw.Remove(c);
            for (int i = innate.Count - 1; i >= 0; i--) draw.Add(innate[i]);
            _innateExtraDraw = Math.Max(0, innate.Count - Config.DrawPerTurn);
        }

        void StartPlayerTurn()
        {
            State.Turn++;
            Log.CurrentTurn = State.Turn;
            State.Phase = BattlePhase.Resolving;
            var ev = Log.Add(BattleEventType.TurnStarted, $"—— 第 {State.Turn} 回合 ——", amount: State.Turn);
            ev.Value = State.Turn;
            State.CardsPlayedThisTurn = 0;
            State.AttacksPlayedThisTurn = 0;
            SetIntentsRevealed(false);
            SetRoundFlags(false, false);   // 新的一轮从这里开始，侵蚀推过线算这一轮的变身

            if (State.Turn >= Config.ErosionStartTurn)
                Queue.AddToBottom(new LoseSanityAction(Config.ErosionAmount, SanitySource.Erosion));
            foreach (var en in State.AliveEnemies.ToList()) en.Behaviour.OnPlayerTurnStart(this, en);
            SetBlock(Player, 0);
            SetEnergy(Config.EnergyPerTurn + Player.EnergyNextTurn);
            SetEnergyNextTurn(0);
            Notify(l => l.OnPlayerTurnStart(this));
            Queue.AddToBottom(new TurnDrawAction(State.Turn == 1 ? _innateExtraDraw : 0));

            PushFlow(() =>
            {
                if (Player.IsLunatic) State.Stats.LunacyTurns++;
                State.Phase = BattlePhase.PlayerAction;
            });
        }

        void EndTurnTriggers()
        {
            State.EnemyTurn = true;   // 从这里开始获得的能量都用不上了，顺延到下回合
            foreach (var c in Player.Hand.ToList()) c.OnTurnEndInHand(this);
            Notify(l => l.OnPlayerTurnEnd(this));
        }

        void EndTurnCleanup()
        {
            if (Player.IsClarity) State.Stats.ClarityTurns++;
            foreach (var c in Player.Hand.ToList())
            {
                if (c.Has(CardKeywords.Retain)) SetTempCost(c, -1);
                else MoveCard(c, c.Ethereal ? PileKind.Exhaust : PileKind.Discard);
            }
            TickStatuses(Player);
        }

        void BeginEnemyTurn()
        {
            State.EnemyTurn = true;
            var steps = State.Enemies.ToList().Select(en => (Action)(() => EnemyAct(en))).ToList();
            steps.Add(() =>
            {
                State.EnemyTurn = false;
                PushFlow(StartPlayerTurn);
            });
            PushFlow(steps.ToArray());
        }

        void EnemyAct(EnemyState en)
        {
            if (en.IsDead) return;
            if (en.Statuses.Get(StatusId.Bleed) > 0)
            {
                // 流血在行动前结算（策划/14 §1.4）；它触发的效果（嗜血……）结算完、确认还活着，才轮到它出招
                Queue.AddToBottom(new BleedAction(en));
                PushFlow(() => EnemyTakeTurn(en));
                return;
            }
            EnemyTakeTurn(en);
        }

        void EnemyTakeTurn(EnemyState en)
        {
            if (en.IsDead) return;
            SetBlock(en, 0);
            if (en.Stunned)
            {
                SetStunned(en, false);
                Log.Add(BattleEventType.EnemyStunSkipped, $"{en.Name} 被震慑，跳过行动", en.Name).Entity = en.Index;
            }
            else
            {
                var move = en.Intent;
                en.MoveHistory.Add(move.Id);
                en.MoveUses[move.Id] = en.UsesOf(move.Id) + 1;
                var ev = Log.Add(BattleEventType.EnemyMove, $"{en.Name} 使用「{move.Name}」", en.Name);
                ev.Entity = en.Index;
                ev.Id = move.Id;
                en.Behaviour.Perform(this, en, move);
            }
            PushFlow(() =>
            {
                TickStatuses(en);
                en.Behaviour.OnOwnTurnEnd(this, en);
                if (!en.IsDead) RollIntent(en);
            });
        }

        internal void RollIntent(EnemyState en)
        {
            en.Intent = en.Behaviour.ChooseNext(this, en);
            var ev = Log.Add(BattleEventType.IntentChosen, $"{en.Name} 的意图：{en.Intent.Name}", en.Name);
            ev.Entity = en.Index;
            ev.Id = en.Intent.Id;
        }

        // ------------------------------------------------------------------ 状态修改（都会写状态事件）

        public Card NewCard(string id) => CardLibrary.Create(id, _nextUid++);

        /// <summary>设置生命（夹在 0..最大生命）。返回这次修改是否让它倒下。</summary>
        internal bool SetHp(Combatant c, int hp)
        {
            hp = Math.Max(0, Math.Min(c.MaxHp, hp));
            if (hp == c.Hp) return false;
            bool wasAlive = !c.IsDead;
            c.Hp = hp;
            Log.State(BattleEventType.HpChanged, c.EntityId, hp, value2: c.MaxHp);
            return wasAlive && c.IsDead;
        }

        internal void SetBlock(Combatant c, int block)
        {
            block = Math.Max(0, block);
            if (block == c.Block) return;
            c.Block = block;
            Log.State(BattleEventType.BlockChanged, c.EntityId, block);
        }

        internal void SetEnergy(int energy)
        {
            if (energy == Player.Energy) return;
            Player.Energy = energy;
            Log.State(BattleEventType.EnergyChanged, BattleEvent.PlayerEntity, energy, value2: Player.EnergyNextTurn);
        }

        internal void SetEnergyNextTurn(int energy)
        {
            if (energy == Player.EnergyNextTurn) return;
            Player.EnergyNextTurn = energy;
            Log.State(BattleEventType.EnergyChanged, BattleEvent.PlayerEntity, Player.Energy, value2: energy);
        }

        internal void SetSanity(int sanity)
        {
            sanity = Math.Max(0, Math.Min(Player.MaxSanity, sanity));
            if (sanity == Player.Sanity) return;
            Player.Sanity = sanity;
            Log.State(BattleEventType.SanityChanged, BattleEvent.PlayerEntity, sanity, value2: Player.MaxSanity);
        }

        internal void SetStatus(Combatant c, StatusId id, int amount)
        {
            if (c.Statuses.Get(id) == amount) return;
            c.Statuses.Set(id, amount);
            var ev = Log.State(BattleEventType.StatusChanged, c.EntityId, amount);
            ev.Status = id;
        }

        internal void AddStatus(Combatant c, StatusId id, int delta) => SetStatus(c, id, c.Statuses.Get(id) + delta);

        /// <summary>回合制状态（易伤 / 虚弱 / 脆弱）在持有者回合结束时 −1。</summary>
        internal void TickStatuses(Combatant c)
        {
            foreach (var id in StatusSet.TurnBased)
                if (c.Statuses.Get(id) > 0) AddStatus(c, id, -1);
        }

        internal void SetFear(EnemyState en, int fear, int will)
        {
            if (fear == en.Fear && will == en.Will) return;
            en.Fear = fear;
            en.Will = will;
            Log.State(BattleEventType.FearChanged, en.Index, fear, value2: will);
        }

        internal void SetStunned(EnemyState en, bool stunned)
        {
            if (en.Stunned == stunned) return;
            en.Stunned = stunned;
            Log.State(BattleEventType.StunChanged, en.Index, stunned ? 1 : 0);
        }

        public void SetCounter(EnemyState en, string key, int value)
        {
            if (en.Counter(key) == value && en.Counters.ContainsKey(key)) return;
            en.Counters[key] = value;
            Log.State(BattleEventType.CounterChanged, en.Index, value, key);
        }

        public void SetIntentsRevealed(bool revealed)
        {
            if (State.IntentsRevealedThisTurn == revealed) return;
            State.IntentsRevealedThisTurn = revealed;
            Log.State(BattleEventType.IntentRevealChanged, BattleEvent.PlayerEntity, revealed ? 1 : 0);
        }

        internal void SetRoundFlags(bool transformed, bool reverted)
        {
            if (State.TransformedThisRound == transformed && State.RevertedThisRound == reverted) return;
            State.TransformedThisRound = transformed;
            State.RevertedThisRound = reverted;
            Log.State(BattleEventType.RoundFlagsChanged, BattleEvent.PlayerEntity, (transformed ? 1 : 0) | (reverted ? 2 : 0));
        }

        /// <summary>
        /// 理智跨过癫狂线（策划/01 §2.2）：写叙述事件，通知 OnLunacyChanged（每次都通知）。
        /// 每轮第一次进入 / 离开癫狂另外通知 OnTransform / OnRevert——狼人的变身 / 复归（策划/14 §1.2）。
        /// 崩溃把理智重置到 3 时 counts = false：那不是玩家或敌人推过去的，不算复归。
        /// </summary>
        internal void LunacyCrossed(bool entered, bool counts = true)
        {
            if (entered)
            {
                if (State.Stats.FirstLunacyTurn == 0) State.Stats.FirstLunacyTurn = State.Turn;
                Log.Add(BattleEventType.LunacyEntered, "进入癫狂：双面牌翻到癫狂面，敌人意图变得模糊");
            }
            else Log.Add(BattleEventType.LunacyLeft, "离开癫狂：双面牌翻回清醒面");
            Notify(l => l.OnLunacyChanged(this, entered));
            if (!counts) return;

            if (entered)
            {
                if (State.TransformedThisRound) return;
                SetRoundFlags(true, State.RevertedThisRound);
                State.Stats.Transforms++;
                Notify(l => l.OnTransform(this));
            }
            else
            {
                if (State.RevertedThisRound) return;
                SetRoundFlags(State.TransformedThisRound, true);
                State.Stats.Reverts++;
                Notify(l => l.OnRevert(this));
            }
        }

        /// <summary>按 Uid 找手牌 / 牌堆里的牌，找不到返回 null。</summary>
        public Card FindCard(int uid)
        {
            foreach (var pile in Piles)
                foreach (var c in PileList(pile))
                    if (c.Uid == uid) return c;
            return null;
        }

        internal void SetPotion(int slot, Potion potion)
        {
            Player.Potions[slot] = potion;
            Log.State(BattleEventType.PotionChanged, BattleEvent.PlayerEntity, slot, potion?.Id);
        }

        internal void SetTempCost(Card card, int cost)
        {
            if (card.CostThisTurn == cost) return;
            card.CostThisTurn = cost;
            var ev = Log.State(BattleEventType.CardCostChanged, BattleEvent.NoEntity, cost);
            ev.CardUid = card.Uid;
        }

        /// <summary>叠加能力：同 ID 的能力增加 Amount，否则加入并开始监听。</summary>
        internal Power ApplyPower(Power power, int amount)
        {
            var existing = Player.FindPower(power.Id);
            if (existing != null)
            {
                existing.Amount += amount;
                power = existing;
            }
            else
            {
                power.Amount = amount;
                Player.Powers.Add(power);
                AddListener(power);
            }
            Log.State(BattleEventType.PowerChanged, BattleEvent.PlayerEntity, power.Amount, power.Id);
            return power;
        }

        static readonly PileKind[] Piles = { PileKind.Hand, PileKind.Draw, PileKind.Discard, PileKind.Exhaust };

        List<Card> PileList(PileKind pile)
        {
            switch (pile)
            {
                case PileKind.Hand: return Player.Hand;
                case PileKind.Draw: return Player.DrawPile;
                case PileKind.Discard: return Player.DiscardPile;
                case PileKind.Exhaust: return Player.ExhaustPile;
                default: return null;
            }
        }

        /// <summary>这张牌现在在哪儿（测试会直接往牌堆里塞牌，所以按实际列表查找）。</summary>
        public PileKind Locate(Card card)
        {
            foreach (var pile in Piles)
                if (PileList(pile).Contains(card)) return pile;
            return card.Pile == PileKind.Play ? PileKind.Play : PileKind.None;
        }

        /// <summary>
        /// 移动一张牌。放进抽牌堆时 index 是插入位置（-1 = 牌堆顶，即列表末尾）。
        /// 离开手牌时清掉「本回合费用」。手牌上限由调用方处理。
        /// </summary>
        internal void MoveCard(Card card, PileKind to, int index = -1)
        {
            var from = Locate(card);
            PileList(from)?.Remove(card);
            if (from == PileKind.Hand && to != PileKind.Hand) SetTempCost(card, -1);
            var list = PileList(to);
            if (list != null)
            {
                if (index < 0 || index > list.Count) index = list.Count;
                list.Insert(index, card);
            }
            card.Pile = to;
            var ev = Log.State(BattleEventType.CardMoved, BattleEvent.NoEntity, index, card.DeckId);
            ev.CardUid = card.Uid;
            ev.From = from;
            ev.To = to;
        }

        /// <summary>弃牌堆整个洗回抽牌堆。</summary>
        internal void ShuffleDiscardIntoDraw()
        {
            var p = Player;
            foreach (var c in p.DiscardPile) c.Pile = PileKind.Draw;
            p.DrawPile.AddRange(p.DiscardPile);
            p.DiscardPile.Clear();
            Rng.Shuffle.Shuffle(p.DrawPile);
            Log.State(BattleEventType.PileShuffled, BattleEvent.NoEntity, p.DrawPile.Count);
            Log.Add(BattleEventType.Shuffled, "弃牌堆洗回抽牌堆");
        }

        /// <summary>召唤 / 生成敌人：追加在列表末尾，已有敌人的下标不变。生命按定义随机（misc 流）。</summary>
        public EnemyState SpawnEnemy(string defId, bool minion, string name = null)
        {
            var def = EnemyLibrary.Get(defId);
            int hp = Rng.Misc.Range(def.HpMin, def.HpMax);
            var en = new EnemyState
            {
                Index = State.Enemies.Count,
                Def = def,
                Name = name ?? def.Name,
                Behaviour = EnemyLibrary.CreateBehaviour(def),
                MaxHp = hp,
                Hp = hp,
                Will = def.Will,
                Mindless = def.Will <= 0,
                Minion = minion,
            };
            State.Enemies.Add(en);
            // Value = 生命，Value2 = 最大生命，Amount = 意志，Actor = 显示名，Flag = 召唤物
            var ev = Log.State(BattleEventType.EnemySpawned, en.Index, hp, def.Id, en.MaxHp);
            ev.Actor = en.Name;
            ev.Amount = en.Will;
            ev.Flag = minion;
            en.Behaviour.OnSpawned(this, en);
            return en;
        }

        // ------------------------------------------------------------------ 结算工具（供动作调用）

        /// <summary>造成伤害（先扣格挡）。返回实际扣掉的生命。</summary>
        internal int ApplyDamage(Combatant source, Combatant target, int amount)
        {
            int absorbed = Math.Min(target.Block, amount);
            SetBlock(target, target.Block - absorbed);
            int hpLoss = amount - absorbed;
            bool died = SetHp(target, target.Hp - hpLoss);
            string src = source?.Name ?? "效果";
            var ev = Log.Add(BattleEventType.Damage,
                absorbed > 0
                    ? $"{src} 对 {target.Name} 造成 {amount} 伤害（格挡吸收 {absorbed}）→ 生命 {target.Hp}"
                    : $"{src} 对 {target.Name} 造成 {amount} 伤害 → 生命 {target.Hp}",
                src, target.Name, amount);
            ev.Entity = target.EntityId;
            ev.Source = source?.EntityId ?? BattleEvent.NoEntity;
            ev.Value = hpLoss;
            AfterHpLoss(target, died, hpLoss);
            return hpLoss;
        }

        /// <summary>攻击伤害的一段打完之后通知 OnAttackHit（倒刺爪）。效果伤害不通知。</summary>
        internal void AttackHit(Combatant source, Combatant target, int hpLoss) =>
            Notify(l => l.OnAttackHit(this, source, target, hpLoss));

        /// <summary>永久增加最大生命，同时回复同样多的生命（饱餐）。跑局层在战斗结束时抄最大生命。</summary>
        internal void GainMaxHp(int amount)
        {
            if (amount <= 0) return;
            Player.MaxHp += amount;
            Player.Hp += amount;
            State.Stats.MaxHpGained += amount;
            Log.State(BattleEventType.HpChanged, Player.EntityId, Player.Hp, value2: Player.MaxHp);
            Log.Add(BattleEventType.Healed, $"最大生命 +{amount} → {Player.Hp}/{Player.MaxHp}", target: Player.Name, amount: amount).Entity = Player.EntityId;
        }

        internal void LoseHp(Combatant target, int amount)
        {
            if (amount <= 0 || target.IsDead) return;
            bool died = SetHp(target, target.Hp - amount);
            var ev = Log.Add(BattleEventType.HpLost, $"{target.Name} 失去 {amount} 生命 → {target.Hp}", target: target.Name, amount: amount);
            ev.Entity = target.EntityId;
            AfterHpLoss(target, died, amount);
        }

        internal void Heal(Combatant target, int amount)
        {
            if (amount <= 0 || target.IsDead) return;
            int before = target.Hp;
            SetHp(target, target.Hp + amount);
            int healed = target.Hp - before;
            if (healed <= 0) return;
            var ev = Log.Add(BattleEventType.Healed, $"{target.Name} 恢复 {healed} 生命 → {target.Hp}", target: target.Name, amount: healed);
            ev.Entity = target.EntityId;
        }

        void AfterHpLoss(Combatant target, bool died, int hpLoss)
        {
            if (!(target is EnemyState en)) return;
            if (died)
            {
                Log.Add(BattleEventType.EnemyDied, $"{en.Name} 被击倒", target: en.Name).Entity = en.Index;
                SetStunned(en, false);
                foreach (var other in State.Enemies.ToList())
                    if (!other.IsDead) other.Behaviour.OnEnemyDied(this, other, en);
                Notify(l => l.OnEnemyDied(this, en));
            }
            else if (hpLoss > 0) en.Behaviour.OnDamaged(this, en);
        }

        /// <summary>抽一张牌；抽牌堆空了就洗弃牌堆。手牌满时这张牌直接进弃牌堆。没有牌可抽时返回 null。</summary>
        internal Card DrawOne()
        {
            var p = Player;
            if (p.DrawPile.Count == 0)
            {
                if (p.DiscardPile.Count == 0) return null;
                ShuffleDiscardIntoDraw();
            }
            var card = p.DrawPile[p.DrawPile.Count - 1];
            if (p.Hand.Count >= Config.HandLimit)
            {
                MoveCard(card, PileKind.Discard);
                Log.Add(BattleEventType.CardDiscarded, $"手牌已满，{card.Name} 进入弃牌堆", target: card.Name).CardUid = card.Uid;
            }
            else MoveCard(card, PileKind.Hand);
            return card;
        }

        // ------------------------------------------------------------------ 查询（供表现层 / 控制台）

        public IntentView GetIntent(EnemyState en)
        {
            var m = en.Intent;
            bool hidden = en.Behaviour.IntentHidden(this, en);
            var view = new IntentView
            {
                Type = hidden ? IntentType.Unknown : m?.Intent ?? IntentType.Unknown,
                MoveName = hidden ? "?" : m?.Name ?? "?",
                Note = hidden ? null : m?.Note,
                Stunned = en.Stunned,
                Hidden = hidden,
                Blurred = IntentsBlurred,
            };
            if (m != null && !hidden)
            {
                view.Hits = en.Behaviour.IntentHits(this, en, m);
                view.SanityDamage = m.SanityDamage;
                view.Block = m.Block;
                int dmg = en.Behaviour.IntentDamage(this, en, m);
                if (dmg > 0 && view.Hits > 0) view.Damage = DamageCalculator.Compute(this, en, Player, dmg);
            }
            return view;
        }

        /// <summary>本回合敌人预计造成的攻击伤害总和（不含被震慑的敌人；意图隐藏的敌人也按真实招式算）。</summary>
        public int IncomingDamage()
        {
            int sum = 0;
            foreach (var en in State.AliveEnemies)
            {
                if (en.Stunned || en.Intent == null) continue;
                int dmg = en.Behaviour.IntentDamage(this, en, en.Intent);
                if (dmg <= 0) continue;
                sum += DamageCalculator.Compute(this, en, Player, dmg) * en.Behaviour.IntentHits(this, en, en.Intent);
            }
            return sum;
        }

        /// <summary>
        /// 下个玩家回合开始时预计失去的理智：侵蚀 + 敌人光环（刺目强光、深海之歌）。理智条的「侵蚀预告」用（策划/10 §6.2），
        /// 不扣心防：心防能不能挡住光环要到时才知道，预告按最坏情况显示。
        /// </summary>
        public (int Erosion, int Aura) UpcomingSanityLoss()
        {
            int erosion = State.Turn + 1 >= Config.ErosionStartTurn ? Config.ErosionAmount : 0;
            int aura = 0;
            foreach (var en in State.AliveEnemies) aura += en.Behaviour.TurnStartSanityLoss(en);
            return (erosion, aura);
        }

        /// <summary>现在结束回合，手牌里的牌（疯语）会让你失去的理智。理智条的「疯语预告」用，按当前手牌算，出牌、抽牌后要重新取。</summary>
        public int TurnEndSanityLoss()
        {
            int sum = 0;
            foreach (var c in Player.Hand) sum += c.TurnEndSanityLoss;
            return sum;
        }

        /// <summary>某个实体当前的显示名（事件里只有编号时用）。</summary>
        public string NameOf(int entity) =>
            entity == BattleEvent.PlayerEntity ? Player.Name : entity >= 0 && entity < State.Enemies.Count ? State.Enemies[entity].Name : "";
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
        /// <summary>癫狂导致的模糊：只显示类型。</summary>
        public bool Blurred;
        /// <summary>敌人自身的隐藏（无面修女）：连类型都看不到，显示「?」。</summary>
        public bool Hidden;
    }
}
