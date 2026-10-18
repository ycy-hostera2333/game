using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Deeptide.Core.Cards;
using Deeptide.Core.Content;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Potions;
using Deeptide.Core.Random;
using Deeptide.Core.Relics;

namespace Deeptide.Core.Run
{
    /// <summary>开局参数（策划/03 §1、06）。</summary>
    public sealed class RunConfig
    {
        public int MaxHp = 70;
        public int MaxSanity = 10;
        public int Gold = 99;
        public int PotionSlots = 3;
        public List<string> Deck = TestDecks.Starter.ToList();
        public List<string> Relics = new List<string> { RelicIds.InvestigatorNotes };
        public BattleConfig Battle = new BattleConfig();
        public MapConfig Map = new MapConfig();
    }

    /// <summary>当前界面上的一个选项。Do 只在 Execute 时调用，选项列表每次都从状态重新算出来，所以不需要存档。</summary>
    public sealed class RunOption
    {
        public string Label;
        public bool Enabled = true;
        /// <summary>不可选的原因（金币不够……）。</summary>
        public string Reason;
        /// <summary>选项对应的卡牌（带 + 表示升级），表现层用来画卡面。</summary>
        public string CardId;
        /// <summary>
        /// 给程序看的标签（AI 和表现层用）：node:房间类型、gold、relic:ID、potion:ID、cards、leave、card:ID、deck:下标、skip、
        /// shop:card:ID / shop:relic:ID / shop:potion:ID、remove、rest、smith、meditate、purge、drop:栏位、decline；事件选项是选项 ID。
        /// </summary>
        public string Tag;
        internal Action Do;
    }

    /// <summary>
    /// 跑局内核（策划/09 M4）：持有 RunState 和当前战斗，对外只接受 RunCommand。
    /// 存档点：进入节点时（节点内容已生成）、战斗结束时、回到地图时；战斗中退出再读档，战斗从头开始（策划/06 §8）。
    /// </summary>
    public sealed partial class RunEngine
    {
        public const int SaveVersion = 1;

        public readonly RunConfig Config;
        public RunState State { get; private set; }
        public RunRng Rng { get; private set; }
        /// <summary>当前战斗（Phase = Battle 时）。</summary>
        public BattleEngine Battle { get; private set; }
        /// <summary>本次运行里执行过的命令（读档后从空开始）。</summary>
        public readonly List<RunCommand> History = new List<RunCommand>();
        /// <summary>每到一个存档点 +1。外层看到它变了就调用 SaveState() 写档。</summary>
        public int SavePoints { get; private set; }

        RunEngine(RunConfig config) { Config = config ?? new RunConfig(); }

        public static RunEngine NewRun(ulong seed, RunConfig config = null)
        {
            var e = new RunEngine(config);
            var c = e.Config;
            e.Rng = new RunRng(seed);
            e.State = new RunState
            {
                Seed = seed,
                MaxHp = c.MaxHp,
                Hp = c.MaxHp,
                MaxSanity = c.MaxSanity,
                Gold = c.Gold,
                Deck = c.Deck.ToList(),
                Relics = c.Relics.ToList(),
                Potions = Enumerable.Repeat<string>(null, c.PotionSlots).ToList(),
                Map = MapGenerator.Generate(e.Rng.Map, c.Map),
                Phase = RunPhase.Map,
            };
            e.SavePoint();
            return e;
        }

        /// <summary>从存档恢复。state 由外层反序列化得到，读入后归这个引擎所有。</summary>
        public static RunEngine Load(RunState state, RunConfig config = null)
        {
            if (state.Version != SaveVersion) throw new InvalidOperationException($"存档版本 {state.Version} 和当前版本 {SaveVersion} 不一致");
            var e = new RunEngine(config) { State = state, Rng = RunRng.Load(state.Rng) };
            if (state.Phase == RunPhase.Battle) e.CreateBattle();
            return e;
        }

        public static RunEngine Replay(ulong seed, IEnumerable<RunCommand> commands, RunConfig config = null)
        {
            var e = NewRun(seed, config);
            foreach (var cmd in commands)
                if (!e.Execute(cmd, out var error))
                    throw new InvalidOperationException($"整局回放失败：{cmd}（{error}）");
            return e;
        }

        /// <summary>把随机流状态写回 State 并返回它：调用方应立即序列化（State 之后还会继续变化）。</summary>
        public RunState SaveState()
        {
            State.Rng = Rng.Save();
            return State;
        }

        void SavePoint()
        {
            State.Rng = Rng.Save();
            SavePoints++;
        }

        public bool IsOver => State.Phase == RunPhase.Victory || State.Phase == RunPhase.Defeat;

        // ------------------------------------------------------------------ 命令

        public bool Execute(RunCommand cmd, out string error)
        {
            bool ok = TryExecute(cmd, out error);
            if (ok) History.Add(cmd);
            return ok;
        }

        bool TryExecute(RunCommand cmd, out string error)
        {
            error = null;
            if (IsOver) { error = "这一局已经结束"; return false; }
            switch (cmd.Kind)
            {
                case RunCommandKind.Battle:
                    if (State.Phase != RunPhase.Battle || Battle == null) { error = "现在不在战斗中"; return false; }
                    if (!Battle.Execute(cmd.Battle, out error)) return false;
                    if (Battle.State.Outcome != BattleOutcome.Ongoing) FinishBattle();
                    return true;

                case RunCommandKind.DropPotion:
                    if (State.Phase == RunPhase.Battle) { error = "战斗中不能丢药水"; return false; }
                    if (cmd.Index < 0 || cmd.Index >= State.Potions.Count || State.Potions[cmd.Index] == null) { error = "这个药水栏是空的"; return false; }
                    State.Potions[cmd.Index] = null;
                    return true;

                default:
                    var options = Options();
                    if (cmd.Index < 0 || cmd.Index >= options.Count) { error = "没有这个选项"; return false; }
                    var opt = options[cmd.Index];
                    if (!opt.Enabled) { error = opt.Reason ?? "现在不能选这一项"; return false; }
                    opt.Do();
                    return true;
            }
        }

        /// <summary>当前界面的标题。</summary>
        public string Title
        {
            get
            {
                switch (State.Phase)
                {
                    case RunPhase.Map: return State.Row < 0 ? "选择出发点" : $"第 {State.Floor} 层：选择下一个节点";
                    case RunPhase.Battle: return $"战斗：{EncounterLibrary.Get(State.Battle.EncounterId).Name}";
                    case RunPhase.Reward: return "奖励";
                    case RunPhase.Shop: return $"商店（金币 {State.Gold}）";
                    case RunPhase.Campfire: return "篝火";
                    case RunPhase.Event: return EventLibrary.Get(State.Event.Id).Name;
                    case RunPhase.Select: return State.Select.Title;
                    case RunPhase.Victory: return "溺亡司祭沉入深渊。Demo 通关！";
                    default: return "调查员倒下了。";
                }
            }
        }

        /// <summary>当前界面的说明文字（事件描述），没有返回 null。</summary>
        public string Description => State.Phase == RunPhase.Event ? EventLibrary.Create(State.Event.Id).Description : null;

        /// <summary>当前界面的选项，顺序固定（Pick 的下标就是这个列表的下标）。战斗中返回空列表。</summary>
        public List<RunOption> Options()
        {
            switch (State.Phase)
            {
                case RunPhase.Map: return MapOptions();
                case RunPhase.Reward: return RewardOptions();
                case RunPhase.Shop: return ShopOptions();
                case RunPhase.Campfire: return CampfireOptions();
                case RunPhase.Event: return EventOptions();
                case RunPhase.Select: return SelectOptions();
                default: return new List<RunOption>();
            }
        }

        // ------------------------------------------------------------------ 地图

        public IEnumerable<MapNode> NextNodes()
        {
            var map = State.Map;
            if (State.Row < 0) return map.InRow(0);
            if (State.Row >= map.Height - 1) return Enumerable.Empty<MapNode>();
            var cur = map.Get(State.Row, State.Col);
            return cur.Children.Select(c => map.Get(State.Row + 1, c));
        }

        List<RunOption> MapOptions()
        {
            var options = NextNodes().Select(n => new RunOption
            {
                Label = $"第 {n.Floor} 层 · {RoomName(n.Room)}（第 {n.Col + 1} 列）",
                Tag = "node:" + n.Room,
                Do = () => EnterNode(n.Row, n.Col, n.Room),
            }).ToList();
            if (State.Row == State.Map.Height - 1)
                options.Add(new RunOption { Label = $"第 {State.Map.BossFloor} 层 · Boss", Tag = "node:Boss", Do = () => EnterNode(State.Map.Height, 0, RoomType.Boss) });
            return options;
        }

        public static string RoomName(RoomType room)
        {
            switch (room)
            {
                case RoomType.Battle: return "普通战斗";
                case RoomType.Event: return "事件";
                case RoomType.Campfire: return "篝火";
                case RoomType.Elite: return "精英";
                case RoomType.Shop: return "商店";
                case RoomType.Treasure: return "宝箱";
                default: return "Boss";
            }
        }

        internal void EnterNode(int row, int col, RoomType room)
        {
            State.Row = row;
            State.Col = col;
            State.Floor = row + 1;
            State.Stats.NodeHp.Add(State.Hp);
            switch (room)
            {
                case RoomType.Battle:
                    State.NormalFights++;
                    StartBattle(PickEncounter(State.NormalFights <= 3 ? EncounterPool.Easy : EncounterPool.Hard), room);
                    break;
                case RoomType.Elite:
                    StartBattle(PickElite(), room);
                    break;
                case RoomType.Boss:
                    State.Stats.HpAtBoss = State.Hp;
                    StartBattle(EncounterLibrary.InPool(EncounterPool.Boss).First().Id, room);
                    break;
                case RoomType.Event:
                    var ev = PickEvent();
                    if (ev == null)
                    {
                        State.NormalFights++;
                        StartBattle(PickEncounter(EncounterPool.Hard), RoomType.Battle);
                        break;
                    }
                    State.EventsSeen.Add(ev.Id);
                    State.Event = new EventState { Id = ev.Id };
                    State.Phase = RunPhase.Event;
                    break;
                case RoomType.Shop:
                    State.Stats.ShopVisits++;
                    State.Shop = GenerateShop();
                    State.Phase = RunPhase.Shop;
                    break;
                case RoomType.Campfire:
                    State.Phase = RunPhase.Campfire;
                    break;
                case RoomType.Treasure:
                    State.Rewards = new List<RewardItem>();
                    AddRelicReward(RollRelicRarity(Rng.Reward));
                    State.Rewards.Add(new RewardItem { Kind = RewardKind.Gold, Amount = Rng.Reward.Range(25, 50) });
                    State.Phase = RunPhase.Reward;
                    break;
            }
            SavePoint();
        }

        internal string PickEncounter(EncounterPool pool)
        {
            var last = State.RecentEncounters.LastOrDefault();
            var options = EncounterLibrary.InPool(pool).Where(x => x.Id != last).ToList();
            var id = options[Rng.Encounter.NextInt(options.Count)].Id;
            State.RecentEncounters.Add(id);
            return id;
        }

        string PickElite()
        {
            var all = EncounterLibrary.InPool(EncounterPool.Elite).ToList();
            var fresh = all.Where(x => !State.ElitesSeen.Contains(x.Id)).ToList();
            var id = (fresh.Count > 0 ? fresh : all)[Rng.Encounter.NextInt((fresh.Count > 0 ? fresh : all).Count)].Id;
            State.ElitesSeen.Add(id);
            return id;
        }

        EventDef PickEvent()
        {
            var options = EventLibrary.All.Where(d => !State.EventsSeen.Contains(d.Id) && State.Floor >= d.MinFloor).ToList();
            return options.Count == 0 ? null : options[Rng.Event.NextInt(options.Count)];
        }

        internal void GoToMap()
        {
            if (IsOver) return;
            State.Phase = RunPhase.Map;
            State.Rewards = new List<RewardItem>();
            State.Shop = null;
            State.Event = null;
            State.Select = null;
            SavePoint();
        }

        // ------------------------------------------------------------------ 战斗

        internal void StartBattle(string encounterId, RoomType room)
        {
            State.BattlesFought++;
            State.Battle = new BattleInfo
            {
                EncounterId = encounterId,
                Seed = Pcg32.Mix(State.Seed, (ulong)State.BattlesFought),
                Room = room,
            };
            State.Phase = RunPhase.Battle;
            CreateBattle();
        }

        void CreateBattle()
        {
            var info = State.Battle;
            Battle = BattleEngine.Start(new BattleSetup
            {
                Seed = info.Seed,
                Deck = State.Deck.ToList(),
                Enemies = EncounterLibrary.Get(info.EncounterId).Enemies.ToList(),
                MaxHp = State.MaxHp,
                Hp = State.Hp,
                MaxSanity = State.MaxSanity,
                Relics = State.Relics.ToList(),
                Potions = State.Potions.ToList(),
                PotionSlots = State.Potions.Count,
                Config = Config.Battle,
            });
        }

        void FinishBattle()
        {
            var b = Battle;
            var info = State.Battle;
            var s = b.State.Stats;
            bool won = b.State.Outcome == BattleOutcome.Victory;
            State.Stats.Battles.Add(new BattleRecord
            {
                Floor = State.Floor,
                EncounterId = info.EncounterId,
                Room = info.Room,
                Won = won,
                Turns = b.State.Turn,
                HpLost = State.Hp - b.Player.Hp,
                MinSanity = s.MinSanity,
                Collapses = s.Collapses,
                LunacyTurns = s.LunacyTurns,
                ClarityTurns = s.ClarityTurns,
                EnteredLunacy = s.FirstLunacyTurn > 0,
                PotionsUsed = s.PotionsUsed,
                EnemyHpLeft = b.State.Enemies.Where(x => !x.Minion).Sum(x => x.Hp),
                PlaysByCard = new Dictionary<string, int>(s.PlaysByCard),
            });
            State.Hp = b.Player.Hp;
            State.Potions = b.Player.Potions.Select(p => p?.Id).ToList();
            for (int i = 0; i < s.CursesGained; i++) State.Deck.Add(CardIds.Whispers);
            Battle = null;
            State.Battle = null;

            if (!won)
            {
                State.Stats.DeathEncounter = info.EncounterId;
                State.Phase = RunPhase.Defeat;
            }
            else if (info.Room == RoomType.Boss) State.Phase = RunPhase.Victory;
            else
            {
                GenerateBattleRewards(info.Room == RoomType.Elite);
                State.Phase = RunPhase.Reward;
            }
            // 打完就存一次：在奖励界面退出再读档，不会回到已经打赢的战斗
            SavePoint();
        }

        // ------------------------------------------------------------------ 牌组 / 遗物 / 药水（事件和商店共用）

        public bool HasRelic(string id) => State.Relics.Contains(id);

        internal void AddRelic(string id)
        {
            State.Relics.Add(id);
            if (id == RelicIds.AlchemistBelt) State.Potions.Add(null);
        }

        /// <summary>放进空药水栏；没有空位返回 false。</summary>
        internal bool TryAddPotion(string id)
        {
            int slot = State.Potions.IndexOf(null);
            if (slot < 0) return false;
            State.Potions[slot] = id;
            return true;
        }

        internal void AddPotionOrAskDiscard(string id, RunPhase returnTo)
        {
            if (TryAddPotion(id)) return;
            State.Select = new SelectState
            {
                Purpose = SelectPurpose.DiscardPotion,
                Title = $"药水栏满了：丢掉一瓶换成 {PotionLibrary.Get(id).Name}？",
                PendingPotion = id,
                Return = returnTo,
                SkipReturn = returnTo,
            };
            State.Phase = RunPhase.Select;
        }

        public static bool IsUpgradable(string deckId)
        {
            if (deckId.EndsWith("+", StringComparison.Ordinal)) return false;
            var t = CardLibrary.Get(deckId).Type;
            return t != CardType.Curse && t != CardType.Status;
        }

        internal void Upgrade(int deckIndex)
        {
            if (IsUpgradable(State.Deck[deckIndex])) State.Deck[deckIndex] += "+";
        }

        /// <summary>随机升级若干张可升级的牌（事件用 event 流）。</summary>
        internal List<string> UpgradeRandom(int count, Pcg32 rng)
        {
            var done = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var candidates = Enumerable.Range(0, State.Deck.Count).Where(k => IsUpgradable(State.Deck[k])).ToList();
                if (candidates.Count == 0) break;
                int k2 = candidates[rng.NextInt(candidates.Count)];
                Upgrade(k2);
                done.Add(State.Deck[k2]);
            }
            return done;
        }

        internal void Heal(int amount) => State.Hp = Math.Min(State.MaxHp, State.Hp + Math.Max(0, amount));

        internal void LoseHp(int amount)
        {
            State.Hp = Math.Max(0, State.Hp - Math.Max(0, amount));
            if (State.Hp == 0) State.Phase = RunPhase.Defeat;
        }

        internal void ChangeMaxSanity(int delta) => State.MaxSanity = Math.Max(1, State.MaxSanity + delta);

        internal void LoseMaxHp(int amount)
        {
            State.MaxHp = Math.Max(1, State.MaxHp - amount);
            State.Hp = Math.Min(State.Hp, State.MaxHp);
        }

        internal void GainGold(int amount)
        {
            State.Gold += amount;
            State.Stats.GoldEarned += amount;
        }

        /// <summary>从牌组里选一张：删除 / 升级。indices 为空表示所有牌都可选。</summary>
        internal void AskDeckCard(SelectPurpose purpose, string title, RunPhase returnTo, RunPhase skipReturn, IEnumerable<int> indices = null,
                                  bool canSkip = true, int goldCost = 0, bool shopRemoval = false)
        {
            State.Select = new SelectState
            {
                Purpose = purpose,
                Title = title,
                DeckIndices = (indices ?? Enumerable.Range(0, State.Deck.Count)).ToList(),
                CanSkip = canSkip,
                GoldCost = goldCost,
                Return = returnTo,
                SkipReturn = skipReturn,
                ShopRemoval = shopRemoval,
            };
            State.Phase = RunPhase.Select;
        }

        /// <summary>从几张牌里选一张加入牌组（可以跳过）。</summary>
        internal void OfferCards(List<string> cards, string title, RunPhase returnTo, int rewardIndex = -1)
        {
            State.Select = new SelectState
            {
                Purpose = SelectPurpose.AddCard,
                Title = title,
                Cards = cards,
                Return = returnTo,
                SkipReturn = returnTo,
                RewardIndex = rewardIndex,
            };
            State.Phase = RunPhase.Select;
        }

        List<RunOption> SelectOptions()
        {
            var sel = State.Select;
            var options = new List<RunOption>();
            switch (sel.Purpose)
            {
                case SelectPurpose.AddCard:
                    foreach (var id in sel.Cards)
                    {
                        string card = id;
                        options.Add(new RunOption { Label = CardLabel(card), CardId = card, Tag = "card:" + card, Do = () => FinishSelect(card) });
                    }
                    options.Add(new RunOption { Label = "跳过", Tag = "skip", Do = () => FinishSelect(null) });
                    break;

                case SelectPurpose.RemoveCard:
                case SelectPurpose.UpgradeCard:
                    foreach (int k in sel.DeckIndices)
                    {
                        int index = k;
                        string card = State.Deck[k];
                        string label = sel.Purpose == SelectPurpose.UpgradeCard ? $"{CardLabel(card)}  →  升级后：{CardLabel(card + "+")}" : CardLabel(card);
                        options.Add(new RunOption { Label = label, CardId = card, Tag = "deck:" + index, Do = () => FinishSelect(null, index) });
                    }
                    if (sel.CanSkip) options.Add(new RunOption { Label = "取消", Tag = "skip", Do = () => FinishSelect(null, -1) });
                    break;

                case SelectPurpose.DiscardPotion:
                    for (int i = 0; i < State.Potions.Count; i++)
                    {
                        int slot = i;
                        options.Add(new RunOption
                        {
                            Label = $"丢掉 {PotionLibrary.Get(State.Potions[i]).Name}",
                            Tag = "drop:" + slot,
                            Do = () => { State.Potions[slot] = sel.PendingPotion; FinishSelect(null); },
                        });
                    }
                    options.Add(new RunOption { Label = $"放弃 {PotionLibrary.Get(sel.PendingPotion).Name}", Tag = "decline", Do = () => FinishSelect(null) });
                    break;
            }
            return options;
        }

        void FinishSelect(string addCard, int deckIndex = -1)
        {
            var sel = State.Select;
            bool picked = addCard != null || deckIndex >= 0;
            switch (sel.Purpose)
            {
                case SelectPurpose.AddCard:
                    if (addCard != null) State.Deck.Add(addCard);
                    State.Stats.CardChoices.Add(new CardChoiceRecord { Floor = State.Floor, Offered = sel.Cards.ToList(), Picked = addCard });
                    if (sel.RewardIndex >= 0) State.Rewards[sel.RewardIndex].Taken = true;
                    break;
                case SelectPurpose.RemoveCard when picked:
                    State.Deck.RemoveAt(deckIndex);
                    State.Gold -= sel.GoldCost;
                    State.Stats.CardsRemoved++;
                    if (sel.ShopRemoval)
                    {
                        State.Shop.RemovalUsed = true;
                        State.Removals++;
                    }
                    break;
                case SelectPurpose.UpgradeCard when picked:
                    Upgrade(deckIndex);
                    break;
            }
            State.Select = null;
            State.Phase = picked ? sel.Return : sel.SkipReturn;
            if (State.Phase == RunPhase.Map) GoToMap();
        }

        public static string CardLabel(string deckId)
        {
            var card = CardLibrary.Create(deckId, 0);
            string cost = card.Def.XCost ? "X" : card.Cost.ToString(CultureInfo.InvariantCulture);
            string dual = card.Def.Dual ? $" ◐ 癫狂面：{card.Describe(true)}" : "";
            return $"{card.Name}（{cost}费 {RarityName(card.Def.Rarity)} {card.Def.Archetype}）{card.Describe(false)}{dual}";
        }

        static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return "普通";
                case Rarity.Uncommon: return "罕见";
                case Rarity.Rare: return "稀有";
                case Rarity.Basic: return "基础";
                default: return "特殊";
            }
        }

        // ------------------------------------------------------------------ 篝火（策划/06 §6）

        List<RunOption> CampfireOptions()
        {
            int heal = State.MaxHp * 30 / 100;
            var upgradable = Enumerable.Range(0, State.Deck.Count).Where(k => IsUpgradable(State.Deck[k])).ToList();
            var options = new List<RunOption>
            {
                new RunOption { Label = $"休息：恢复 {heal} 生命（{State.Hp}/{State.MaxHp}）", Tag = "rest", Do = () => { Heal(heal); GoToMap(); } },
                new RunOption
                {
                    Label = "锻造：升级 1 张牌",
                    Tag = "smith",
                    Enabled = upgradable.Count > 0,
                    Reason = "没有可以升级的牌",
                    Do = () => AskDeckCard(SelectPurpose.UpgradeCard, "选 1 张牌升级", RunPhase.Map, RunPhase.Campfire, upgradable),
                },
                new RunOption { Label = $"静思：理智上限 +1（{State.MaxSanity} → {State.MaxSanity + 1}）", Tag = "meditate", Do = () => { ChangeMaxSanity(1); GoToMap(); } },
            };
            int whisper = State.Deck.IndexOf(CardIds.Whispers);
            if (whisper >= 0)
                options.Add(new RunOption { Label = "静思：移除 1 张疯语", Tag = "purge", Do = () => { State.Deck.RemoveAt(whisper); GoToMap(); } });
            return options;
        }

        // ------------------------------------------------------------------ 事件

        List<RunOption> EventOptions() => EventLibrary.Create(State.Event.Id).Options(this, State.Event);

        // ------------------------------------------------------------------ 哈希

        /// <summary>整局状态指纹：回放 / 读档测试用它判断两局是否完全一致。战斗中时把战斗状态也算进去。</summary>
        public string Hash()
        {
            var s = State;
            var sb = new StringBuilder();
            sb.Append(s.Phase).Append('|').Append(s.Row).Append(',').Append(s.Col).Append('|').Append(s.Floor)
              .Append("|hp ").Append(s.Hp).Append('/').Append(s.MaxHp).Append("|san ").Append(s.MaxSanity)
              .Append("|gold ").Append(s.Gold).Append("|pity ").Append(s.RarePity).Append("|pot% ").Append(s.PotionChance)
              .Append("|rm ").Append(s.Removals).Append("|fights ").Append(s.BattlesFought).Append('/').Append(s.NormalFights).Append('\n');
            sb.Append("deck ").Append(string.Join(",", s.Deck)).Append('\n');
            sb.Append("relics ").Append(string.Join(",", s.Relics)).Append('\n');
            sb.Append("potions ").Append(string.Join(",", s.Potions.Select(p => p ?? "-"))).Append('\n');
            sb.Append("seen ").Append(string.Join(",", s.RecentEncounters)).Append(';').Append(string.Join(",", s.ElitesSeen))
              .Append(';').Append(string.Join(",", s.EventsSeen)).Append('\n');
            foreach (var r in s.Rewards) sb.Append("reward ").Append(r.Kind).Append(' ').Append(r.Amount).Append(' ').Append(r.Id)
                .Append(' ').Append(string.Join(",", r.Cards)).Append(r.Taken ? " taken" : "").Append('\n');
            if (s.Shop != null)
                foreach (var it in s.Shop.Items) sb.Append("shop ").Append(it.Kind).Append(' ').Append(it.Id).Append(' ').Append(it.Price).Append(it.Sold ? " sold" : "").Append('\n');
            if (s.Event != null) sb.Append("event ").Append(s.Event.Id).Append(' ').Append(s.Event.Stage).Append('\n');
            if (s.Select != null) sb.Append("select ").Append(s.Select.Purpose).Append(' ').Append(string.Join(",", s.Select.Cards))
                .Append(' ').Append(string.Join(",", s.Select.DeckIndices)).Append('\n');
            sb.Append("rng ").Append(string.Join(",", Rng.Save())).Append('\n');
            if (Battle != null) sb.Append("battle ").Append(StateHasher.Describe(Battle));
            return Fnv(sb.ToString());
        }

        static string Fnv(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
