using System;
using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Actions;

namespace Deeptide.Core.Relics
{
    public static class EchoIds
    {
        public const string Wick = "wick";
        public const string Shoal = "shoal";
        public const string Sleepless = "sleepless";
        public const string Aftershock = "aftershock";
        public const string Baffle = "baffle";
        public const string Spread = "spread";
        public const string NotesAddendum = "notes_addendum";
        public const string Satchel = "satchel";
        public const string SeaChart = "sea_chart";
        public const string DeepScar = "deep_scar";
        public const string TidalRange = "tidal_range";
        public const string BloodToSea = "blood_to_sea";
    }

    /// <summary>回响的档位（策划/15 §3.1）：第一章浅、第二章深、第三章渊。Demo 下一版只有浅。</summary>
    public enum EchoTier { Shallow, Deep, Abyss }

    /// <summary>回响静态数据，对应 Data/echoes.csv 的一行（策划/15 §3、09 M7.1）。</summary>
    public sealed class EchoDef
    {
        /// <summary>倾向里的「中立」：哪个流派都不偏。不属于本角色流派的倾向，对这个角色也按中立算（挡板、不眠对狼人，策划/15 §3.3）。</summary>
        public const string Neutral = "中立";

        public string Id;
        public string Name;
        public EchoTier Tier;
        /// <summary>归属角色（characters.csv 的 id），空 = 通用。</summary>
        public string Character = "";
        /// <summary>流派倾向：某个角色的流派（守夜 / 堕落 / 传染 / 狂猎 / 月相 / 血宴）或中立。开局的三个选项按它错开。</summary>
        public string Tendency;
        public Dictionary<string, int> Vars = new Dictionary<string, int>();
        public string Text;
        public Func<Relic> Factory;
        /// <summary>给 Relic.Init 用的遗物定义：回响在战斗里就是一件隐藏遗物（稀有度 Echo）。</summary>
        internal RelicDef AsRelic;
    }

    /// <summary>回响注册表，内容由 Data.GameData 从 echoes.csv 载入，按表格行序保存。</summary>
    public static class EchoLibrary
    {
        static readonly Dictionary<string, EchoDef> Defs = new Dictionary<string, EchoDef>();
        static readonly List<EchoDef> Ordered = new List<EchoDef>();

        public static IReadOnlyList<EchoDef> All => Ordered;

        public static bool Exists(string id) => id != null && Defs.ContainsKey(id);

        public static EchoDef Get(string id)
        {
            if (id != null && Defs.TryGetValue(id, out var def)) return def;
            if (Defs.Count == 0) throw new InvalidOperationException("回响数据还没有载入（先调用 GameData.Load）");
            throw new KeyNotFoundException($"未知回响：{id}");
        }

        /// <summary>这一档里这个角色能抽到的回响（通用 + 本角色），按表格行序。</summary>
        public static IEnumerable<EchoDef> Pool(EchoTier tier, string character) =>
            Ordered.Where(d => d.Tier == tier && Run.CharacterLibrary.Allows(d.Character, character));

        /// <summary>
        /// 这个回响对这个角色算哪个倾向：属于该角色的流派就是它本身，否则按中立（挡板是守夜，对狼人是中立）。
        /// </summary>
        public static string TendencyFor(EchoDef def, string character)
        {
            var archetypes = Run.CharacterLibrary.Get(character).Archetypes;
            return archetypes.Contains(def.Tendency) ? def.Tendency : EchoDef.Neutral;
        }

        /// <summary>建一个回响的监听器实例（每场战斗新建，和遗物一样）。</summary>
        public static Relic Create(string id)
        {
            var def = Get(id);
            var echo = def.Factory();
            echo.Init(def.AsRelic);
            return echo;
        }

        internal static void Clear()
        {
            Defs.Clear();
            Ordered.Clear();
        }

        internal static void Add(EchoDef def)
        {
            Defs.Add(def.Id, def);
            Ordered.Add(def);
        }
    }

    /// <summary>回响类注册表：echoes.csv 的每一行按 ID 找到这里的工厂（显式表，理由同 CardClasses）。</summary>
    public static class EchoClasses
    {
        public static readonly IReadOnlyDictionary<string, Func<Relic>> Factories = new Dictionary<string, Func<Relic>>
        {
            [EchoIds.Wick] = () => new Wick(),
            [EchoIds.Shoal] = () => new Shoal(),
            [EchoIds.Sleepless] = () => new Sleepless(),
            [EchoIds.Aftershock] = () => new Aftershock(),
            [EchoIds.Baffle] = () => new Baffle(),
            [EchoIds.Spread] = () => new Spread(),
            [EchoIds.NotesAddendum] = () => new NotesAddendum(),
            [EchoIds.Satchel] = () => new RunLevelEcho(),
            [EchoIds.SeaChart] = () => new RunLevelEcho(),
            [EchoIds.DeepScar] = () => new DeepScar(),
            [EchoIds.TidalRange] = () => new TidalRange(),
            [EchoIds.BloodToSea] = () => new BloodToSea(),
        };
    }

    // ==================== 浅（策划/15 §3.3）：只改规则参数的几个，走 BattleConfig ====================

    /// <summary>灯芯：清明放宽，理智 ≥ 上限 − 2 即为清明。</summary>
    public sealed class Wick : Relic
    {
        public override void ModifyConfig(BattleConfig config) => config.ClarityMargin += V("Margin");
    }

    /// <summary>浅滩：癫狂线上移，理智 ≤ 5 即为癫狂。</summary>
    public sealed class Shoal : Relic
    {
        public override void ModifyConfig(BattleConfig config) => config.LunacyThreshold += V("Line");
    }

    /// <summary>不眠：开战理智 = 上限 − 3（狼人是 3，起手就是兽形，但不算变身）。</summary>
    public sealed class Sleepless : Relic
    {
        public override void ModifyConfig(BattleConfig config) =>
            config.StartSanityDeficit = Math.Max(config.StartSanityDeficit, V("Deficit"));
    }

    /// <summary>余悸：震慑时恐惧不清零，保留一半（向下取整，最多到新意志 − 1）。</summary>
    public sealed class Aftershock : Relic
    {
        public override void ModifyConfig(BattleConfig config) =>
            config.StunFearKeptPercent = Math.Max(config.StunFearKeptPercent, V("Keep"));
    }

    // ==================== 浅：加触发的 ====================

    /// <summary>挡板：战斗开始时获得心防；心防也能抵消侵蚀。</summary>
    public sealed class Baffle : Relic
    {
        public override void ModifyConfig(BattleConfig config) => config.WardBlocksErosion = true;

        public override void OnCombatStart(BattleEngine e)
        {
            Trigger(e, $"获得 {V("Ward")} 层心防");
            e.Queue.AddToBottom(new ApplyStatusAction(e.Player, StatusId.Ward, V("Ward")));
        }
    }

    /// <summary>
    /// 蔓延：敌人死亡时，身上的恐惧平均分给其他还活着、有心智、没被震慑的敌人（被震慑的接不住恐惧）（向下取整，除不尽的余数从前往后各 +1，总量不丢）。
    /// 分到的恐惧照常可能震慑。
    /// </summary>
    public sealed class Spread : Relic
    {
        public override void OnEnemyDied(BattleEngine e, EnemyState enemy)
        {
            int fear = enemy.Fear;
            var others = e.State.AliveEnemies.Where(x => x != enemy && !x.Mindless && !x.Stunned).ToList();
            if (fear <= 0 || others.Count == 0) return;
            Trigger(e, $"{enemy.Name} 的 {fear} 恐惧分给其他敌人");
            var actions = new List<GameAction>();
            for (int i = 0; i < others.Count; i++)
            {
                int share = fear / others.Count + (i < fear % others.Count ? 1 : 0);
                if (share > 0) actions.Add(new ApplyFearAction(others[i], share));
            }
            e.Queue.AddToTop(actions.ToArray());
        }
    }

    /// <summary>笔记补页：每场第一次从癫狂回到清醒时，调查笔记再触发一次（同样的能量和抽牌）。没有调查笔记就什么都不做。</summary>
    public sealed class NotesAddendum : Relic
    {
        bool _used;

        public override void OnLunacyChanged(BattleEngine e, bool entered)
        {
            if (entered || _used) return;
            var notes = e.Player.Relics.FirstOrDefault(r => r.Id == RelicIds.InvestigatorNotes);
            if (notes == null) return;
            _used = true;
            Trigger(e, $"调查笔记再触发一次：+{notes.V("Energy")} 能量，抽 {notes.V("Draw")} 张牌");
            e.Queue.AddToTop(new GainEnergyAction(notes.V("Energy")), new DrawCardsAction(notes.V("Draw")));
        }
    }

    /// <summary>行囊、海图：只改跑局层（药水栏、地图信息），在 RunEngine 里按 ID 处理；战斗里什么都不做。</summary>
    public sealed class RunLevelEcho : Relic { }

    // ==================== 浅 · 狼人（策划/15 §3.3） ====================

    /// <summary>深痕：兽形时攻击每段再 +1，和兽血的 +2 合起来是 +3。</summary>
    public sealed class DeepScar : Relic
    {
        public override int DamageBonus(BattleEngine e, Combatant source, Combatant target, DamageKind kind) =>
            kind == DamageKind.Attack && source == e.Player && e.Player.IsLunatic ? V("Damage") : 0;
    }

    /// <summary>潮差：每轮前两次变身、复归都算「变身：/ 复归：」。</summary>
    public sealed class TidalRange : Relic
    {
        public override void ModifyConfig(BattleConfig config) =>
            config.CountedCrossingsPerRound = Math.Max(config.CountedCrossingsPerRound, V("Times"));
    }

    /// <summary>血入海：流血每两回合才 −1。</summary>
    public sealed class BloodToSea : Relic
    {
        public override void ModifyConfig(BattleConfig config) =>
            config.BleedDecayEvery = Math.Max(config.BleedDecayEvery, V("Every"));
    }
}
