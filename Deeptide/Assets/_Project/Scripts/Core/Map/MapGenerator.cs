using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Deeptide.Core.Random;

namespace Deeptide.Core.Map
{
    public enum RoomType { Battle, Event, Campfire, Elite, Shop, Treasure, Boss }

    /// <summary>地图上的一个节点。Row 从 0 开始（第 1 层 = Row 0），Children 是下一行相连节点的列号。</summary>
    public sealed class MapNode
    {
        public int Row;
        public int Col;
        public RoomType Room;
        public List<int> Children = new List<int>();
        public List<int> Parents = new List<int>();

        public int Floor => Row + 1;
    }

    /// <summary>一张地图（纯数据，可以直接序列化进存档）。Boss 在第 Height + 1 层，最后一行的节点都连向它。</summary>
    public sealed class MapData
    {
        public int Width;
        public int Height;
        public List<MapNode> Nodes = new List<MapNode>();
        /// <summary>房间分配时抽 N 次都不满足「兄弟节点互不相同」而退回普通战斗的次数（软约束，用于统计）。</summary>
        public int Fallbacks;

        public MapNode Get(int row, int col) => Nodes.FirstOrDefault(n => n.Row == row && n.Col == col);

        public IEnumerable<MapNode> InRow(int row) => Nodes.Where(n => n.Row == row);

        public int BossFloor => Height + 1;
    }

    /// <summary>地图生成参数（策划/06 §2）。</summary>
    public sealed class MapConfig
    {
        public int Width = 7;
        public int Height = 15;
        public int Paths = 6;
        public int MaxTries = 20;
        /// <summary>非固定层的房间权重（百分比）。</summary>
        public Dictionary<RoomType, int> Weights = new Dictionary<RoomType, int>
        {
            [RoomType.Battle] = 53,
            [RoomType.Event] = 22,
            [RoomType.Campfire] = 12,
            [RoomType.Elite] = 14,
            [RoomType.Shop] = 5,
        };
        public int TreasureFloor = 9;
        /// <summary>第 1–N 层不出精英和篝火。</summary>
        public int NoEliteOrRestUntil = 5;
    }

    /// <summary>
    /// 地图生成（策划/06 §2）：在 7 × 15 的网格上画 6 条路径（第 1、2 条起点不同，边不交叉），
    /// 合并重叠的边、去掉孤立节点，再按固定层 + 权重分配房间，不满足约束就重抽，抽 N 次仍失败退回普通战斗。
    /// </summary>
    public static class MapGenerator
    {
        static readonly RoomType[] SpecialNoRepeat = { RoomType.Elite, RoomType.Campfire, RoomType.Shop };

        public static MapData Generate(Pcg32 rng, MapConfig cfg = null)
        {
            cfg = cfg ?? new MapConfig();
            int w = cfg.Width, h = cfg.Height;
            var edges = new HashSet<(int Row, int From, int To)>();

            int firstStart = -1;
            for (int p = 0; p < cfg.Paths; p++)
            {
                int col = rng.NextInt(w);
                if (p == 0) firstStart = col;
                else if (p == 1)
                    while (col == firstStart) col = rng.NextInt(w);
                for (int r = 0; r < h - 1; r++)
                {
                    var options = new List<int>();
                    for (int d = -1; d <= 1; d++)
                    {
                        int next = col + d;
                        if (next < 0 || next >= w) continue;
                        // X 形交叉：相邻两列之间已经有一条反方向的斜边
                        if (d != 0 && edges.Contains((r, next, col))) continue;
                        options.Add(next);
                    }
                    int to = options[rng.NextInt(options.Count)];
                    edges.Add((r, col, to));
                    col = to;
                }
            }

            var map = new MapData { Width = w, Height = h };
            var nodes = new Dictionary<(int, int), MapNode>();
            MapNode NodeAt(int r, int c)
            {
                if (!nodes.TryGetValue((r, c), out var n)) nodes[(r, c)] = n = new MapNode { Row = r, Col = c };
                return n;
            }
            foreach (var (row, from, to) in edges.OrderBy(e => e.Row).ThenBy(e => e.From).ThenBy(e => e.To))
            {
                var a = NodeAt(row, from);
                var b = NodeAt(row + 1, to);
                if (!a.Children.Contains(to)) a.Children.Add(to);
                if (!b.Parents.Contains(from)) b.Parents.Add(from);
            }
            map.Nodes = nodes.Values.OrderBy(n => n.Row).ThenBy(n => n.Col).ToList();
            foreach (var n in map.Nodes)
            {
                n.Children.Sort();
                n.Parents.Sort();
            }

            AssignRooms(map, rng, cfg);
            return map;
        }

        static void AssignRooms(MapData map, Pcg32 rng, MapConfig cfg)
        {
            int total = cfg.Weights.Values.Sum();
            foreach (var n in map.Nodes)
            {
                int floor = n.Floor;
                if (floor == 1) { n.Room = RoomType.Battle; continue; }
                if (floor == cfg.TreasureFloor) { n.Room = RoomType.Treasure; continue; }
                if (floor == map.Height) { n.Room = RoomType.Campfire; continue; }

                bool assigned = false;
                for (int t = 0; t < cfg.MaxTries && !assigned; t++)
                {
                    int roll = rng.NextInt(total);
                    var room = RoomType.Battle;
                    foreach (var kv in cfg.Weights)
                    {
                        if (roll < kv.Value) { room = kv.Key; break; }
                        roll -= kv.Value;
                    }
                    if (Allowed(map, n, room, cfg, checkSiblings: true))
                    {
                        n.Room = room;
                        assigned = true;
                    }
                }
                if (!assigned)
                {
                    n.Room = RoomType.Battle;
                    map.Fallbacks++;
                }
            }
        }

        static bool Allowed(MapData map, MapNode n, RoomType room, MapConfig cfg, bool checkSiblings)
        {
            int floor = n.Floor;
            if (floor <= cfg.NoEliteOrRestUntil && (room == RoomType.Elite || room == RoomType.Campfire)) return false;
            if (floor == map.Height - 1 && room == RoomType.Campfire) return false;
            var parents = n.Parents.Select(c => map.Get(n.Row - 1, c)).ToList();
            if (SpecialNoRepeat.Contains(room) && parents.Any(p => p.Room == room)) return false;
            if (checkSiblings)
                foreach (var p in parents)
                    foreach (var c in p.Children)
                    {
                        if (c >= n.Col) break;   // 同一行按列从左到右分配，只和已经分配过的兄弟比
                        if (map.Get(n.Row, c).Room == room) return false;
                    }
            return true;
        }

        /// <summary>硬约束检查（策划/06 §2.2）：返回所有违规的描述，空列表 = 合规。「兄弟节点互不相同」是软约束，不在这里。</summary>
        public static List<string> Violations(MapData map, MapConfig cfg = null)
        {
            cfg = cfg ?? new MapConfig();
            var result = new List<string>();
            foreach (var n in map.Nodes)
            {
                string at = $"第 {n.Floor} 层第 {n.Col} 列";
                if (n.Floor == 1 && n.Room != RoomType.Battle) result.Add($"{at}：第 1 层应为普通战斗");
                if (n.Floor == cfg.TreasureFloor && n.Room != RoomType.Treasure) result.Add($"{at}：第 {cfg.TreasureFloor} 层应为宝箱");
                if (n.Floor == map.Height && n.Room != RoomType.Campfire) result.Add($"{at}：第 {map.Height} 层应为篝火");
                if (n.Floor != 1 && n.Floor != cfg.TreasureFloor && n.Floor != map.Height && !Allowed(map, n, n.Room, cfg, false))
                    result.Add($"{at}：{n.Room} 违反约束");
                if (n.Row < map.Height - 1 && n.Children.Count == 0) result.Add($"{at}：没有出路");
                if (n.Row > 0 && n.Parents.Count == 0) result.Add($"{at}：没有入口");
            }
            for (int r = 1; r < map.Height; r++)
            {
                var edges = map.InRow(r - 1).SelectMany(n => n.Children.Select(c => (n.Col, c))).ToList();
                foreach (var (a, b) in edges)
                    if (b == a + 1 && edges.Contains((a + 1, a))) result.Add($"第 {r} → {r + 1} 层第 {a}–{a + 1} 列之间有交叉的边");
            }
            return result;
        }

        /// <summary>兄弟节点（同一父节点的子节点）房间相同的对数，不含固定层（宝箱层、篝火层本来就全是同一种）。</summary>
        public static int SiblingDuplicates(MapData map, MapConfig cfg = null)
        {
            cfg = cfg ?? new MapConfig();
            int n = 0;
            foreach (var p in map.Nodes)
            {
                int childFloor = p.Floor + 1;
                if (childFloor == cfg.TreasureFloor || childFloor == map.Height) continue;
                var rooms = p.Children.Select(c => map.Get(p.Row + 1, c).Room).ToList();
                n += rooms.Count - rooms.Distinct().Count();
            }
            return n;
        }

        public static char Symbol(RoomType room)
        {
            switch (room)
            {
                case RoomType.Battle: return 'M';
                case RoomType.Event: return '?';
                case RoomType.Campfire: return 'R';
                case RoomType.Elite: return 'E';
                case RoomType.Shop: return '$';
                case RoomType.Treasure: return 'T';
                default: return 'B';
            }
        }

        public const string Legend = "M 普通战斗  ? 事件  R 篝火  E 精英  $ 商店  T 宝箱  B Boss  @ 当前位置";

        /// <summary>字符画：上面是高层，每列占 4 个字符，节点之间用 | / \ 连线。</summary>
        public static string Render(MapData map, int curRow = -1, int curCol = -1)
        {
            var sb = new StringBuilder();
            int width = map.Width * 4;
            sb.Append(' ', width / 2 - 1).Append("[B]").AppendLine($"   第 {map.BossFloor} 层 Boss");
            for (int r = map.Height - 1; r >= 0; r--)
            {
                var line = new char[width];
                for (int i = 0; i < width; i++) line[i] = ' ';
                foreach (var n in map.InRow(r))
                    line[n.Col * 4 + 1] = n.Row == curRow && n.Col == curCol ? '@' : Symbol(n.Room);
                sb.Append(new string(line).TrimEnd()).Append("      ").AppendLine($"{r + 1,2}");
                if (r == 0) break;
                var edge = new char[width];
                for (int i = 0; i < width; i++) edge[i] = ' ';
                foreach (var n in map.InRow(r - 1))
                    foreach (var c in n.Children)
                    {
                        int x = n.Col * 4 + 1;
                        if (c == n.Col) edge[x] = '|';
                        else if (c == n.Col + 1) edge[x + 2] = '/';
                        else edge[x - 2] = '\\';
                    }
                sb.AppendLine(new string(edge).TrimEnd());
            }
            return sb.ToString();
        }
    }
}
