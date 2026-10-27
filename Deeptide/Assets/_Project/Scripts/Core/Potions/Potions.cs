using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Deeptide.Core.Actions;

namespace Deeptide.Core.Potions
{
    public enum PotionRarity { Common, Uncommon, Rare }

    /// <summary>药水静态数据，对应 Data/potions.csv 的一行（策划/05 §2）。</summary>
    public sealed class PotionDef
    {
        public string Id;
        public string Name;
        public PotionRarity Rarity;
        /// <summary>归属角色（characters.csv 的 id），空 = 通用。</summary>
        public string Character = "";
        public CardTarget Target;
        public Dictionary<string, int> Vars = new Dictionary<string, int>();
        public string Text;
        public Func<Potion> Factory;
    }

    /// <summary>药水：战斗中随时使用、不耗能量（策划/02 §1）。效果写在子类的 Use 里，数值通过 V() 读取。</summary>
    public abstract class Potion
    {
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        public PotionDef Def { get; private set; }

        internal void Init(PotionDef def) => Def = def;

        public string Id => Def.Id;
        public string Name => Def.Name;
        public string Description => Placeholder.Replace(Def.Text ?? "", m => V(m.Groups[1].Value).ToString());

        public int V(string key)
        {
            if (!Def.Vars.TryGetValue(key, out var v)) throw new KeyNotFoundException($"{Def.Id} 缺少数值 {key}");
            return v;
        }

        public abstract void Use(PotionContext c);
    }

    public sealed class PotionContext
    {
        public readonly BattleEngine Engine;
        public readonly Potion Potion;
        public readonly EnemyState Target;

        public PotionContext(BattleEngine engine, Potion potion, EnemyState target)
        {
            Engine = engine;
            Potion = potion;
            Target = target;
        }

        public PlayerState Player => Engine.Player;

        public void Enqueue(GameAction a) => Engine.Queue.AddToBottom(a);
    }

    /// <summary>药水注册表，内容由 Data.GameData 从 potions.csv 载入。</summary>
    public static class PotionLibrary
    {
        static readonly Dictionary<string, PotionDef> Defs = new Dictionary<string, PotionDef>();
        static readonly List<PotionDef> Ordered = new List<PotionDef>();

        public static IReadOnlyList<PotionDef> All => Ordered;

        public static bool Exists(string id) => Defs.ContainsKey(id);

        public static PotionDef Get(string id)
        {
            if (Defs.TryGetValue(id, out var def)) return def;
            if (Defs.Count == 0) throw new InvalidOperationException("药水数据还没有载入（先调用 GameData.Load）");
            throw new KeyNotFoundException($"未知药水：{id}");
        }

        public static Potion Create(string id)
        {
            var def = Get(id);
            var potion = def.Factory();
            potion.Init(def);
            return potion;
        }

        internal static void Clear()
        {
            Defs.Clear();
            Ordered.Clear();
        }

        internal static void Add(PotionDef def)
        {
            Defs.Add(def.Id, def);
            Ordered.Add(def);
        }
    }

    /// <summary>药水类注册表：potions.csv 的每一行按 ID 找到这里的工厂（显式表，理由同 CardClasses）。</summary>
    public static class PotionClasses
    {
        public static readonly IReadOnlyDictionary<string, Func<Potion>> Factories = new Dictionary<string, Func<Potion>>
        {
            ["smelling_salts"] = () => new SmellingSalts(),
            ["iron_tonic"] = () => new IronTonic(),
            ["powder_flask"] = () => new PowderFlask(),
            ["acid_vial"] = () => new AcidVial(),
            ["hallucinogen"] = () => new Hallucinogen(),
            ["fear_powder"] = () => new FearPowder(),
            ["berserk_tincture"] = () => new BerserkTincture(),
            ["warding_draught"] = () => new WardingDraught(),
            ["bloody_bait"] = () => new BloodyBait(),
        };
    }

    /// <summary>嗅盐：恢复理智。</summary>
    public sealed class SmellingSalts : Potion
    {
        public override void Use(PotionContext c) => c.Enqueue(new RestoreSanityAction(V("Restore")));
    }

    /// <summary>铁剂：获得格挡（不是卡牌格挡，不吃敏捷 / 脆弱）。</summary>
    public sealed class IronTonic : Potion
    {
        public override void Use(PotionContext c) => c.Enqueue(new GainBlockAction(c.Player, V("Block"), false));
    }

    /// <summary>火药瓶：对所有敌人造成效果伤害。</summary>
    public sealed class PowderFlask : Potion
    {
        public override void Use(PotionContext c) => c.Enqueue(new DamageAllEnemiesAction(null, V("Blast"), 1, DamageKind.Effect));
    }

    /// <summary>酸液瓶：施加易伤。</summary>
    public sealed class AcidVial : Potion
    {
        public override void Use(PotionContext c) => c.Enqueue(new ApplyStatusAction(c.Target, StatusId.Vulnerable, V("Vulnerable")));
    }

    /// <summary>迷幻剂：理智设为 4（直接进入癫狂），抽牌。</summary>
    public sealed class Hallucinogen : Potion
    {
        public override void Use(PotionContext c)
        {
            c.Enqueue(new SetSanityAction(V("Sanity")));
            c.Enqueue(new DrawCardsAction(V("Draw")));
        }
    }

    /// <summary>恐惧粉末：对所有敌人施加恐惧。</summary>
    public sealed class FearPowder : Potion
    {
        public override void Use(PotionContext c) => c.Enqueue(new FearAllEnemiesAction(V("Fear")));
    }

    /// <summary>狂暴酊：获得力量。</summary>
    public sealed class BerserkTincture : Potion
    {
        public override void Use(PotionContext c) => c.Enqueue(new ApplyStatusAction(c.Player, StatusId.Strength, V("Strength")));
    }

    /// <summary>护心符水：获得心防。</summary>
    public sealed class WardingDraught : Potion
    {
        public override void Use(PotionContext c) => c.Enqueue(new ApplyStatusAction(c.Player, StatusId.Ward, V("Ward")));
    }

    /// <summary>血腥诱饵（狼人专属，策划/14 §2.7）：对所有敌人施加流血。</summary>
    public sealed class BloodyBait : Potion
    {
        public override void Use(PotionContext c) => c.Enqueue(new StatusAllEnemiesAction(StatusId.Bleed, V("Bleed")));
    }
}
