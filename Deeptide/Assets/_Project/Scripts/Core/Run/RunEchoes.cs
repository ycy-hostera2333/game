using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Relics;

namespace Deeptide.Core.Run
{
    /// <summary>
    /// 回响（策划/15 §3、09 M7.1）：开局在信之后 3 选 1，每局可以整组重抽一次。用 echo 流，不碰其余随机流。
    /// 选中的回响记进 RunState.Echoes，开战时作为隐藏遗物挂上（BattleSetup.Echoes），不进遗物栏、不能移除。
    /// </summary>
    public sealed partial class RunEngine
    {
        /// <summary>每次给几个回响。</summary>
        public const int EchoChoices = 3;

        /// <summary>每局能重抽几次（整组三个一起换）。</summary>
        public const int EchoRerollsPerRun = 1;

        static string TierName(EchoTier tier) => tier == EchoTier.Shallow ? "浅" : tier == EchoTier.Deep ? "深" : "渊";

        /// <summary>打开回响选择（第一章开局：浅档）。</summary>
        internal void OfferEchoes(EchoTier tier)
        {
            var echoes = RollEchoes(tier, Enumerable.Empty<string>());
            State.Select = new SelectState
            {
                Purpose = SelectPurpose.Echo,
                Title = $"回响（{TierName(tier)}）：你在别的夜里走到过这里，留下了一点东西",
                Echoes = echoes,
                EchoTier = tier,
                CanSkip = false,
                Return = RunPhase.Map,
                SkipReturn = RunPhase.Map,
            };
            State.Phase = RunPhase.Select;
            State.Stats.EchoChoices.Add(new EchoChoiceRecord { Tier = tier, Offered = echoes.ToList() });
        }

        /// <summary>
        /// 抽一组回响（策划/15 §3.2 第一章的规则）：只从这个角色能用的里抽，选过的不再出现；三个选项的倾向互不相同（各流派和中立里挑），
        /// 重抽时尽量不给刚才给过的。凑不齐时依次放宽：先允许给过的，再允许倾向重复。
        /// 每个位置先在「最合适」的那一档里等概率挑一个倾向，再在这个倾向里等概率挑一个回响，所以中立回响多也不会挤掉流派回响。
        /// </summary>
        List<string> RollEchoes(EchoTier tier, IEnumerable<string> avoid)
        {
            var shown = new HashSet<string>(avoid);
            var pool = EchoLibrary.Pool(tier, State.Character).Where(d => !State.Echoes.Contains(d.Id)).ToList();
            var picked = new List<EchoDef>();
            var tendencies = new HashSet<string>();
            string TendencyOf(EchoDef d) => EchoLibrary.TendencyFor(d, State.Character);

            while (picked.Count < EchoChoices)
            {
                var rest = pool.Where(d => !picked.Contains(d)).ToList();
                if (rest.Count == 0) break;
                int Rank(EchoDef d) => (tendencies.Contains(TendencyOf(d)) ? 2 : 0) + (shown.Contains(d.Id) ? 1 : 0);
                int best = rest.Min(Rank);
                var top = rest.Where(d => Rank(d) == best).ToList();
                var kinds = top.Select(TendencyOf).Distinct().ToList();
                string kind = kinds[Rng.Echo.NextInt(kinds.Count)];
                var ofKind = top.Where(d => TendencyOf(d) == kind).ToList();
                var echo = ofKind[Rng.Echo.NextInt(ofKind.Count)];
                picked.Add(echo);
                tendencies.Add(kind);
            }
            return picked.Select(d => d.Id).ToList();
        }

        /// <summary>回响的一行说明：名字、对本角色的倾向、效果。</summary>
        public string EchoLabel(string id)
        {
            var def = EchoLibrary.Get(id);
            return $"{def.Name}（{EchoLibrary.TendencyFor(def, State.Character)}）{EchoLibrary.Create(id).Description}";
        }

        void EchoOptions(SelectState sel, List<RunOption> options)
        {
            foreach (var id in sel.Echoes)
            {
                string echo = id;
                options.Add(new RunOption { Label = EchoLabel(echo), Tag = "echo:" + echo, Do = () => PickEcho(echo) });
            }
            int left = EchoRerollsPerRun - State.EchoRerolls;
            options.Add(new RunOption
            {
                Label = $"重抽（这一局还能重抽 {left} 次）",
                Tag = "reroll",
                Enabled = left > 0,
                Reason = "这一局已经重抽过了",
                Do = RerollEchoes,
            });
        }

        /// <summary>
        /// 行囊：药水栏 +1，再给一瓶随机药水（用 echo 流，不打乱奖励流）。15 §3.3 写的是「每章开始时」给一瓶，
        /// Demo 只有一章，选的这一刻就是第一章开始；多章时在章初再调一次。
        /// </summary>
        void ApplySatchel()
        {
            var def = EchoLibrary.Get(EchoIds.Satchel);
            for (int i = 0; i < def.Vars["Slots"]; i++) State.Potions.Add(null);
            for (int i = 0; i < def.Vars["Potions"]; i++) TryAddPotion(RollPotion(Rng.Echo));
        }

        void RerollEchoes()
        {
            var sel = State.Select;
            State.EchoRerolls++;
            sel.EchoesShown.AddRange(sel.Echoes);
            sel.Echoes = RollEchoes(sel.EchoTier, sel.EchoesShown);
            var record = State.Stats.EchoChoices.Last();
            record.Offered.AddRange(sel.Echoes);
            record.Rerolls++;
            // 重抽之后马上存：读档不能把重抽次数和这一组再刷一遍
            SavePoint();
        }

        void PickEcho(string id)
        {
            State.Echoes.Add(id);
            if (id == EchoIds.Satchel) ApplySatchel();
            State.Stats.EchoChoices.Last().Picked = id;
            State.Select = null;
            GoToMap();
        }
    }
}
