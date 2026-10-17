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
            [EnemyIds.Acolyte] = new EnemyDef { Id = EnemyIds.Acolyte, Name = "教团侍僧", HpMin = 46, HpMax = 50, Will = 8, Factory = () => new CultAcolyte() },
            [EnemyIds.Whisperer] = new EnemyDef { Id = EnemyIds.Whisperer, Name = "呓语者", HpMin = 23, HpMax = 27, Will = 5, Factory = () => new Whisperer() },
            [EnemyIds.Townsfolk] = new EnemyDef { Id = EnemyIds.Townsfolk, Name = "失魂镇民", HpMin = 28, HpMax = 32, Will = 10, Factory = () => new HollowTownsfolk() },
            [EnemyIds.Dummy] = new EnemyDef { Id = EnemyIds.Dummy, Name = "稻草人", HpMin = 200, HpMax = 200, Will = 10, Factory = () => new TrainingDummy() },
        };

        public static EnemyDef Get(string id)
        {
            if (!Defs.TryGetValue(id, out var def)) throw new KeyNotFoundException($"未知敌人：{id}");
            return def;
        }

        public static IEnumerable<EnemyDef> All => Defs.Values;
    }

    /// <summary>N01 教团侍僧（策划/04 §2）：首回合祷文；之后匕首 65% / 祷文 35%，祷文不连续。</summary>
    public sealed class CultAcolyte : EnemyBehaviour
    {
        public CultAcolyte()
        {
            AddMove(new EnemyMove { Id = "Prayer", Name = "狂热祷文", Intent = IntentType.Buff, Note = "所有敌人获得 2 力量" });
            AddMove(new EnemyMove { Id = "Dagger", Name = "血祭匕首", Intent = IntentType.Attack, Damage = 7 });
            FirstMove = "Prayer";
            AddOption("Dagger", 65);
            AddOption("Prayer", 35, maxConsecutive: 1);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Dagger":
                    Attack(e, self, m);
                    break;
                case "Prayer":
                    foreach (var en in e.State.AliveEnemies)
                        e.Queue.AddToBottom(new ApplyStatusAction(en, StatusId.Strength, 2));
                    break;
            }
        }
    }

    /// <summary>N04 呓语者：低语 70% / 惑乱 30%，惑乱每场最多 2 次。</summary>
    public sealed class Whisperer : EnemyBehaviour
    {
        public Whisperer()
        {
            AddMove(new EnemyMove { Id = "Whisper", Name = "低语", Intent = IntentType.Mind, SanityDamage = 2 });
            AddMove(new EnemyMove { Id = "Confuse", Name = "惑乱", Intent = IntentType.Debuff, Note = "将 1 张疯语放入你的弃牌堆" });
            AddOption("Whisper", 70);
            AddOption("Confuse", 30, maxUses: 2);
        }

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Whisper":
                    MindAttack(e, m.SanityDamage);
                    break;
                case "Confuse":
                    e.Queue.AddToBottom(new AddCardToPileAction(CardIds.Whispers, PileKind.Discard));
                    break;
            }
        }
    }

    /// <summary>N06 失魂镇民：挥锄 60% / 哀嚎 40%；被动「嗅到疯狂」：玩家癫狂时攻击 +4。</summary>
    public sealed class HollowTownsfolk : EnemyBehaviour
    {
        public const int ScentOfMadnessBonus = 4;

        public HollowTownsfolk()
        {
            AddMove(new EnemyMove { Id = "Hoe", Name = "挥锄", Intent = IntentType.Attack, Damage = 9 });
            AddMove(new EnemyMove { Id = "Wail", Name = "哀嚎", Intent = IntentType.Mind | IntentType.Defend, SanityDamage = 1, Block = 5 });
            AddOption("Hoe", 60);
            AddOption("Wail", 40);
        }

        public override int BonusAttackDamage(BattleEngine e, EnemyState self) =>
            e.Player.IsLunatic ? ScentOfMadnessBonus : 0;

        public override void Perform(BattleEngine e, EnemyState self, EnemyMove m)
        {
            switch (m.Id)
            {
                case "Hoe":
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
        public TrainingDummy()
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
