using System.Collections.Generic;
using Deeptide.Core.Actions;
using Deeptide.Core.Cards;

namespace Deeptide.Core.Enemies
{
    public static class EnemyIds
    {
        public const string Acolyte = "acolyte";
        public const string Whisperer = "whisperer";
        public const string Townsfolk = "townsfolk";
        /// <summary>调试用稻草人：不行动，用于测试和数值试验。</summary>
        public const string Dummy = "dummy";
    }

    public static class EnemyLibrary
    {
        static readonly Dictionary<string, EnemyDef> Defs = new Dictionary<string, EnemyDef>
        {
            [EnemyIds.Acolyte] = new EnemyDef
            {
                Id = EnemyIds.Acolyte, Name = "教团侍僧", HpMin = 46, HpMax = 50, Will = 8,
                Vars = CardLibrary.ParseVars("Dagger=6;Heavy=11;PrayerStrength=2"),
                Factory = () => new CultAcolyte(),
            },
            [EnemyIds.Whisperer] = new EnemyDef
            {
                Id = EnemyIds.Whisperer, Name = "呓语者", HpMin = 23, HpMax = 27, Will = 5,
                Vars = CardLibrary.ParseVars("Whisper=2;Shriek=3"),
                Factory = () => new Whisperer(),
            },
            [EnemyIds.Townsfolk] = new EnemyDef
            {
                Id = EnemyIds.Townsfolk, Name = "失魂镇民", HpMin = 28, HpMax = 32, Will = 10,
                Vars = CardLibrary.ParseVars("Hoe=9;Smash=12;WailMind=1;WailBlock=5;ScentBonus=4"),
                Factory = () => new HollowTownsfolk(),
            },
            [EnemyIds.Dummy] = new EnemyDef { Id = EnemyIds.Dummy, Name = "稻草人", HpMin = 200, HpMax = 200, Will = 10, Factory = () => new TrainingDummy() },
        };

        public static EnemyDef Get(string id)
        {
            if (!Defs.TryGetValue(id, out var def)) throw new KeyNotFoundException($"未知敌人：{id}");
            return def;
        }

        public static IEnumerable<EnemyDef> All => Defs.Values;
    }

    // 招式权重按六面骰的「几个面」来定，纸面原型可以直接用骰子掷（策划/04 §2）。
    // 每个敌人至少有两招同类型、数值不同的招式，癫狂时的意图模糊才真的有信息代价（试玩发现 #2）。

    /// <summary>N01 教团侍僧：首回合祷文；之后匕首 3 / 献祭重击 1 / 祷文 2（六面骰），重击和祷文都不连续。</summary>
    public sealed class CultAcolyte : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Prayer", Name = "狂热祷文", Intent = IntentType.Buff, Note = $"所有敌人获得 {V("PrayerStrength")} 力量" });
            AddMove(new EnemyMove { Id = "Dagger", Name = "血祭匕首", Intent = IntentType.Attack, Damage = V("Dagger") });
            AddMove(new EnemyMove { Id = "Heavy", Name = "献祭重击", Intent = IntentType.Attack, Damage = V("Heavy") });
            FirstMove = "Prayer";
            AddOption("Dagger", 3);
            AddOption("Heavy", 1, maxConsecutive: 1);
            AddOption("Prayer", 2, maxConsecutive: 1);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Dagger":
                case "Heavy":
                    Attack(e, self, m);
                    break;
                case "Prayer":
                    foreach (var en in e.State.AliveEnemies)
                        e.Queue.AddToBottom(new ApplyStatusAction(en, StatusId.Strength, V("PrayerStrength")));
                    break;
            }
        }
    }

    /// <summary>N04 呓语者：低语 3 / 尖啸 1 / 惑乱 2（六面骰）；尖啸不连续，惑乱每场最多 2 次。</summary>
    public sealed class Whisperer : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Whisper", Name = "低语", Intent = IntentType.Mind, SanityDamage = V("Whisper") });
            AddMove(new EnemyMove { Id = "Shriek", Name = "尖啸", Intent = IntentType.Mind, SanityDamage = V("Shriek") });
            AddMove(new EnemyMove { Id = "Confuse", Name = "惑乱", Intent = IntentType.Debuff, Note = "将 1 张疯语放入你的弃牌堆" });
            AddOption("Whisper", 3);
            AddOption("Shriek", 1, maxConsecutive: 1);
            AddOption("Confuse", 2, maxUses: 2);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Whisper":
                case "Shriek":
                    MindAttack(e, m.SanityDamage);
                    break;
                case "Confuse":
                    e.Queue.AddToBottom(new AddCardToPileAction(CardIds.Whispers, PileKind.Discard));
                    break;
            }
        }
    }

    /// <summary>N06 失魂镇民：挥锄 2 / 猛砸 1 / 哀嚎 3（六面骰），猛砸不连续；被动「嗅到疯狂」：玩家癫狂时攻击 +4。</summary>
    public sealed class HollowTownsfolk : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Hoe", Name = "挥锄", Intent = IntentType.Attack, Damage = V("Hoe") });
            AddMove(new EnemyMove { Id = "Smash", Name = "猛砸", Intent = IntentType.Attack, Damage = V("Smash") });
            AddMove(new EnemyMove { Id = "Wail", Name = "哀嚎", Intent = IntentType.Mind | IntentType.Defend, SanityDamage = V("WailMind"), Block = V("WailBlock") });
            AddOption("Hoe", 2);
            AddOption("Smash", 1, maxConsecutive: 1);
            AddOption("Wail", 3);
        }

        public override int BonusAttackDamage(BattleEngine e, EnemyState self) =>
            e.Player.IsLunatic ? V("ScentBonus") : 0;

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Hoe":
                case "Smash":
                    Attack(e, self, m);
                    break;
                case "Wail":
                    MindAttack(e, m.SanityDamage);
                    GainBlock(e, self, m.Block);
                    break;
            }
        }
    }

    public sealed class TrainingDummy : EnemyBehaviour
    {
        protected override void Build()
        {
            AddMove(new EnemyMove { Id = "Idle", Name = "发呆", Intent = IntentType.Unknown });
            AddOption("Idle", 1, maxConsecutive: int.MaxValue);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m) { }
    }

    /// <summary>纸面原型的三个测试遭遇（原型/阶段0 规则第 4 条）。</summary>
    public static class Encounters
    {
        public static readonly Dictionary<string, string[]> Prototype = new Dictionary<string, string[]>
        {
            ["1"] = new[] { EnemyIds.Acolyte },
            ["2"] = new[] { EnemyIds.Acolyte, EnemyIds.Whisperer },
            ["3"] = new[] { EnemyIds.Townsfolk, EnemyIds.Townsfolk },
        };
    }
}
