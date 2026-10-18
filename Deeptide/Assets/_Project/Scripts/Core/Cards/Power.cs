namespace Deeptide.Core.Cards
{
    /// <summary>
    /// 能力：能力牌打出后作为监听器常驻到战斗结束（策划/09 M1.2）。
    /// Id / Name 取自打出它的牌；再打出同一张能力牌时不新建实例，而是叠加 Amount。
    /// </summary>
    public abstract class Power : BattleListener
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public int Amount { get; internal set; }

        public override int Priority => 10;   // 遗物先结算，能力后结算

        public override string ToString() => $"{Name} {Amount}";
    }
}
