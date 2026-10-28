using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Deeptide.Core.Actions;

namespace Deeptide.Core.Cards
{
    /// <summary>
    /// 卡牌静态数据，对应 Data/cards.csv 的一行（策划/08 §4）。
    /// Vars 中的约定键：Sanity / MadSanity = 清醒面 / 癫狂面的理智代价，SanityPerX = 每点 X 的理智代价，由引擎在出牌第 ③ 步统一支付。
    /// 名字里带 Damage 的变量是攻击伤害、带 Block 的是卡牌格挡，卡面预览会把它们换成修正后的数值。
    /// </summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public CardType Type;
        public Rarity Rarity;
        /// <summary>归属角色（characters.csv 的 id），空 = 通用。奖励、商店、事件只出本角色和通用的牌（策划/14 §1.1）。</summary>
        public string Character = "";
        public string Archetype;
        /// <summary>费用；X 费牌为 0，看 XCost。</summary>
        public int Cost;
        /// <summary>升级后的费用，-1 表示不变。</summary>
        public int UpgradedCost = -1;
        public bool XCost;
        public CardTarget Target;
        public bool Dual;
        public CardKeywords Keywords;
        /// <summary>升级后的关键词；null 表示和未升级相同。</summary>
        public CardKeywords? UpgradedKeywords;
        public Dictionary<string, int> Vars = new Dictionary<string, int>();
        public Dictionary<string, int> UpgradeVars = new Dictionary<string, int>();
        public string TextSane;
        public string TextMad;
        /// <summary>升级后的文本；为空时沿用未升级的文本（数值照样按升级后的变量替换）。</summary>
        public string TextSaneUp;
        public string TextMadUp;
        public Func<Card> Factory;

        public bool Exhaust => (Keywords & CardKeywords.Exhaust) != 0;
        public bool Ethereal => (Keywords & CardKeywords.Ethereal) != 0;
        public bool Unplayable => (Keywords & CardKeywords.Unplayable) != 0;
    }

    [Flags]
    public enum CardKeywords
    {
        None = 0,
        /// <summary>消耗：打出后移出本场战斗。</summary>
        Exhaust = 1,
        /// <summary>虚无：回合结束时若在手中，则消耗。</summary>
        Ethereal = 2,
        /// <summary>不能打出。</summary>
        Unplayable = 4,
        /// <summary>固有：战斗开始时一定在起手。</summary>
        Innate = 8,
        /// <summary>保留：回合结束时不弃掉。</summary>
        Retain = 16,
    }

    /// <summary>一张具体的牌（牌组里的每张实体牌各是一个实例）。逻辑写在子类的 Play 里，数值全部通过 V() 读取。</summary>
    public abstract class Card
    {
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        public CardDef Def { get; private set; }
        /// <summary>本场战斗内唯一编号，用于哈希和表现层追踪。</summary>
        public int Uid { get; private set; }
        public bool Upgraded { get; private set; }
        /// <summary>本回合的临时费用（禁书残页），-1 = 没有。离开手牌或回合结束时清掉。</summary>
        public int CostThisTurn { get; internal set; } = -1;
        /// <summary>最近一次移动到的位置（引擎维护；正在结算的牌是 Play）。</summary>
        public PileKind Pile { get; internal set; } = PileKind.None;

        internal void Init(CardDef def, int uid, bool upgraded)
        {
            Def = def;
            Uid = uid;
            Upgraded = upgraded;
        }

        public string Id => Def.Id;
        public string Name => Upgraded ? Def.Name + "+" : Def.Name;
        /// <summary>带升级标记的 ID（如「C06+」），牌组和存档都用这个写法。</summary>
        public string DeckId => Upgraded ? Def.Id + "+" : Def.Id;
        /// <summary>印在牌上的费用（不含临时修正）；X 费牌为 0。</summary>
        public int Cost => Upgraded && Def.UpgradedCost >= 0 ? Def.UpgradedCost : Def.Cost;
        public CardKeywords Keywords => Upgraded && Def.UpgradedKeywords.HasValue ? Def.UpgradedKeywords.Value : Def.Keywords;
        public bool Has(CardKeywords k) => (Keywords & k) != 0;
        public bool Playable => !Has(CardKeywords.Unplayable);
        public bool Exhaust => Has(CardKeywords.Exhaust);
        public bool Ethereal => Has(CardKeywords.Ethereal);

        public int V(string key)
        {
            if (Upgraded && Def.UpgradeVars.TryGetValue(key, out var up)) return up;
            return Def.Vars.TryGetValue(key, out var v) ? v : 0;
        }

        public bool HasVar(string key) => Def.Vars.ContainsKey(key);

        /// <summary>出牌第 ③ 步要支付的理智代价。x 是 X 费牌这次的 X。</summary>
        public int SanityCost(bool madFace, int x = 0) => V(madFace && Def.Dual ? "MadSanity" : "Sanity") + x * V("SanityPerX");

        public string RawText(bool madFace)
        {
            bool mad = madFace && Def.Dual;
            string up = Upgraded ? (mad ? Def.TextMadUp : Def.TextSaneUp) : null;
            return !string.IsNullOrEmpty(up) ? up : mad ? Def.TextMad : Def.TextSane;
        }

        /// <summary>不带任何修正的牌面文本（图鉴、奖励界面）。</summary>
        public string Describe(bool madFace) =>
            Placeholder.Replace(RawText(madFace) ?? "", m => V(m.Groups[1].Value).ToString());

        /// <summary>战斗中的牌面文本：用当前生效的那一面，伤害 / 格挡换成修正后的数值（策划/02 §3.1「卡面实时显示修正后的值」）。</summary>
        public string Describe(BattleEngine e, EnemyState target = null)
        {
            bool mad = e.UsesMadFace(this);
            string text = Placeholder.Replace(RawText(mad) ?? "", m => PreviewValue(e, m.Groups[1].Value, target).ToString());
            string note = PreviewNote(e, mad, target);
            return note == null ? text : text + note;
        }

        /// <summary>变量的预览值：攻击伤害走 DamageCalculator，卡牌格挡走 BlockCalculator，和实际结算是同一条路径。</summary>
        public int PreviewValue(BattleEngine e, string key, EnemyState target = null)
        {
            int v = V(key);
            if (key.Contains("Damage")) return DamageCalculator.Compute(e, e.Player, target, v);
            if (key.Contains("Block")) return BlockCalculator.Compute(e, e.Player, v, true);
            return v;
        }

        /// <summary>牌面末尾的动态说明（如明理之刃「（当前 21）」）。</summary>
        protected virtual string PreviewNote(BattleEngine e, bool mad, EnemyState target) => null;

        /// <summary>
        /// 预览这次打出对 target 造成的攻击伤害：(每段伤害, 段数)；不造成单体攻击伤害返回 null。
        /// 默认按 Damage / MadDamage、Hits / MadHits 变量计算；伤害依赖其他条件的牌自己重写。
        /// </summary>
        public virtual (int PerHit, int Hits)? PreviewAttack(BattleEngine e, bool mad, EnemyState target)
        {
            if (Def.Type != CardType.Attack || Def.Target != CardTarget.Enemy) return null;
            string dmgKey = mad && HasVar("MadDamage") ? "MadDamage" : "Damage";
            if (!HasVar(dmgKey)) return null;
            int hits = mad && HasVar("MadHits") ? V("MadHits") : HasVar("Hits") ? V("Hits") : 1;
            return (DamageCalculator.Compute(e, e.Player, target, V(dmgKey)), hits);
        }

        public abstract void Play(PlayContext ctx);

        /// <summary>回合结束时仍在手牌中（如疯语）。</summary>
        public virtual void OnTurnEndInHand(BattleEngine e) { }

        /// <summary>回合结束时仍在手牌中会失去的理智（疯语）。理智条预告用，OnTurnEndInHand 实际扣的也是这个数。</summary>
        public virtual int TurnEndSanityLoss => 0;
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
        /// <summary>X 费牌这次的 X（打出时的全部能量）。</summary>
        public readonly int X;

        List<GameAction> _front;

        public PlayContext(BattleEngine engine, Card card, EnemyState target, bool mad, int x = 0)
        {
            Engine = engine;
            Card = card;
            Target = target;
            Mad = mad;
            X = x;
        }

        public PlayerState Player => Engine.Player;

        public void Enqueue(GameAction a)
        {
            if (_front != null) _front.Add(a);
            else Engine.Queue.AddToBottom(a);
        }

        public void Damage(int amount, int hits = 1) => Enqueue(new DealDamageAction(Player, Target, amount, hits));
        public void DamageAll(int amount, int hits = 1) => Enqueue(new DamageAllEnemiesAction(Player, amount, hits));
        public void Block(int amount) => Enqueue(new GainBlockAction(Player, amount, true));
        public void Draw(int count) => Enqueue(new DrawCardsAction(count));
        public void Energy(int amount) => Enqueue(new GainEnergyAction(amount));
        public void RestoreSanity(int amount) => Enqueue(new RestoreSanityAction(amount));
        public void LoseSanity(int amount) => Enqueue(new LoseSanityAction(amount, SanitySource.Effect));
        public void LoseHp(int amount) => Enqueue(new LoseHpAction(Player, amount));
        public void Fear(int amount) => Enqueue(new ApplyFearAction(Target, amount));
        public void FearAll(int amount) => Enqueue(new FearAllEnemiesAction(amount));
        public void Status(Combatant target, StatusId status, int amount) => Enqueue(new ApplyStatusAction(target, status, amount));
        public void StatusAll(StatusId status, int amount) => Enqueue(new StatusAllEnemiesAction(status, amount));
        public void DiscardRandom(int count) => Enqueue(new DiscardRandomAction(count));
        public void AddCard(string cardId, PileKind pile, int tempCost = -1) => Enqueue(new AddCardToPileAction(cardId, pile, tempCost));
        public void Choose(string prompt, Func<BattleEngine, List<ChoiceOption>> options, Action<BattleEngine, ChoiceOption> onChosen) =>
            Enqueue(new ChooseAction(prompt, options, onChosen));

        /// <summary>打出能力牌：能力以这张牌的 ID 和名字常驻到战斗结束，同名能力叠加 amount。</summary>
        public void ApplyPower(Power power, int amount)
        {
            power.Id = Card.Id;
            power.Name = Card.Def.Name;
            Enqueue(new DelegateAction(e =>
            {
                var p = e.ApplyPower(power, amount);
                var ev = e.Log.Add(BattleEventType.PowerApplied, $"获得能力「{p.Name}」（{p.Amount}）", e.Player.Name);
                ev.Id = p.Id;
                ev.Value = p.Amount;
            }));
        }

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
