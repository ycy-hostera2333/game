using System;
using System.Globalization;

namespace Deeptide.Core
{
    public enum CommandKind { PlayCard, EndTurn }

    /// <summary>玩家命令。表现层的所有输入最终都变成这个结构交给内核；回放就是按顺序重放它们。</summary>
    public readonly struct BattleCommand
    {
        public readonly CommandKind Kind;
        public readonly int HandIndex;
        public readonly int TargetIndex;

        BattleCommand(CommandKind kind, int handIndex, int targetIndex)
        {
            Kind = kind;
            HandIndex = handIndex;
            TargetIndex = targetIndex;
        }

        public static BattleCommand Play(int handIndex, int targetIndex = -1) => new BattleCommand(CommandKind.PlayCard, handIndex, targetIndex);

        public static BattleCommand End() => new BattleCommand(CommandKind.EndTurn, -1, -1);

        public override string ToString() =>
            Kind == CommandKind.EndTurn
                ? "end"
                : string.Format(CultureInfo.InvariantCulture, "play {0} {1}", HandIndex, TargetIndex);

        /// <summary>解析 ToString() 的输出，用于保存 / 读取回放文件。</summary>
        public static BattleCommand Parse(string text)
        {
            var parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && parts[0] == "end") return End();
            if (parts.Length == 3 && parts[0] == "play")
                return Play(int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture));
            throw new FormatException($"无法解析命令：{text}");
        }
    }
}
