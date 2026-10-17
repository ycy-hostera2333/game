namespace Deeptide.Core
{
    /// <summary>战斗规则参数，默认值见 策划/01 §2.1 与 02 §1。集中在这里，方便纸面原型验证后统一调整。</summary>
    public sealed class BattleConfig
    {
        public int EnergyPerTurn = 3;
        public int DrawPerTurn = 5;
        public int HandLimit = 10;

        /// <summary>理智 ≤ 此值即处于癫狂。</summary>
        public int LunacyThreshold = 4;

        /// <summary>清明：理智 ≥ 上限 − 此值（0 = 必须满理智）。默认 1：上限 10 时理智 9、10 都算清明（策划/07 §7.5）。</summary>
        public int ClarityMargin = 1;

        /// <summary>从第几回合开始侵蚀（回合开始时失去理智）。</summary>
        public int ErosionStartTurn = 4;
        public int ErosionAmount = 1;

        /// <summary>崩溃：失去最大生命的百分比（向下取整），之后理智重置为该值。</summary>
        public int CollapseHpPercent = 10;
        public int CollapseSanityReset = 3;

        /// <summary>每次震慑后意志 ×(Num/Den)，向上取整。默认 ×1.5。</summary>
        public int WillGrowthNum = 3;
        public int WillGrowthDen = 2;
    }
}
