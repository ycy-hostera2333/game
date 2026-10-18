using System;
using System.Globalization;

namespace Deeptide.Core.Run
{
    public enum RunCommandKind { Battle, Pick, DropPotion }

    /// <summary>
    /// 局层命令（策划/09 M4.5）。战斗中转发 BattleCommand；其余界面都是「选第几项」；药水随时可以丢。
    /// 同一种子 + 同一串命令 = 同样的整局。
    /// </summary>
    public readonly struct RunCommand : IEquatable<RunCommand>
    {
        public readonly RunCommandKind Kind;
        public readonly BattleCommand Battle;
        public readonly int Index;

        RunCommand(RunCommandKind kind, BattleCommand battle, int index)
        {
            Kind = kind;
            Battle = battle;
            Index = index;
        }

        public static RunCommand Fight(BattleCommand cmd) => new RunCommand(RunCommandKind.Battle, cmd, -1);

        /// <summary>选当前界面的第 index 项（从 0 开始）。</summary>
        public static RunCommand Pick(int index) => new RunCommand(RunCommandKind.Pick, default, index);

        /// <summary>丢掉第 slot 格药水。</summary>
        public static RunCommand DropPotion(int slot) => new RunCommand(RunCommandKind.DropPotion, default, slot);

        public override string ToString()
        {
            switch (Kind)
            {
                case RunCommandKind.Pick: return string.Format(CultureInfo.InvariantCulture, "pick {0}", Index);
                case RunCommandKind.DropPotion: return string.Format(CultureInfo.InvariantCulture, "drop {0}", Index);
                default: return Battle.ToString();
            }
        }

        public static RunCommand Parse(string text)
        {
            var parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0] == "pick") return Pick(int.Parse(parts[1], CultureInfo.InvariantCulture));
            if (parts.Length == 2 && parts[0] == "drop") return DropPotion(int.Parse(parts[1], CultureInfo.InvariantCulture));
            return Fight(BattleCommand.Parse(text));
        }

        public bool Equals(RunCommand other) => Kind == other.Kind && Index == other.Index && Battle.Equals(other.Battle);
        public override bool Equals(object obj) => obj is RunCommand c && Equals(c);
        public override int GetHashCode() => ((int)Kind * 397 ^ Index) * 397 ^ Battle.GetHashCode();
    }
}
