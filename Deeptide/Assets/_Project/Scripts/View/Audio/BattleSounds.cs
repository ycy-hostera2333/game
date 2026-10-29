using System;
using System.Collections.Generic;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;

namespace Deeptide.View.Audio
{
    /// <summary>一次要播的音效：ID（不带变体号）、相对现在的延迟、走哪条总线。</summary>
    public struct Cue
    {
        public string Id;
        public float Delay;
        public SfxBus Bus;

        public Cue(string id, SfxBus bus, float delay = 0)
        {
            Id = id;
            Bus = bus;
            Delay = delay;
        }

        public override string ToString() => Delay > 0 ? $"{Id}@{Delay:0.00}" : Id;
    }

    public enum SfxBus { Ui, Combat, Sanity }

    /// <summary>
    /// 战斗事件 → 音效（策划/13 §5.2）。和 BattleScreen 一样逐条吃 BattleDirector 回放的事件，自己记生命 / 格挡 / 理智 / 计数器的上一个值来算变化量；
    /// 需要知道「这次扣格挡是不是被打的」「这次掉理智是谁造成的」时，往后看同一批里还没播的事件（<paramref name="upcoming"/>）。
    /// 纯逻辑、不碰 Unity，播放交给 <see cref="AudioDirector"/>。
    /// </summary>
    public sealed class BattleSounds
    {
        /// <summary>同一次抽牌最多几声翻纸（13 §5.2 U02）。</summary>
        public const int MaxDrawSounds = 5;
        /// <summary>理智一次变化好几格时，音阶一格一个音，最多几个音、间隔多久。</summary>
        public const int MaxSanitySteps = 4;
        public const float SanityStep = 0.14f;
        /// <summary>R03 / R05 / R06 先响，R01 晚这么久接上。</summary>
        public const float SourceLead = 0.32f;
        /// <summary>R01 / R02 的素材做到理智 12（上限被事件加高时用）。</summary>
        public const int MaxSanityClip = 12;

        readonly Dictionary<int, int> _hp = new Dictionary<int, int>();
        readonly Dictionary<int, int> _block = new Dictionary<int, int>();
        readonly Dictionary<(int, string), int> _counters = new Dictionary<(int, string), int>();
        int _sanity = -1;
        int _drawSounds;

        /// <summary>C01 的三档（单次扣血，档位同 10 §6.3）。</summary>
        public static string HitTier(int hpLoss) => hpLoss >= 20 ? "C01_heavy" : hpLoss >= 10 ? "C01_medium" : "C01_light";

        public List<Cue> Map(BattleEvent e, BattleEngine engine, IEnumerable<BattleEvent> upcoming)
        {
            var cues = new List<Cue>();
            switch (e.Type)
            {
                case BattleEventType.PlayerInit:
                    _hp[BattleEvent.PlayerEntity] = e.Value;
                    _block[BattleEvent.PlayerEntity] = 0;
                    _sanity = e.Amount;
                    break;

                case BattleEventType.EnemySpawned:
                    _hp[e.Entity] = e.Value;
                    _block[e.Entity] = 0;
                    break;

                case BattleEventType.HpChanged:
                {
                    int before = _hp.TryGetValue(e.Entity, out var h) ? h : e.Value;
                    _hp[e.Entity] = e.Value;
                    if (e.Value < before) cues.Add(new Cue(HitTier(before - e.Value), SfxBus.Combat));
                    break;
                }

                case BattleEventType.BlockChanged:
                {
                    int before = _block.TryGetValue(e.Entity, out var b) ? b : 0;
                    _block[e.Entity] = e.Value;
                    if (e.Value > before) cues.Add(new Cue("C03", SfxBus.Combat));
                    else if (e.Value < before)
                    {
                        // 回合开始清格挡也会走这里：只有后面紧跟着打在同一个人身上的 Damage 才算被打
                        var hit = NextNarrative(upcoming);
                        if (hit != null && hit.Type == BattleEventType.Damage && hit.Entity == e.Entity)
                            cues.Add(new Cue(hit.Value == 0 && e.Value > 0 ? "C02" : "C04", SfxBus.Combat));
                    }
                    break;
                }

                case BattleEventType.SanityChanged:
                {
                    int before = _sanity < 0 ? e.Value : _sanity;
                    _sanity = e.Value;
                    if (e.Value < before) SanityLost(cues, before, e.Value, NextNarrative(upcoming));
                    else if (e.Value > before) SanityRestored(cues, before, e.Value);
                    break;
                }

                case BattleEventType.CardMoved:
                    if (e.From == PileKind.Draw && e.To == PileKind.Hand && _drawSounds++ < MaxDrawSounds)
                        cues.Add(new Cue("U02", SfxBus.Ui));
                    break;

                case BattleEventType.CardsDrawn:
                    _drawSounds = 0;
                    break;

                case BattleEventType.PileShuffled:
                    cues.Add(new Cue("U03", SfxBus.Ui));
                    break;

                case BattleEventType.CardPlayed:
                    cues.Add(new Cue(CardPlayed(e.Id), SfxBus.Ui));
                    break;

                case BattleEventType.TurnStarted:
                    _drawSounds = 0;
                    break;

                // 通用的前冲声：每个敌人自己的声音身份是 P1（13 §5.3），到时候按敌人 ID 换；前冲动画每个招式都播，声音也一样
                case BattleEventType.EnemyMove:
                    cues.Add(new Cue("C05", SfxBus.Combat));
                    break;

                case BattleEventType.EnemyDied:
                    cues.Add(new Cue("C06", SfxBus.Combat));
                    break;

                case BattleEventType.EnemySummoned:
                    cues.Add(new Cue("C07", SfxBus.Combat));
                    break;

                case BattleEventType.StatusApplied:
                    cues.Add(new Cue(IsBuff(e.Status, e.Amount) ? "C08_buff" : "C08_debuff", SfxBus.Combat));
                    break;

                case BattleEventType.PotionUsed:
                    cues.Add(new Cue("C09", SfxBus.Combat));
                    break;

                case BattleEventType.RelicTriggered:
                    cues.Add(new Cue("C10", SfxBus.Ui));
                    break;

                case BattleEventType.WardConsumed:
                    cues.Add(new Cue("R04", SfxBus.Sanity));
                    break;

                case BattleEventType.FearApplied:
                    if (e.Amount > 0) cues.Add(new Cue("F01", SfxBus.Combat));
                    break;

                case BattleEventType.EnemyStunned:
                    cues.Add(new Cue("F02", SfxBus.Combat));
                    break;

                case BattleEventType.EnemyStunSkipped:
                    cues.Add(new Cue("F03", SfxBus.Combat));
                    break;

                case BattleEventType.IntentRevealChanged:
                    if (e.Value == 1) cues.Add(new Cue("L01", SfxBus.Combat));
                    break;

                case BattleEventType.CounterChanged:
                {
                    var key = (e.Entity, e.Id);
                    int before = _counters.TryGetValue(key, out var c) ? c : 0;
                    _counters[key] = e.Value;
                    if (e.Id == DrownedPriest.Tide && IsPriest(engine, e.Entity) && e.Value != before)
                        cues.Add(new Cue(e.Value > before ? "B01" : "B02", SfxBus.Combat));
                    break;
                }
            }
            return cues;
        }

        /// <summary>R01：一格一个音往下走，结束在剩下的理智；精神攻击 / 侵蚀 / 疯语先响自己的声音（13 §5.2「来源判断」）。</summary>
        static void SanityLost(List<Cue> cues, int before, int after, BattleEvent narrative)
        {
            float start = 0;
            string source = narrative != null && narrative.Type == BattleEventType.SanityLost ? narrative.Id : null;
            string lead = source == nameof(SanitySource.Enemy) ? "R03"
                : source == nameof(SanitySource.Erosion) ? "R05"
                : source == nameof(SanitySource.Curse) ? "R06" : null;
            if (lead != null)
            {
                cues.Add(new Cue(lead, SfxBus.Sanity));
                start = SourceLead;
            }
            int steps = Math.Min(before - after, MaxSanitySteps);
            for (int i = 0; i < steps; i++)
            {
                int left = after + steps - 1 - i;
                cues.Add(new Cue($"R01_{Math.Min(left, MaxSanityClip)}", SfxBus.Sanity, start + i * SanityStep));
            }
        }

        static void SanityRestored(List<Cue> cues, int before, int after)
        {
            int steps = Math.Min(after - before, MaxSanitySteps);
            for (int i = 0; i < steps; i++)
            {
                int now = after - steps + 1 + i;
                cues.Add(new Cue($"R02_{Math.Max(1, Math.Min(now, MaxSanityClip))}", SfxBus.Sanity, i * SanityStep));
            }
        }

        static string CardPlayed(string id)
        {
            var def = !string.IsNullOrEmpty(id) && CardLibrary.Exists(id) ? CardLibrary.Get(id.TrimEnd('+')) : null;
            if (def == null) return "U04_skill";
            return def.Type == CardType.Attack ? "U04_attack" : def.Type == CardType.Power ? "U04_power" : "U04_skill";
        }

        /// <summary>增益：力量、敏捷、心防加层；其余（易伤、虚弱、脆弱、缠网、流血，或者力量被扣）都算减益。</summary>
        static bool IsBuff(StatusId status, int amount) =>
            amount > 0 && (status == StatusId.Strength || status == StatusId.Dexterity || status == StatusId.Ward);

        static bool IsPriest(BattleEngine engine, int entity) =>
            engine != null && entity >= 0 && entity < engine.State.Enemies.Count && engine.State.Enemies[entity].Def.Id == EnemyIds.Priest;

        static BattleEvent NextNarrative(IEnumerable<BattleEvent> upcoming)
        {
            if (upcoming == null) return null;
            foreach (var u in upcoming)
                if (!u.IsStateChange) return u;
            return null;
        }
    }
}
