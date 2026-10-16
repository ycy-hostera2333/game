using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Deeptide.Core.Actions;

namespace Deeptide.Core.Cards
{
    /// <summary>
    /// 卡牌静态数据（对应以后 Data/cards.csv 的一行，策划/08 §4）。
    /// Vars 中的约定键：Sanity / MadSanity = 清醒面 / 癫狂面的理智代价，由引擎在出牌第 ③ 步统一支付。
    /// </summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public CardType Type;
        public Rarity Rarity;
        public string Archetype;
        public int Cost;
        /// <summary>升级后的费用，-1 表示不变。</summary>
        public int UpgradedCost = -1;
        public CardTarget Target;
        public bool Dual;
        public bool Exhaust;
        public bool Ethereal;
        public bool Unplayable;
        public Dictionary<string, int> Vars = new Dictionary<string, int>();
        public Dictionary<string, int> UpgradeVars = new Dictionary<string, int>();
        public string TextSane;
        public string TextMad;
        public Func<Card> Factory;
    }

    /// <summary>一张具体的牌（牌组里的每张实体牌各是一个实例）。逻辑写在子类的 Play 里，数值全部通过 V() 读取。</summary>
    public abstract class Card
    {
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        public CardDef Def { get; private set; }
        /// <summary>本场战斗内唯一编号，用于哈希和表现层追踪。</summary>
        public int Uid { get; private set; }
        public bool Upgraded { get; private set; }

        internal void Init(CardDef def, int uid, bool upgraded)
        {
            Def = def;
            Uid = uid;
            Upgraded = upgraded;
        }

        public string Id => Def.Id;
        public string Name => Upgraded ? Def.Name + "+" : Def.Name;
        public int Cost => Upgraded && Def.UpgradedCost >= 0 ? Def.UpgradedCost : Def.Cost;
        public bool Playable => !Def.Unplayable;

        public int V(string key)
        {
            if (Upgraded && Def.UpgradeVars.TryGetValue(key, out var up)) return up;
            return Def.Vars.TryGetValue(key, out var v) ? v : 0;
        }

        public int SanityCost(bool madFace) => V(madFace && Def.Dual ? "MadSanity" : "Sanity");

        public string Describe(bool madFace)
        {
            string text = madFace && Def.Dual ? Def.TextMad : Def.TextSane;
            return Placeholder.Replace(text ?? "", m => V(m.Groups[1].Value).ToString());
        }

        public abstract void Play(PlayContext ctx);

        /// <summary>回合结束时仍在手牌中（如疯语）。</summary>
        public virtual void OnTurnEndInHand(BattleEngine e) { }
    }

    /// <summary>
    /// 出牌上下文：卡牌通过它把效果按顺序排进队列。
    /// Then / IfClarity 里的效果在执行到那一步时才计算，并插在剩余效果之前（「清明：」在结算到该句时判定）。
    /// </summary>
    public sealed class PlayContext
    {
        public readonly BattleEngine Engine;
        public readonly Card Card;
        public readonly EnemyState Target;
        /// <summary>本次使用的是否为癫狂面（出牌第 ① 步锁定）。</summary>
        public readonly bool Mad;

        List<GameAction> _front;

        public PlayContext(BattleEngine engine, Card card, EnemyState target, bool mad)
        {
            Engine = engine;
            Card = card;
            Target = target;
            Mad = mad;
        }

        public PlayerState Player => Engine.Player;

        void Enqueue(GameAction a)
        {
            if (_front != null) _front.Add(a);
            else Engine.Queue.AddToBottom(a);
        }

        public void Damage(int amount, int hits = 1) => Enqueue(new DealDamageAction(Player, Target, amount, hits));
        public void Block(int amount) => Enqueue(new GainBlockAction(Player, amount, true));
        public void Draw(int count) => Enqueue(new DrawCardsAction(count));
        public void Energy(int amount) => Enqueue(new GainEnergyAction(amount));
        public void RestoreSanity(int amount) => Enqueue(new RestoreSanityAction(amount));
        public void LoseSanity(int amount) => Enqueue(new LoseSanityAction(amount, SanitySource.Effect));
        public void LoseHp(int amount) => Enqueue(new LoseHpAction(Player, amount));
        public void Fear(int amount) => Enqueue(new ApplyFearAction(Target, amount));
        public void Status(Combatant target, StatusId status, int amount) => Enqueue(new ApplyStatusAction(target, status, amount));
        public void DiscardRandom(int count) => Enqueue(new DiscardRandomAction(count));

        public void Then(Action<PlayContext> fn)
        {
            Enqueue(new DelegateAction(e =>
            {
                var outer = _front;
                _front = new List<GameAction>();
                fn(this);
                var produced = _front;
                _front = outer;
                e.Queue.AddToTop(produced.ToArray());
            }));
        }

        public void IfClarity(Action<PlayContext> fn) => Then(c => { if (c.Player.IsClarity) fn(c); });
    }
}
