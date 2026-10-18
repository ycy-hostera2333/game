using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using Deeptide.Core.Map;
using Deeptide.Core.Relics;

namespace Deeptide.Core.Run
{
    /// <summary>事件静态数据，对应 Data/events.csv 的一行（策划/05 §3）。</summary>
    public sealed class EventDef
    {
        public string Id;
        public string Name;
        /// <summary>从第几层开始才会出现。</summary>
        public int MinFloor = 1;
        public Dictionary<string, int> Vars = new Dictionary<string, int>();
        public string Text;
        /// <summary>选项 ID → 选项文本（按书写顺序）。</summary>
        public List<KeyValuePair<string, string>> Options = new List<KeyValuePair<string, string>>();
        public Func<RunEvent> Factory;
    }

    /// <summary>
    /// 事件基类：选项文本来自 events.csv，效果写在子类的 Choose 里。每个事件至少有一个零风险的选项。
    /// 事件内部的随机都用 event 流。
    /// </summary>
    public abstract class RunEvent
    {
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        public EventDef Def { get; private set; }

        internal void Init(EventDef def) => Def = def;

        /// <summary>这个类处理的选项 ID（数据校验用：必须和 events.csv 的 options 列一致）。</summary>
        public abstract IEnumerable<string> Handles { get; }

        protected int V(string key)
        {
            if (!Def.Vars.TryGetValue(key, out var v)) throw new KeyNotFoundException($"{Def.Id} 缺少数值 {key}");
            return v;
        }

        public string Format(string text) => Placeholder.Replace(text ?? "", m => V(m.Groups[1].Value).ToString());

        public string Description => Format(Def.Text);

        /// <summary>当前阶段的选项。默认：阶段 0 列出表里的全部选项，CanChoose 决定能不能选。</summary>
        public virtual List<RunOption> Options(RunEngine run, EventState st) =>
            Def.Options.Select(kv =>
            {
                string id = kv.Key;
                bool ok = CanChoose(run, id, out var reason);
                return new RunOption { Label = Format(kv.Value), Enabled = ok, Reason = reason, Tag = id, Do = () => Choose(run, st, id) };
            }).ToList();

        protected virtual bool CanChoose(RunEngine run, string option, out string reason)
        {
            reason = null;
            return true;
        }

        protected abstract void Choose(RunEngine run, EventState st, string option);

        protected static bool Need(bool condition, string why, out string reason)
        {
            reason = condition ? null : why;
            return condition;
        }
    }

    public static class EventLibrary
    {
        static readonly Dictionary<string, EventDef> Defs = new Dictionary<string, EventDef>();
        static readonly List<EventDef> Ordered = new List<EventDef>();

        public static IReadOnlyList<EventDef> All => Ordered;

        public static EventDef Get(string id)
        {
            if (Defs.TryGetValue(id, out var def)) return def;
            if (Defs.Count == 0) throw new InvalidOperationException("事件数据还没有载入（先调用 GameData.Load）");
            throw new KeyNotFoundException($"未知事件：{id}");
        }

        public static RunEvent Create(string id)
        {
            var def = Get(id);
            var ev = def.Factory();
            ev.Init(def);
            return ev;
        }

        internal static void Clear()
        {
            Defs.Clear();
            Ordered.Clear();
        }

        internal static void Add(EventDef def)
        {
            Defs.Add(def.Id, def);
            Ordered.Add(def);
        }
    }

    /// <summary>事件类注册表（显式表，理由同 CardClasses）。</summary>
    public static class EventClasses
    {
        public static readonly IReadOnlyDictionary<string, Func<RunEvent>> Factories = new Dictionary<string, Func<RunEvent>>
        {
            ["forbidden_book"] = () => new ForbiddenBook(),
            ["drowning_sailor"] = () => new DrowningSailor(),
            ["seaside_altar"] = () => new SeasideAltar(),
            ["mirror"] = () => new MirrorReflection(),
            ["asylum_ward"] = () => new AsylumWard(),
            ["cult_gathering"] = () => new CultGathering(),
            ["fog_peddler"] = () => new FogPeddler(),
            ["dream_mentor"] = () => new DreamMentor(),
        };
    }

    static class DeckQueries
    {
        public static bool IsStrike(string id) => id == "B01" || id == "B01+";
    }

    /// <summary>1 禁书：研读（理智上限 −2，随机稀有牌）/ 摘抄（理智上限 −1，随机升级 2 张）/ 焚毁取暖（恢复生命）。</summary>
    public sealed class ForbiddenBook : RunEvent
    {
        public override IEnumerable<string> Handles => new[] { "Read", "Copy", "Burn" };

        protected override void Choose(RunEngine run, EventState st, string option)
        {
            switch (option)
            {
                case "Read":
                    run.ChangeMaxSanity(-V("ReadSanity"));
                    run.State.Deck.AddRange(run.RandomCards(run.Rng.Event, 1, d => d.Rarity == Rarity.Rare));
                    break;
                case "Copy":
                    run.ChangeMaxSanity(-V("CopySanity"));
                    run.UpgradeRandom(V("Upgrades"), run.Rng.Event);
                    break;
                default:
                    run.Heal(V("Heal"));
                    break;
            }
            run.GoToMap();
        }
    }

    /// <summary>2 溺水的水手：施救（失去生命，3 选 1 罕见牌）/ 搜刮尸体（金币 + 1 张疯语）/ 离开。</summary>
    public sealed class DrowningSailor : RunEvent
    {
        public override IEnumerable<string> Handles => new[] { "Rescue", "Loot", "Leave" };

        protected override bool CanChoose(RunEngine run, string option, out string reason) =>
            option != "Rescue" ? Need(true, null, out reason) : Need(run.State.Hp > V("HpCost"), "生命不够", out reason);

        protected override void Choose(RunEngine run, EventState st, string option)
        {
            switch (option)
            {
                case "Rescue":
                    run.LoseHp(V("HpCost"));
                    run.OfferCards(run.RandomCards(run.Rng.Event, V("Choices"), d => d.Rarity == Rarity.Uncommon), "水手塞给你一样东西：选 1 张牌", RunPhase.Map);
                    return;
                case "Loot":
                    run.GainGold(V("Gold"));
                    run.State.Deck.Add(CardIds.Whispers);
                    break;
            }
            run.GoToMap();
        }
    }

    /// <summary>3 海边祭坛：献血（失去生命，随机遗物）/ 献金（付金币，删 1 张牌）/ 离开。</summary>
    public sealed class SeasideAltar : RunEvent
    {
        public override IEnumerable<string> Handles => new[] { "Blood", "Gold", "Leave" };

        protected override bool CanChoose(RunEngine run, string option, out string reason)
        {
            if (option == "Blood") return Need(run.State.Hp > V("HpCost"), "生命不够", out reason);
            if (option == "Gold") return Need(run.State.Gold >= V("GoldCost"), "金币不够", out reason);
            return Need(true, null, out reason);
        }

        protected override void Choose(RunEngine run, EventState st, string option)
        {
            switch (option)
            {
                case "Blood":
                    run.LoseHp(V("HpCost"));
                    var relic = run.RollRelic(run.Rng.Event, RunEngine.RollRelicRarity(run.Rng.Event));
                    if (relic != null) run.AddRelic(relic);
                    break;
                case "Gold":
                    run.AskDeckCard(SelectPurpose.RemoveCard, "祭坛收下了金币：删除哪张牌？", RunPhase.Map, RunPhase.Map, canSkip: false, goldCost: V("GoldCost"));
                    return;
            }
            run.GoToMap();
        }
    }

    /// <summary>4 镜中倒影：凝视（理智上限 −1，随机升级 2 张）/ 打碎（失去生命，删 1 张牌）/ 离开。</summary>
    public sealed class MirrorReflection : RunEvent
    {
        public override IEnumerable<string> Handles => new[] { "Gaze", "Shatter", "Leave" };

        protected override bool CanChoose(RunEngine run, string option, out string reason) =>
            option != "Shatter" ? Need(true, null, out reason) : Need(run.State.Hp > V("HpCost"), "生命不够", out reason);

        protected override void Choose(RunEngine run, EventState st, string option)
        {
            switch (option)
            {
                case "Gaze":
                    run.ChangeMaxSanity(-V("Sanity"));
                    run.UpgradeRandom(V("Upgrades"), run.Rng.Event);
                    break;
                case "Shatter":
                    run.LoseHp(V("HpCost"));
                    run.AskDeckCard(SelectPurpose.RemoveCard, "镜子碎了：删除哪张牌？", RunPhase.Map, RunPhase.Map, canSkip: false);
                    return;
            }
            run.GoToMap();
        }
    }

    /// <summary>5 疯人院病房：接受治疗（付金币：理智上限 +1 或移除 1 张疯语）/ 偷取病历（理智上限 −1，3 选 1 双面牌）/ 离开。</summary>
    public sealed class AsylumWard : RunEvent
    {
        public override IEnumerable<string> Handles => new[] { "Treat", "Steal", "Leave" };

        public override List<RunOption> Options(RunEngine run, EventState st)
        {
            if (st.Stage != 1) return base.Options(run, st);
            int whisper = run.State.Deck.IndexOf(CardIds.Whispers);
            return new List<RunOption>
            {
                new RunOption { Label = $"理智上限 +{V("Sanity")}", Tag = "meditate", Do = () => { run.ChangeMaxSanity(V("Sanity")); run.GoToMap(); } },
                new RunOption { Label = "移除 1 张疯语", Tag = "purge", Do = () => { run.State.Deck.RemoveAt(whisper); run.GoToMap(); } },
            };
        }

        protected override bool CanChoose(RunEngine run, string option, out string reason) =>
            option != "Treat" ? Need(true, null, out reason) : Need(run.State.Gold >= V("GoldCost"), "金币不够", out reason);

        protected override void Choose(RunEngine run, EventState st, string option)
        {
            switch (option)
            {
                case "Treat":
                    run.State.Gold -= V("GoldCost");
                    if (run.State.Deck.Contains(CardIds.Whispers))
                    {
                        st.Stage = 1;
                        return;
                    }
                    run.ChangeMaxSanity(V("Sanity"));
                    break;
                case "Steal":
                    run.ChangeMaxSanity(-V("Sanity"));
                    run.OfferCards(run.RandomCards(run.Rng.Event, V("Choices"), d => d.Dual), "病历里夹着几张纸：选 1 张牌", RunPhase.Map);
                    return;
            }
            run.GoToMap();
        }
    }

    /// <summary>6 教团集会：混入人群（一半概率得金币，一半概率打一场困难池战斗，正常奖励）/ 悄悄离开。</summary>
    public sealed class CultGathering : RunEvent
    {
        public override IEnumerable<string> Handles => new[] { "Mingle", "Sneak" };

        protected override void Choose(RunEngine run, EventState st, string option)
        {
            if (option == "Mingle")
            {
                if (run.Rng.Event.Percent() < V("Chance"))
                    run.GainGold(V("Gold"));
                else
                {
                    run.StartBattle(run.PickEncounter(EncounterPool.Hard), RoomType.Battle);
                    return;
                }
            }
            run.GoToMap();
        }
    }

    /// <summary>7 雾中商贩：以血易物（最大生命 −5，3 选 1 稀有牌）/ 以物易物（交出 2 张打击，换 1 件普通遗物）/ 离开。</summary>
    public sealed class FogPeddler : RunEvent
    {
        public override IEnumerable<string> Handles => new[] { "Blood", "Barter", "Leave" };

        protected override bool CanChoose(RunEngine run, string option, out string reason)
        {
            if (option == "Blood") return Need(run.State.MaxHp > V("MaxHp"), "最大生命不够", out reason);
            if (option == "Barter") return Need(run.State.Deck.Count(DeckQueries.IsStrike) >= V("Strikes"), "打击不够", out reason);
            return Need(true, null, out reason);
        }

        protected override void Choose(RunEngine run, EventState st, string option)
        {
            switch (option)
            {
                case "Blood":
                    run.LoseMaxHp(V("MaxHp"));
                    run.OfferCards(run.RandomCards(run.Rng.Event, V("Choices"), d => d.Rarity == Rarity.Rare), "商贩打开了箱子：选 1 张牌", RunPhase.Map);
                    return;
                case "Barter":
                    for (int i = 0; i < V("Strikes"); i++)
                    {
                        // 先交出没升级的打击
                        int k = run.State.Deck.IndexOf("B01");
                        if (k < 0) k = run.State.Deck.IndexOf("B01+");
                        run.State.Deck.RemoveAt(k);
                    }
                    var relic = run.RollRelic(run.Rng.Event, RelicRarity.Common);
                    if (relic != null) run.AddRelic(relic);
                    break;
            }
            run.GoToMap();
        }
    }

    /// <summary>8 梦中的导师（第 9 层起，每局最多一次）：追随梦境（1 张打击变成梦魇之刃，保留升级）/ 惊醒（理智上限恢复到 10，恢复生命）。</summary>
    public sealed class DreamMentor : RunEvent
    {
        public override IEnumerable<string> Handles => new[] { "Follow", "Wake" };

        protected override bool CanChoose(RunEngine run, string option, out string reason) =>
            option != "Follow" ? Need(true, null, out reason) : Need(run.State.Deck.Any(DeckQueries.IsStrike), "牌组里没有打击", out reason);

        protected override void Choose(RunEngine run, EventState st, string option)
        {
            if (option == "Follow")
            {
                int k = run.State.Deck.FindIndex(DeckQueries.IsStrike);
                run.State.Deck[k] = run.State.Deck[k].EndsWith("+", StringComparison.Ordinal) ? "C06+" : "C06";
            }
            else
            {
                run.State.MaxSanity = Math.Max(run.State.MaxSanity, V("Sanity"));
                run.Heal(V("Heal"));
            }
            run.GoToMap();
        }
    }
}
