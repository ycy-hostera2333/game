using System.Collections.Generic;
using Deeptide.Core.Actions;

namespace Deeptide.Core.Relics
{
    public static class RelicIds
    {
        public const string InvestigatorNotes = "RL0";
    }

    public abstract class Relic : BattleListener
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
    }

    public static class RelicLibrary
    {
        public static Relic Create(string id)
        {
            switch (id)
            {
                case RelicIds.InvestigatorNotes: return new InvestigatorNotes();
                default: throw new KeyNotFoundException($"未知遗物：{id}");
            }
        }
    }

    /// <summary>起始遗物「调查笔记」：每场战斗首次进入癫狂时，获得 1 能量并抽 2 张牌（策划/03 §1）。</summary>
    public sealed class InvestigatorNotes : Relic
    {
        public override string Id => RelicIds.InvestigatorNotes;
        public override string Name => "调查笔记";
        public override string Description => "每场战斗第一次进入癫狂时，获得 1 能量并抽 2 张牌。";

        public bool TriggeredThisCombat { get; private set; }

        public override void OnCombatStart(BattleEngine e) => TriggeredThisCombat = false;

        public override void OnLunacyChanged(BattleEngine e, bool entered)
        {
            if (!entered || TriggeredThisCombat) return;
            TriggeredThisCombat = true;
            e.Log.Add(BattleEventType.RelicTriggered, "调查笔记触发：+1 能量，抽 2 张牌", Name);
            e.Queue.AddToTop(new GainEnergyAction(1), new DrawCardsAction(2));
        }
    }
}
