using System;
using System.Globalization;

namespace Deeptide.Core
{
    public enum CommandKind { PlayCard, EndTurn, Choose, UsePotion }

    /// <summary>玩家命令。表现层的所有输入最终都变成这个结构交给内核；回放就是按顺序重放它们。</summary>
    public readonly struct BattleCommand : IEquatable<BattleCommand>
    {
        public readonly CommandKind Kind;
        /// <summary>出牌：手牌下标；用药水：药水栏下标；选择：选项下标。</summary>
        public readonly int Index;
        public readonly int TargetIndex;

        BattleCommand(CommandKind kind, int index, int targetIndex)
        {
            Kind = kind;
            Index = index;
            TargetIndex = targetIndex;
        }

        public int HandIndex => Index;

        public static BattleCommand Play(int handIndex, int targetIndex = -1) => new BattleCommand(CommandKind.PlayCard, handIndex, targetIndex);

        public static BattleCommand End() => new BattleCommand(CommandKind.EndTurn, -1, -1);

        /// <summary>回答 WaitingForChoice：选第 option 项（从 0 开始）。</summary>
        public static BattleCommand Choose(int option) => new BattleCommand(CommandKind.Choose, option, -1);

        public static BattleCommand Potion(int slot, int targetIndex = -1) => new BattleCommand(CommandKind.UsePotion, slot, targetIndex);

        public override string ToString()
        {
            switch (Kind)
            {
                case CommandKind.EndTurn: return "end";
                case CommandKind.Choose: return string.Format(CultureInfo.InvariantCulture, "choose {0}", Index);
                case CommandKind.UsePotion: return string.Format(CultureInfo.InvariantCulture, "potion {0} {1}", Index, TargetIndex);
                default: return string.Format(CultureInfo.InvariantCulture, "play {0} {1}", Index, TargetIndex);
            }
        }

        /// <summary>解析 ToString() 的输出，用于保存 / 读取回放文件。</summary>
        public static BattleCommand Parse(string text)
        {
            var parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int I(int k) => int.Parse(parts[k], CultureInfo.InvariantCulture);
            if (parts.Length == 1 && parts[0] == "end") return End();
            if (parts.Length == 3 && parts[0] == "play") return Play(I(1), I(2));
            if (parts.Length == 2 && parts[0] == "choose") return Choose(I(1));
            if (parts.Length == 3 && parts[0] == "potion") return Potion(I(1), I(2));
            throw new FormatException($"无法解析命令：{text}");
        }

        public bool Equals(BattleCommand other) => Kind == other.Kind && Index == other.Index && TargetIndex == other.TargetIndex;
        public override bool Equals(object obj) => obj is BattleCommand c && Equals(c);
        public override int GetHashCode() => ((int)Kind * 397 ^ Index) * 397 ^ TargetIndex;
    }
}
