using System.Collections.Generic;
using System.Linq;
using Deeptide.Core.Map;
using Deeptide.Core.Run;
using Deeptide.View.Battle;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deeptide.View.Run
{
    /// <summary>
    /// 地图（策划/06 §2.3、10 §8）：竖向卷动的海图，底下是灰潮镇和栈桥（出发点），往上是外海，最上面的漩涡是 Boss。
    /// 没走过的路是点线，走过的是实墨线；悬停任意节点时加粗从当前位置通往它的所有路径；当前位置一个呼吸缩放的墨圈，
    /// 能去的下一层节点轻轻跳动。只在地图阶段能点。
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        public const float ArtWidth = 1920, ArtHeight = 3400;
        // 海图上的位置（从左上角量，像素）：第 1 层在栈桥上方，往上每层 140，第 15 层之上是漩涡中心的 Boss
        const float Row0Y = 2600, RowStep = 140, BossY = 345, StartX = 896, StartY = 2800, ColStep = 205;
        const float NodeSize = 84, IconSize = 62, BossSize = 230;
        const float LineWidth = 6, LineHover = 10, WalkWidth = 6;

        RunScreen _screen;
        RunEngine _run;
        MapData _map;
        ScrollRect _scroll;
        RectTransform _viewport, _content, _edges, _nodeLayer;
        TextMeshProUGUI _hint;
        RectTransform _start;
        Image _startRing;
        readonly Dictionary<(int Row, int Col), MapNodeView> _nodes = new Dictionary<(int, int), MapNodeView>();
        readonly List<Edge> _edgeViews = new List<Edge>();
        readonly HashSet<(int, int)> _choosable = new HashSet<(int, int)>();
        float? _pendingScroll;
        Material _artMat;

        sealed class Edge
        {
            public (int Row, int Col) From, To;
            public Image Line;
            public bool Walked;
        }

        public int NodeCount => _nodes.Count;
        public IEnumerable<(int Row, int Col)> Choosable => _choosable;
        /// <summary>悬停高亮着的路段数（测试用）。</summary>
        public int HighlightedEdges => _edgeViews.Count(e => e.Line.rectTransform.sizeDelta.y >= LineHover);
        public int WalkedEdges => _edgeViews.Count(e => e.Walked);
        public bool HasArt { get; private set; }

        public static MapView Create(RectTransform parent, RunScreen screen)
        {
            var root = Ui.Rect("Map", parent).Fill();
            var view = root.gameObject.AddComponent<MapView>();
            view._screen = screen;
            view.Build(root);
            return view;
        }

        void OnDestroy()
        {
            if (_artMat != null) Destroy(_artMat);
            if (_startRing != null) Tween.StopAll(_startRing.rectTransform);
            if (_bossFog != null) Tween.StopAll(_bossFog);
        }

        void Build(RectTransform root)
        {
            Ui.Box("Paper", root, Palette.Paper).rectTransform.Fill();

            var scrollRt = Ui.Rect("Scroll", root).Fill();
            scrollRt.offsetMax = new Vector2(0, -TopBar.Height);
            _scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 50;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.12f;

            var viewport = Ui.Box("Viewport", scrollRt, new Color(0, 0, 0, 0), true);
            _viewport = viewport.rectTransform.Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = _viewport;

            _content = Ui.Rect("Content", _viewport);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.sizeDelta = new Vector2(ArtWidth, ArtHeight);
            _scroll.content = _content;

            // 海图底图（打包贴图：R 淡墨，B 癫狂层，和战斗背景同一个着色器）；没有时纸色 + 斜排线
            var tex = ArtDatabase.Background("map");
            var shader = Shader.Find("Deeptide/UI/Backdrop");
            if (tex != null && shader != null)
            {
                _artMat = new Material(shader) { hideFlags = HideFlags.DontSave };
                _artMat.SetColor("_InkColor", Palette.InkMid);
                _artMat.SetColor("_MadColor", Palette.Teal);
                var art = Ui.Rect("Art", _content).Fill().gameObject.AddComponent<RawImage>();
                art.texture = tex;
                art.material = _artMat;
                art.color = Palette.Paper;
                art.raycastTarget = false;
                HasArt = true;
            }
            else
            {
                var hatch = Ui.Rect("Hatch", _content).Fill().gameObject.AddComponent<RawImage>();
                hatch.texture = Shapes.HatchTexture;
                hatch.uvRect = new Rect(0, 0, ArtWidth / 48f, ArtHeight / 48f);
                hatch.color = new Color(Palette.PaperShade.r, Palette.PaperShade.g, Palette.PaperShade.b, 0.6f);
                hatch.raycastTarget = false;
            }

            _edges = Ui.Rect("Edges", _content).Fill();
            _nodeLayer = Ui.Rect("Nodes", _content).Fill();

            _hint = Ui.Text("Hint", root, "", 30, Palette.Text, TextAlignmentOptions.Left);
            _hint.font = Fonts.Serif;
            _hint.fontStyle = FontStyles.Bold;
            _hint.outlineWidth = 0.25f;
            _hint.outlineColor = Palette.Paper;
            _hint.rectTransform.Place(new Vector2(0, 1), new Vector2(380, -TopBar.Height - 40), new Vector2(700, 50));

            BuildLegend(root);
        }

        /// <summary>右下角的图例（固定，不随地图卷动）。</summary>
        void BuildLegend(RectTransform root)
        {
            var rooms = new[] { RoomType.Battle, RoomType.Elite, RoomType.Event, RoomType.Shop, RoomType.Campfire, RoomType.Treasure, RoomType.Boss };
            var box = RunUi.Panel(root, "Legend", Vector2.zero, new Vector2(200, 60 + rooms.Length * 46));
            box.raycastTarget = false;
            var rt = box.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-130, 40 + rt.sizeDelta.y / 2);
            RunUi.Label(rt, "图例", new Vector2(0, rt.sizeDelta.y / 2 - 30), new Vector2(160, 36), 24).fontStyle = FontStyles.Bold;
            for (int i = 0; i < rooms.Length; i++)
            {
                float y = rt.sizeDelta.y / 2 - 74 - i * 46;
                var icon = Ui.Box("Icon", rt, Palette.Ink);
                icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-55, y), new Vector2(38, 38));
                if (!ArtSprites.Apply(icon, IconId(rooms[i]), IconColor(rooms[i])))
                {
                    icon.color = new Color(0, 0, 0, 0);
                    RunUi.Label(icon.transform, Glyph(rooms[i]), Vector2.zero, new Vector2(38, 38), 24, IconColor(rooms[i]));
                }
                RunUi.Label(rt, RunEngine.RoomName(rooms[i]), new Vector2(25, y), new Vector2(110, 36), 22, null, TextAlignmentOptions.Left);
            }
        }

        // ------------------------------------------------------------------ 坐标

        /// <summary>节点在海图上的位置（相对内容区的左上角，y 向下为负）。手绘感：每个节点按局种子偏一点点。</summary>
        Vector2 PositionOf(int row, int col)
        {
            if (row >= _map.Height) return new Vector2(0, -BossY);
            float x = (col - (_map.Width - 1) / 2f) * ColStep + Jitter(row, col, 1) * 30;
            float y = Row0Y - row * RowStep + Jitter(row, col, 2) * 20;
            return new Vector2(x, -y);
        }

        float Jitter(int row, int col, int k)
        {
            unchecked
            {
                uint h = (uint)(row * 73856093) ^ (uint)(col * 19349663) ^ (uint)(k * 83492791) ^ (uint)_run.State.Seed;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 1023) / 1023f * 2 - 1;
            }
        }

        static Vector2 StartPosition => new Vector2(StartX - ArtWidth / 2, -StartY);

        // ------------------------------------------------------------------ 刷新

        /// <summary>按当前跑局状态刷新：换了地图就重建，然后更新走过的路、当前位置、能去的节点，卷到当前位置附近。</summary>
        public void Refresh(RunEngine run)
        {
            _run = run;
            if (_map != run.State.Map) Rebuild(run.State.Map);
            UpdateBossFog(run);
            var s = run.State;
            bool choosing = s.Phase == RunPhase.Map;

            _choosable.Clear();
            if (choosing)
            {
                foreach (var n in run.NextNodes()) _choosable.Add((n.Row, n.Col));
                if (s.Row == _map.Height - 1) _choosable.Add((_map.Height, 0));
            }
            _hint.text = choosing ? run.Title : "";

            var visited = new HashSet<(int, int)>();
            for (int r = 0; r < s.Path.Count; r++) visited.Add((r, s.Path[r]));
            var current = s.Row < 0 ? ((int, int)?)null : (s.Row, s.Col);
            foreach (var kv in _nodes)
            {
                var key = kv.Key;
                var state = current.HasValue && key.Equals(current.Value) ? MapNodeView.Mark.Current
                    : visited.Contains(key) ? MapNodeView.Mark.Visited
                    : _choosable.Contains(key) ? MapNodeView.Mark.Next
                    : key.Item1 > s.Row ? MapNodeView.Mark.Ahead
                    : MapNodeView.Mark.Passed;
                kv.Value.SetMark(state);
            }
            SetStartMark(s.Row < 0);

            foreach (var e in _edgeViews)
                e.Walked = WalkedEdge(e, s);
            Highlight(null);
            ScrollTo(s.Row < 0 ? -1 : Mathf.Min(s.Row, _map.Height));
        }

        static bool WalkedEdge(Edge e, RunState s)
        {
            int r = e.From.Row;
            if (r == -1) return s.Path.Count > 0 && e.To.Col == s.Path[0];
            return r + 1 < s.Path.Count && s.Path[r] == e.From.Col && s.Path[r + 1] == e.To.Col;
        }

        void Rebuild(MapData map)
        {
            _map = map;
            foreach (Transform c in _edges) Destroy(c.gameObject);
            if (_bossFog != null) Tween.StopAll(_bossFog);
            foreach (Transform c in _nodeLayer) Destroy(c.gameObject);
            _nodes.Clear();
            _edgeViews.Clear();

            // 出发点：栈桥上的一个小墨圈
            _start = Ui.Rect("Start", _nodeLayer).Place(new Vector2(0.5f, 1), StartPosition, new Vector2(60, 60));
            var dot = Ui.Box("Dot", _start, Palette.Ink);
            dot.sprite = Shapes.Circle;
            dot.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
            _startRing = Ui.Box("Ring", _start, Palette.Ink);
            _startRing.sprite = Shapes.Ring(14);
            _startRing.rectTransform.Fill();
            var startLabel = RunUi.Label(_start, "出发", new Vector2(0, -46), new Vector2(120, 34), 24);
            startLabel.font = Fonts.Serif;
            startLabel.fontStyle = FontStyles.Bold;
            startLabel.outlineWidth = 0.25f;
            startLabel.outlineColor = Palette.Paper;

            foreach (var n in map.InRow(0)) AddEdge((-1, StartColumnKey), (0, n.Col), StartPosition, PositionOf(0, n.Col));
            foreach (var n in map.Nodes)
            {
                foreach (var c in n.Children) AddEdge((n.Row, n.Col), (n.Row + 1, c), PositionOf(n.Row, n.Col), PositionOf(n.Row + 1, c));
                if (n.Row == map.Height - 1) AddEdge((n.Row, n.Col), (map.Height, 0), PositionOf(n.Row, n.Col), PositionOf(map.Height, 0));
            }
            foreach (var n in map.Nodes) AddNode(n.Row, n.Col, n.Room);
            AddNode(map.Height, 0, RoomType.Boss);
            BuildBossFog(_nodes[(map.Height, 0)]);
        }

        // ------------------------------------------------------------------ Boss 节点的雾（策划/15 §1.3、09 M7.6）

        Image _bossFog, _bossShade, _bossPlate;
        TextMeshProUGUI _bossName;

        /// <summary>Boss 节点还被雾遮着（不知道是谁）。测试用。</summary>
        public bool BossFogged => _bossFog != null && _bossFog.gameObject.activeSelf;
        /// <summary>Boss 节点下面写出来的名字（拿了海图才有），没有为 null。测试用。</summary>
        public string BossNameShown => _bossName != null && _bossName.gameObject.activeSelf ? _bossName.text : null;

        /// <summary>雾：一层半透明的纸色圆盖住半沉的礼拜堂，中间一个深渊青的「?」，慢慢呼吸。知道是谁之后换成剪影和名字。</summary>
        void BuildBossFog(MapNodeView boss)
        {
            var rt = (RectTransform)boss.transform;
            _bossFog = Ui.Box("Fog", rt, new Color(Palette.PaperShade.r, Palette.PaperShade.g, Palette.PaperShade.b, 0.86f));
            _bossFog.sprite = Shapes.Circle;
            _bossFog.raycastTarget = false;
            _bossFog.rectTransform.Fill(-18);
            var mark = Ui.Text("Unknown", _bossFog.transform, "?", 150, Palette.Teal);
            mark.font = Fonts.Serif;
            mark.fontStyle = FontStyles.Bold;
            mark.rectTransform.Fill();
            Tween.Alpha(_bossFog, 0.7f, 2.4f, Ease.InOutSine, -1, CycleMode.Yoyo);

            // 知道是谁：礼拜堂前面垫一块纸色圆，上面是墨色剪影（青色的礼拜堂上看不清青色的剪影）
            _bossPlate = Ui.Box("Plate", rt, Palette.PaperMid);
            _bossPlate.sprite = Shapes.Circle;
            _bossPlate.raycastTarget = false;
            _bossPlate.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(BossSize * 0.62f, BossSize * 0.62f));
            _bossShade = Ui.Box("Silhouette", rt, Palette.Ink);
            _bossShade.raycastTarget = false;
            _bossShade.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(BossSize * 0.45f, BossSize * 0.6f));
            _bossName = RunUi.Label(rt, "", new Vector2(0, -BossSize * 0.58f), new Vector2(360, 44), 32, Palette.Teal);
            _bossName.font = Fonts.Serif;
            _bossName.fontStyle = FontStyles.Bold;
            _bossName.outlineWidth = 0.25f;
            _bossName.outlineColor = Palette.Paper;
            _bossName.name = "BossName";
        }

        void UpdateBossFog(RunEngine run)
        {
            if (_bossFog == null) return;
            string id = run.RevealedBoss;
            bool known = id != null;
            _bossFog.gameObject.SetActive(!known);
            _bossShade.gameObject.SetActive(known);
            _bossPlate.gameObject.SetActive(known);
            _bossName.gameObject.SetActive(known);
            if (!known) return;
            var enc = Deeptide.Core.Enemies.EncounterLibrary.Get(id);
            _bossName.text = enc.Name;
            _bossShade.sprite = Shapes.Silhouette(BossSilhouette(enc.Enemies[0]));
        }

        /// <summary>Boss 的剪影种类（地图上、进门揭晓用）：鳞母是兽形，其余人形。</summary>
        public static SilhouetteKind BossSilhouette(string enemyId) =>
            enemyId == Deeptide.Core.Enemies.EnemyIds.ScaleMother ? SilhouetteKind.Beast : SilhouetteKind.Humanoid;

        const int StartColumnKey = -1;

        void AddEdge((int, int) from, (int, int) to, Vector2 a, Vector2 b)
        {
            var line = Ui.Box($"Edge {from}->{to}", _edges, Palette.InkMid);
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // 两端各让出半个节点，线不压在图标上
            var d = b - a;
            float trimA = from.Item1 < 0 ? 24 : NodeSize * 0.5f;
            float trimB = to.Item1 >= _map.Height ? BossSize * 0.38f : NodeSize * 0.5f;
            var dir = d.normalized;
            var p0 = a + dir * trimA;
            var p1 = b - dir * trimB;
            rt.anchoredPosition = (p0 + p1) / 2;
            rt.sizeDelta = new Vector2(Mathf.Max(4, (p1 - p0).magnitude), LineWidth);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            _edgeViews.Add(new Edge { From = from, To = to, Line = line });
        }

        void AddNode(int row, int col, RoomType room)
        {
            bool boss = room == RoomType.Boss;
            float size = boss ? BossSize : NodeSize;
            var view = MapNodeView.Create(_nodeLayer, this, row, col, room, PositionOf(row, col), size, boss ? BossSize * 0.9f : IconSize);
            _nodes[(row, col)] = view;
        }

        void SetStartMark(bool here)
        {
            if (_start == null) return;
            Tween.StopAll(_startRing.rectTransform);
            _startRing.rectTransform.localScale = Vector3.one;
            _startRing.gameObject.SetActive(here);
            if (here) Tween.Scale(_startRing.rectTransform, 1.18f, 0.9f, Ease.InOutSine, -1, CycleMode.Yoyo);
        }

        // ------------------------------------------------------------------ 卷动

        /// <summary>把第 row 行（-1 = 出发点）卷到视口偏下的位置，让上面几层能看到。</summary>
        void ScrollTo(int row)
        {
            float y = row < 0 ? StartY : row >= _map.Height ? BossY : Row0Y - row * RowStep;
            _pendingScroll = y;
        }

        void LateUpdate()
        {
            if (!_pendingScroll.HasValue) return;
            float viewH = _viewport.rect.height;
            if (viewH <= 0) return;
            float top = Mathf.Clamp(_pendingScroll.Value - viewH * 0.68f, 0, ArtHeight - viewH);
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1 - top / (ArtHeight - viewH);
            _pendingScroll = null;
        }

        /// <summary>当前卷到的位置：视口顶边在海图上的 y（测试用）。</summary>
        public float ViewTop => (1 - _scroll.verticalNormalizedPosition) * (ArtHeight - _viewport.rect.height);

        /// <summary>节点中心在海图上的 y（测试用）。</summary>
        public float NodeY(int row, int col) => -PositionOf(row, col).y;

        public float ViewHeight => _viewport.rect.height;

        // ------------------------------------------------------------------ 交互

        public bool IsChoosable(int row, int col) => _choosable.Contains((row, col));

        public MapNodeView NodeAt(int row, int col) => _nodes.TryGetValue((row, col), out var n) ? n : null;

        /// <summary>点了一个节点：能去的就交给跑局界面（选项下标 = NextNodes 里的顺序，Boss 排最后）。</summary>
        public bool ClickNode(int row, int col)
        {
            if (!_choosable.Contains((row, col)) || _run == null) return false;
            var next = _run.NextNodes().ToList();
            int index = row >= _map.Height ? next.Count : next.FindIndex(n => n.Row == row && n.Col == col);
            return index >= 0 && _screen.Pick(index);
        }

        /// <summary>悬停节点：加粗从当前位置出发、能走到它的所有路段（target = null 时恢复）。</summary>
        public void Hover(MapNodeView target, bool on) => Highlight(on ? target : null);

        void Highlight(MapNodeView target)
        {
            HashSet<(int, int)> onPath = null;
            if (target != null && _run != null && (target.Row > _run.State.Row || _run.State.Row < 0))
            {
                var ahead = Descendants();
                var before = Ancestors((target.Row, target.Col));
                onPath = new HashSet<(int, int)>(ahead.Where(before.Contains));
                if (_run.State.Row >= 0) onPath.Add((_run.State.Row, _run.State.Col));
                else onPath.Add((-1, StartColumnKey));
            }
            foreach (var e in _edgeViews)
            {
                bool hot = onPath != null && onPath.Contains(e.From) && onPath.Contains(e.To);
                StyleEdge(e, hot);
            }
        }

        void StyleEdge(Edge e, bool hot)
        {
            var img = e.Line;
            var rt = img.rectTransform;
            if (e.Walked)
            {
                img.sprite = null;
                img.type = Image.Type.Simple;
                img.color = Palette.Ink;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, hot ? LineHover : WalkWidth);
                return;
            }
            float w = hot ? LineHover : LineWidth;
            img.sprite = Shapes.Dots;
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 8f / w;
            img.color = hot ? Palette.Ink : new Color(Palette.InkMid.r, Palette.InkMid.g, Palette.InkMid.b, 0.75f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, w);
        }

        /// <summary>从当前位置往上能走到的节点（不含当前位置）；还没出发时从第 1 层算起。</summary>
        HashSet<(int, int)> Descendants()
        {
            var result = new HashSet<(int, int)>();
            var frontier = new Queue<(int, int)>();
            var s = _run.State;
            if (s.Row < 0)
                foreach (var n in _map.InRow(0)) Enqueue((0, n.Col));
            else
                foreach (var c in ChildrenOf((s.Row, s.Col))) Enqueue(c);
            while (frontier.Count > 0)
                foreach (var c in ChildrenOf(frontier.Dequeue())) Enqueue(c);
            return result;

            void Enqueue((int, int) key)
            {
                if (result.Add(key)) frontier.Enqueue(key);
            }
        }

        HashSet<(int, int)> Ancestors((int Row, int Col) key)
        {
            var result = new HashSet<(int, int)> { key };
            var frontier = new Queue<(int, int)>();
            frontier.Enqueue(key);
            while (frontier.Count > 0)
            {
                var (row, col) = frontier.Dequeue();
                IEnumerable<(int, int)> parents;
                if (row >= _map.Height) parents = _map.InRow(_map.Height - 1).Select(n => (n.Row, n.Col));
                else if (row == 0) parents = Enumerable.Empty<(int, int)>();
                else parents = _map.Get(row, col).Parents.Select(p => (row - 1, p));
                foreach (var p in parents)
                    if (result.Add(p)) frontier.Enqueue(p);
            }
            return result;
        }

        IEnumerable<(int, int)> ChildrenOf((int Row, int Col) key)
        {
            if (key.Row >= _map.Height) yield break;
            if (key.Row == _map.Height - 1)
            {
                yield return (_map.Height, 0);
                yield break;
            }
            foreach (var c in _map.Get(key.Row, key.Col).Children) yield return (key.Row + 1, c);
        }

        // ------------------------------------------------------------------ 图标

        public static string IconId(RoomType room)
        {
            switch (room)
            {
                case RoomType.Battle: return "node_battle";
                case RoomType.Elite: return "node_elite";
                case RoomType.Event: return "node_event";
                case RoomType.Shop: return "node_shop";
                case RoomType.Campfire: return "node_rest";
                case RoomType.Treasure: return "node_treasure";
                default: return "node_boss";
            }
        }

        /// <summary>灰盒时的图标汉字（10 §10「几何 + 一个汉字」）。</summary>
        public static string Glyph(RoomType room)
        {
            switch (room)
            {
                case RoomType.Battle: return "战";
                case RoomType.Elite: return "精";
                case RoomType.Event: return "?";
                case RoomType.Shop: return "商";
                case RoomType.Campfire: return "火";
                case RoomType.Treasure: return "宝";
                default: return "渊";
            }
        }

        /// <summary>节点图标一律墨色，只有 Boss（深海之物）用深渊青（10 §2.2）。</summary>
        public static Color IconColor(RoomType room) => room == RoomType.Boss ? Palette.Teal : Palette.Ink;

        /// <summary>开局就知道的 Boss（回响「海图」）：名字 + 它的被动说明（15 §1.3「剪影、名字和考点」），不知道为 null。</summary>
        public string RevealedBossName
        {
            get
            {
                if (!(_run?.RevealedBoss is string id)) return null;
                var enc = Deeptide.Core.Enemies.EncounterLibrary.Get(id);
                var passive = Deeptide.Core.Enemies.EnemyLibrary.CreateBehaviour(Deeptide.Core.Enemies.EnemyLibrary.Get(enc.Enemies[0])).Passive;
                return string.IsNullOrEmpty(passive) ? enc.Name : $"{enc.Name}\n<size=80%>{passive}</size>";
            }
        }

        /// <param name="bossName">Boss 节点：知道是谁（海图）就写名字，否则只说不知道（策划/15 §1.3）。</param>
        public static string Describe(RoomType room, string bossName = null)
        {
            switch (room)
            {
                case RoomType.Battle: return "普通战斗\n一组普通敌人";
                case RoomType.Elite: return "精英\n强敌，打赢必得 1 件遗物";
                case RoomType.Event: return "事件\n不知道会遇到什么";
                case RoomType.Shop: return "商店\n买卡、遗物、药水，或者付钱删牌";
                case RoomType.Campfire: return "篝火\n休息、锻造或静思";
                case RoomType.Treasure: return "宝箱\n1 件遗物和一些金币";
                default: return bossName != null ? $"Boss\n{bossName}" : "Boss\n今晚站在那儿的，你还不知道是谁";
            }
        }
    }

    /// <summary>地图上的一个节点：纸色圆片垫底 + 图标；状态决定浓淡和动画。</summary>
    public sealed class MapNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum Mark { Ahead, Next, Current, Visited, Passed }

        public int Row { get; private set; }
        public int Col { get; private set; }
        public RoomType Room { get; private set; }
        public Mark State { get; private set; }

        MapView _map;
        RectTransform _rt;
        Image _icon, _ring;
        TextMeshProUGUI _glyph;

        public static MapNodeView Create(Transform parent, MapView map, int row, int col, RoomType room, Vector2 pos, float size, float iconSize)
        {
            var disc = Ui.Box($"Node {row},{col}", parent, Palette.Paper, true);
            disc.sprite = Shapes.Circle;
            var rt = disc.rectTransform.Place(new Vector2(0.5f, 1), pos, new Vector2(size, size));
            var view = disc.gameObject.AddComponent<MapNodeView>();
            view._map = map;
            view._rt = rt;
            view.Row = row;
            view.Col = col;
            view.Room = room;
            // Boss 节点的半沉礼拜堂本身就是一大片图形，圆片只留一点点边
            if (room == RoomType.Boss) disc.color = new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.75f);

            view._ring = Ui.Box("Ring", rt, Palette.Ink);
            view._ring.sprite = Shapes.Ring(12);
            view._ring.rectTransform.Fill(-10);
            view._ring.gameObject.SetActive(false);

            view._icon = Ui.Box("Icon", rt, MapView.IconColor(room));
            view._icon.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
            if (!ArtSprites.Apply(view._icon, MapView.IconId(room), MapView.IconColor(room)))
            {
                view._icon.color = new Color(0, 0, 0, 0);
                view._glyph = Ui.Text("Glyph", view._icon.transform, MapView.Glyph(room), room == RoomType.Boss ? 120 : 40, MapView.IconColor(room));
                view._glyph.font = Fonts.Serif;
                view._glyph.fontStyle = FontStyles.Bold;
                view._glyph.rectTransform.Fill();
            }
            TooltipTrigger.Add(disc, () => MapView.Describe(room, map.RevealedBossName));
            return view;
        }

        public void SetMark(Mark mark)
        {
            State = mark;
            Tween.StopAll(_rt);
            Tween.StopAll(_ring.rectTransform);
            _rt.localScale = Vector3.one;
            _ring.rectTransform.localScale = Vector3.one;
            var color = MapView.IconColor(Room);
            float alpha = mark == Mark.Ahead ? 0.6f : mark == Mark.Passed ? 0.3f : 1f;
            if (_glyph != null) _glyph.color = new Color(color.r, color.g, color.b, alpha);
            else _icon.color = new Color(color.r, color.g, color.b, alpha);

            _ring.gameObject.SetActive(mark == Mark.Current || mark == Mark.Visited);
            if (mark == Mark.Current) Tween.Scale(_ring.rectTransform, 1.15f, 0.9f, Ease.InOutSine, -1, CycleMode.Yoyo);
            if (mark == Mark.Next) Tween.Scale(_rt, 1.12f, 0.7f, Ease.InOutSine, -1, CycleMode.Yoyo);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _map.Hover(this, true);
            if (State == Mark.Next)
            {
                Tween.StopAll(_rt);
                Tween.Scale(_rt, 1.28f, 0.12f);
            }
        }

        public void OnPointerExit(PointerEventData e)
        {
            _map.Hover(this, false);
            if (State == Mark.Next) SetMark(Mark.Next);
        }

        public void OnPointerClick(PointerEventData e) => _map.ClickNode(Row, Col);

        // 呼吸、跳动都是无限循环的补间：节点销毁（重建地图、回到标题）时先停掉
        void OnDestroy()
        {
            Tween.StopAll(_rt);
            if (_ring != null) Tween.StopAll(_ring.rectTransform);
        }
    }
}
