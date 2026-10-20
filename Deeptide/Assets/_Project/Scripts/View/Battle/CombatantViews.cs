using System.Collections.Generic;
using Deeptide.Core;
using Deeptide.Core.Cards;
using Deeptide.Core.Enemies;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>尺寸档（策划/10 §5.1）。</summary>
    public enum ActorTier { Small, Medium, Elite, Boss }

    /// <summary>
    /// 战斗舞台上的角色：纯墨剪影（10 §10 灰盒）+ 生命条 + 格挡 + 状态，玩家和敌人共用。数值由事件推进，不直接读内核。
    /// 所有子物件以本节点中心为锚，脚底在中心下方 <see cref="FeetY"/>：BattleScreen 只要把同一行的角色中心放在同一高度，脚底就对齐在同一条地平线上。
    /// </summary>
    public class CombatantView : MonoBehaviour
    {
        /// <summary>脚底相对本节点中心的高度。</summary>
        public const float FeetY = -160;

        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        protected RectTransform Body;
        protected Image BodyImage;
        protected Material BodyMaterial;
        protected float BodyHeight, BodyWidth, BarWidth;
        Vector2 _bodyHome;
        Sequence _lunge;
        float _breathPeriod, _breathPhase;

        RectTransform _hpFill, _statusRow;
        Image _hpFrame, _blockBadge;
        TextMeshProUGUI _hpText, _blockText;
        protected TextMeshProUGUI NameText;
        readonly List<GameObject> _chips = new List<GameObject>();
        readonly Dictionary<StatusId, int> _statusAmounts = new Dictionary<StatusId, int>();

        /// <summary>界面上记着的某个状态层数（飘字算差值用）。</summary>
        public int StatusAmount(StatusId id) => _statusAmounts.TryGetValue(id, out var v) ? v : 0;

        protected void RememberStatuses(Combatant c)
        {
            _statusAmounts.Clear();
            foreach (var kv in c.Statuses.Ordered()) _statusAmounts[kv.Key] = kv.Value;
        }

        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int Block { get; private set; }
        public ActorTier Tier { get; private set; }

        /// <summary>按尺寸档和剪影种类算出身体大小（1080p 显示高度，10 §5.1）。</summary>
        public static Vector2 BodySize(ActorTier tier, SilhouetteKind kind)
        {
            // Boss 受顶栏限制：地平线 440 + 470 + 意图 ≈ 1000，刚好在顶栏（1016）下面
            float h = tier == ActorTier.Small ? 210 : tier == ActorTier.Medium ? 340 : tier == ActorTier.Elite ? 430 : 470;
            var sprite = Shapes.Silhouette(kind);
            float aspect = sprite.rect.width / sprite.rect.height;
            // 横向的剪影（兽、鸦群）按「视觉体量」缩一些，否则会比同档的人形宽出一倍
            if (kind == SilhouetteKind.Beast) h *= 0.55f;
            else if (kind == SilhouetteKind.Swarm) h *= 0.72f;
            return new Vector2(h * aspect, h);
        }

        protected void BuildBase(string displayName, ActorTier tier, SilhouetteKind kind)
        {
            Tier = tier;
            var root = (RectTransform)transform;
            var size = BodySize(tier, kind);
            BodyWidth = size.x;
            BodyHeight = size.y;
            BarWidth = Mathf.Max(size.x, 180);
            root.sizeDelta = new Vector2(BarWidth + 40, 760);

            var sprite = Shapes.Silhouette(kind);
            BodyImage = Ui.Box("Body", root, Palette.Ink, true);
            BodyImage.sprite = sprite;
            Body = BodyImage.rectTransform;
            Body.anchorMin = Body.anchorMax = new Vector2(0.5f, 0.5f);
            // 精灵四周有留白，脚底在精灵 pivot.y 处：按这个比例把身体往下挪，让脚踩在地平线上
            float padBottom = sprite.pivot.y / sprite.rect.height;
            Body.pivot = new Vector2(0.5f, padBottom);
            Body.anchoredPosition = new Vector2(0, FeetY);
            Body.sizeDelta = size;
            _bodyHome = Body.anchoredPosition;

            var shader = Shader.Find("Deeptide/UI/Stage");
            if (shader != null)
            {
                BodyMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                BodyMaterial.SetColor("_FlashColor", Palette.PaperShade);
                BodyMaterial.SetColor("_FringeA", Palette.Lunacy);
                BodyMaterial.SetColor("_FringeB", Palette.BloodLight);
                BodyMaterial.SetFloat(SeedId, Random.value * 10);
                BodyImage.material = BodyMaterial;
            }
            _breathPeriod = Random.Range(2.4f, 3.2f);
            _breathPhase = Random.value * Mathf.PI * 2;

            NameText = Ui.Text("Name", root, displayName, 20, Palette.InkMid);
            NameText.richText = true;
            NameText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, FeetY + BodyHeight * (1 - padBottom) + 16), new Vector2(BarWidth + 120, 26));

            // 生命条：墨框、血色填充、数字居中（10 §6.4）
            var bar = Ui.Bar("Hp", root, Palette.HpBack, Palette.Hp, out _hpFill);
            bar.Place(new Vector2(0.5f, 0.5f), new Vector2(0, FeetY - 24), new Vector2(BarWidth, 22));
            _hpFrame = Ui.Box("Frame", bar, Palette.Ink);
            _hpFrame.sprite = Shapes.RoundRect(4, 2);
            _hpFrame.type = Image.Type.Sliced;
            _hpFrame.rectTransform.Fill(-2);
            _hpText = Ui.Text("Value", bar, "", 18, Palette.Paper);
            _hpText.font = Fonts.Serif;
            _hpText.fontStyle = FontStyles.Bold;
            _hpText.outlineWidth = 0.22f;
            _hpText.outlineColor = Palette.Ink;
            _hpText.rectTransform.Fill();

            _blockBadge = Ui.Box("Block", root, Palette.Ink);
            _blockBadge.sprite = Shapes.Shield;
            _blockBadge.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-BarWidth / 2 - 16, FeetY - 24), new Vector2(40, 45));
            _blockText = Ui.Text("Value", _blockBadge.transform, "", 21, Palette.Paper);
            _blockText.font = Fonts.Serif;
            _blockText.fontStyle = FontStyles.Bold;
            _blockText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(40, 30));
            _blockBadge.gameObject.SetActive(false);

            _statusRow = Ui.Rect("Statuses", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0, FeetY - 80), new Vector2(BarWidth + 80, 32));
            var layout = _statusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }

        protected virtual void OnDestroy()
        {
            if (_lunge.isAlive) _lunge.Stop();
            if (BodyMaterial != null)
            {
                Tween.StopAll(BodyMaterial);
                Destroy(BodyMaterial);
            }
        }

        protected virtual void Update()
        {
            if (Body == null) return;
            // 呼吸（10 §5.2）：Y 缩放 1.000 ↔ 1.015，轴心在脚底
            float s = 1 + 0.0075f * (1 + Mathf.Sin(Time.time * Mathf.PI * 2 / _breathPeriod + _breathPhase));
            Body.localScale = new Vector3(1, s, 1);
        }

        /// <summary>返回生命变化量（负数 = 受伤）。</summary>
        public int SetHp(int hp, int maxHp)
        {
            int delta = hp - Hp;
            Hp = hp;
            MaxHp = maxHp;
            Ui.SetFill(_hpFill, maxHp > 0 ? (float)hp / maxHp : 0);
            _hpText.text = $"{hp}/{maxHp}";
            return delta;
        }

        public int SetBlock(int block)
        {
            int delta = block - Block;
            Block = block;
            _blockBadge.gameObject.SetActive(block > 0);
            _blockText.text = block.ToString();
            // 有格挡时血条外框变粗
            _hpFrame.sprite = Shapes.RoundRect(4, block > 0 ? 4 : 2);
            return delta;
        }

        /// <summary>状态行：每个状态一枚小牌（几何 + 汉字，10 §10），层数写在右下角，悬停显示名字。</summary>
        protected void SetChips(IEnumerable<(string Glyph, int Amount, string Tip, Color Color)> chips)
        {
            // 死掉的敌人淡出后会被隐藏；对隐藏的 TMP 设描边会空引用，也没必要再画
            if (!gameObject.activeInHierarchy) return;
            foreach (var c in _chips) Destroy(c);
            _chips.Clear();
            foreach (var (glyph, amount, tip, color) in chips)
            {
                var chip = Ui.Box("Status", _statusRow, Palette.PaperMid);
                chip.sprite = Shapes.RoundRect(6);
                chip.type = Image.Type.Sliced;
                chip.rectTransform.sizeDelta = new Vector2(32, 32);
                var ring = Ui.Box("Ring", chip.transform, color);
                ring.sprite = Shapes.RoundRect(6, 2);
                ring.type = Image.Type.Sliced;
                ring.rectTransform.Fill();
                var g = Ui.Text("Glyph", chip.transform, glyph, 18, color);
                g.rectTransform.Fill();
                if (amount != 0)
                {
                    var n = Ui.Text("Amount", chip.transform, amount.ToString(), 14, Palette.Ink, TextAlignmentOptions.BottomRight);
                    n.font = Fonts.Serif;
                    n.fontStyle = FontStyles.Bold;
                    n.outlineWidth = 0.25f;
                    n.outlineColor = Palette.Paper;
                    n.rectTransform.Fill(-3);
                }
                string text = tip;
                TooltipTrigger.Add(chip, () => text);
                _chips.Add(chip.gameObject);
            }
        }

        protected static IEnumerable<(string, int, string, Color)> StatusChips(Combatant c, bool skipWard)
        {
            foreach (var kv in c.Statuses.Ordered())
            {
                if (skipWard && kv.Key == StatusId.Ward) continue;
                string name = StatusSet.NameOf(kv.Key);
                yield return (StatusGlyph(kv.Key), kv.Value, $"{name} {kv.Value}", StatusColor(kv.Key));
            }
        }

        static string StatusGlyph(StatusId id)
        {
            switch (id)
            {
                case StatusId.Strength: return "力";
                case StatusId.Dexterity: return "敏";
                case StatusId.Vulnerable: return "易";
                case StatusId.Weak: return "虚";
                case StatusId.Frail: return "脆";
                case StatusId.Bound: return "缚";
                case StatusId.Ward: return "心";
                default: return "?";
            }
        }

        static Color StatusColor(StatusId id)
        {
            switch (id)
            {
                case StatusId.Strength:
                case StatusId.Dexterity: return Palette.Ink;
                case StatusId.Ward: return Palette.Teal;
                default: return Palette.Blood;
            }
        }

        /// <summary>停掉身体上的动画并放回原位，再开始新的抖动 / 前冲，避免位置越抖越偏。</summary>
        protected void ResetBody()
        {
            // 前冲是序列，里面的子动画不能单独停（PrimeTween 会报错），先停整个序列
            if (_lunge.isAlive) _lunge.Stop();
            Tween.StopAll(Body);
            Body.anchoredPosition = _bodyHome;
        }

        /// <summary>受击（10 §5.2）：身体闪一下（纸底上的墨剪影闪成纸影色）+ 水平抖动 ±8px、0.15 秒。</summary>
        public void Shake()
        {
            ResetBody();
            Tween.ShakeLocalPosition(Body, new Vector3(8, 2, 0), 0.15f, 30);
            if (BodyMaterial != null)
            {
                Tween.StopAll(BodyMaterial);
                Tween.Custom(BodyMaterial, 1f, 0f, 0.12f, (m, v) => m.SetFloat(FlashId, v), Ease.InQuad);
            }
        }

        /// <summary>攻击前冲（10 §5.2）：向对手冲 40px（0.12 秒，缓出），0.25 秒回位。</summary>
        protected void LungeToward(float direction)
        {
            ResetBody();
            _lunge = Sequence.Create()
                .Chain(Tween.UIAnchoredPosition(Body, _bodyHome + new Vector2(40 * direction, 0), 0.12f, Ease.OutCubic))
                .Chain(Tween.UIAnchoredPosition(Body, _bodyHome, 0.25f, Ease.InOutSine));
        }

        /// <summary>死亡：溶解成墨向下流淌，0.6 秒（10 §5.2）。</summary>
        protected void Dissolve()
        {
            if (BodyMaterial == null) return;
            Tween.StopAll(BodyMaterial);
            Tween.Custom(BodyMaterial, 0f, 1f, 0.6f, (m, v) => m.SetFloat(DissolveId, v), Ease.InQuad);
        }

        /// <summary>飘字从这里冒出（世界坐标）：身体顶端。</summary>
        public Vector3 WorldTop => Body.TransformPoint(new Vector3(0, Body.rect.height * (1 - Body.pivot.y), 0));

        /// <summary>剪影上的一个小亮点（癫狂层的眼睛、调查员的提灯），位置按身体矩形的比例给。</summary>
        protected Image Dot(string name, Vector2 normalized, float size, Color color)
        {
            var dot = Ui.Box(name, Body, color);
            dot.sprite = Shapes.Circle;
            var rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = normalized;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            return dot;
        }
    }

    public sealed class EnemyView : CombatantView
    {
        public int Index { get; private set; }
        public string DefId { get; private set; }
        IntentBadge _intent;
        RectTransform _fearBar, _fearFill;
        TextMeshProUGUI _fearText;
        string _displayName;
        readonly List<Image> _eyes = new List<Image>();
        bool _dead;

        public bool IsDead => _dead;
        public IntentBadge Intent => _intent;
        /// <summary>当前显示的恐惧值（飘字算差值用）。</summary>
        public int Fear { get; private set; }

        public static EnemyView Create(Transform parent, int index, string displayName) => Create(parent, index, displayName, null);

        /// <param name="defId">敌人定义 ID（EnemySpawned 的 Id），决定尺寸档和剪影。</param>
        public static EnemyView Create(Transform parent, int index, string displayName, string defId)
        {
            var rt = Ui.Rect($"Enemy {index}", parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var view = rt.gameObject.AddComponent<EnemyView>();
            view.Index = index;
            view.DefId = defId;
            view._displayName = displayName;
            var (tier, kind) = Look(defId);
            view.BuildBase(displayName, tier, kind);
            view.BuildEnemy(kind);
            return view;
        }

        /// <summary>敌人 ID → 尺寸档 + 剪影（10 §5.3）。</summary>
        public static (ActorTier, SilhouetteKind) Look(string defId)
        {
            switch (defId)
            {
                case EnemyIds.Scaleling:
                case EnemyIds.Rat: return (ActorTier.Small, SilhouetteKind.Beast);
                case EnemyIds.Tentacle: return (ActorTier.Small, SilhouetteKind.Tentacle);
                case EnemyIds.Crows: return (ActorTier.Medium, SilhouetteKind.Swarm);
                case EnemyIds.Lighthouse:
                case EnemyIds.Nun:
                case EnemyIds.Fisherman: return (ActorTier.Elite, SilhouetteKind.Humanoid);
                case EnemyIds.Priest: return (ActorTier.Boss, SilhouetteKind.Humanoid);
                default: return (ActorTier.Medium, SilhouetteKind.Humanoid);
            }
        }

        void BuildEnemy(SilhouetteKind kind)
        {
            var root = (RectTransform)transform;
            float top = FeetY + BodyHeight * (1 - Body.pivot.y);
            _intent = IntentBadge.Create(root);
            ((RectTransform)_intent.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0, top + 60), new Vector2(200, 70));

            // 恐惧条（10 §6.4）：血条下方，恐惧紫填充
            _fearBar = Ui.Bar("Fear", root, Palette.PaperShade, Palette.Dread, out _fearFill);
            _fearBar.Place(new Vector2(0.5f, 0.5f), new Vector2(0, FeetY - 39), new Vector2(BarWidth, 8));
            _fearText = Ui.Text("FearText", root, "", 15, Palette.Dread);
            _fearText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, FeetY - 54), new Vector2(BarWidth + 40, 20));

            // 癫狂层：只在癫狂时显现的眼睛（10 §5.3「眼睛发青光」）
            foreach (var p in EyeSpots(kind)) _eyes.Add(Dot("Eye", p, Mathf.Clamp(BodyWidth * 0.06f, 7, 14), Palette.Lunacy));
        }

        static IEnumerable<Vector2> EyeSpots(SilhouetteKind kind)
        {
            switch (kind)
            {
                case SilhouetteKind.Beast: return new[] { new Vector2(0.17f, 0.52f), new Vector2(0.26f, 0.55f) };
                case SilhouetteKind.Tentacle: return new[] { new Vector2(0.55f, 0.8f), new Vector2(0.45f, 0.62f), new Vector2(0.56f, 0.44f) };
                case SilhouetteKind.Swarm: return new[] { new Vector2(0.24f, 0.6f), new Vector2(0.62f, 0.66f), new Vector2(0.45f, 0.38f), new Vector2(0.76f, 0.3f) };
                default: return new[] { new Vector2(0.42f, 0.83f), new Vector2(0.58f, 0.83f) };
            }
        }

        protected override void Update()
        {
            base.Update();
            float a = _dead ? 0 : Mathf.Clamp01((LunacyFx.Current - 0.2f) / 0.4f);
            foreach (var eye in _eyes)
            {
                var c = eye.color;
                c.a = a;
                eye.color = c;
            }
        }

        public void SetIntent(BattleEngine engine)
        {
            var en = engine.State.Enemies[Index];
            if (en.IsDead)
            {
                _intent.Clear();
                return;
            }
            _intent.Show(engine.GetIntent(en), engine.Player.IsLunatic);
            SetCounter(en);
        }

        readonly List<Image> _tideCells = new List<Image>();

        /// <summary>潮位水位计（10 §5.3「旁边是 5 格水位计」）：身体左侧一列格子，满格时变成发光青。只有带潮位计数器的敌人（溺亡司祭）才有。</summary>
        void SetTide(EnemyState en)
        {
            if (!en.Counters.ContainsKey(DrownedPriest.Tide) || !en.Def.Vars.TryGetValue("TideMax", out int max) || max <= 0) return;
            if (_tideCells.Count != max)
            {
                foreach (var c in _tideCells) Destroy(c.gameObject);
                _tideCells.Clear();
                const float height = 220, gap = 4;
                float h = (height - gap * (max - 1)) / max;
                var column = Ui.Rect("TideGauge", transform);
                column.Place(new Vector2(0.5f, 0.5f), new Vector2(-BodyWidth / 2 - 40, FeetY + 14 + height / 2), new Vector2(22, height));
                var label = Ui.Text("Label", column, "潮位", 15, Palette.Teal);
                label.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, 14), new Vector2(60, 20));
                for (int i = 0; i < max; i++)
                {
                    var cell = Ui.Box($"Tide {i + 1}", column, Palette.PaperShade);
                    cell.sprite = Shapes.RoundRect(4);
                    cell.type = Image.Type.Sliced;
                    var rt = cell.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                    rt.pivot = new Vector2(0.5f, 0);
                    rt.anchoredPosition = new Vector2(0, i * (h + gap));
                    rt.sizeDelta = new Vector2(22, h);
                    var ring = Ui.Box("Ring", rt, Palette.Teal);
                    ring.sprite = Shapes.RoundRect(4, 2);
                    ring.type = Image.Type.Sliced;
                    ring.rectTransform.Fill();
                    _tideCells.Add(cell);
                }
            }
            int tide = en.Counter(DrownedPriest.Tide);
            for (int i = 0; i < _tideCells.Count; i++)
                _tideCells[i].color = i < tide ? (tide >= max ? Palette.Lunacy : Palette.Teal) : Palette.PaperShade;
        }

        /// <summary>计数器（Boss 的潮位）接在名字后面，深渊青。</summary>
        void SetCounter(EnemyState en)
        {
            string counter = en.Behaviour.CounterText(en);
            NameText.text = string.IsNullOrEmpty(counter) ? _displayName : $"{_displayName}  <color={Ui.Hex(Palette.Teal)}>{counter}</color>";
        }

        public void SetStatuses(EnemyState en)
        {
            RememberStatuses(en);
            SetTide(en);
            var chips = new List<(string, int, string, Color)>(StatusChips(en, false));
            var passive = en.Behaviour.Passive;
            if (!string.IsNullOrEmpty(passive)) chips.Add(("被", 0, passive, Palette.InkMid));
            SetChips(chips);

            Fear = en.Fear;
            _fearBar.gameObject.SetActive(!en.Mindless);
            if (en.Mindless) _fearText.text = "无心智";
            else
            {
                Ui.SetFill(_fearFill, en.Will > 0 ? (float)en.Fear / en.Will : 0);
                _fearText.text = $"恐惧 {en.Fear} / {en.Will}";
            }
            _fearText.color = en.Mindless ? Palette.InkMid : Palette.Dread;
            SetCounter(en);
        }

        public void Lunge() => LungeToward(-1);

        public void Die()
        {
            _dead = true;
            var group = gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Dissolve();
            _intent.Clear();
            // 身体在着色器里溶解，条和文字一起淡出
            foreach (Transform child in transform)
            {
                if (child == Body) continue;
                // 不能用 ??：编辑器里 GetComponent 找不到时返回的是 Unity 的「假 null」
                var g = child.GetComponent<CanvasGroup>();
                if (g == null) g = child.gameObject.AddComponent<CanvasGroup>();
                Tween.Alpha(g, 0, 0.4f);
            }
        }
    }

    public sealed class PlayerView : CombatantView
    {
        public int Sanity { get; private set; }
        public int MaxSanity { get; private set; }
        public SanityGauge Gauge { get; private set; }

        int _lunacyLine = 4, _clarityMargin = 1;
        Image _lantern;
        readonly List<Image> _shadow = new List<Image>();

        public static PlayerView Create(Transform parent)
        {
            var rt = Ui.Rect("Player", parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var view = rt.gameObject.AddComponent<PlayerView>();
            view.BuildBase("调查员", ActorTier.Medium, SilhouetteKind.Humanoid);
            view.BuildPlayer();
            return view;
        }

        void BuildPlayer()
        {
            var root = (RectTransform)transform;
            // 理智条在调查员左侧（10 §6.1 / §6.2）
            Gauge = SanityGauge.Create(root);
            ((RectTransform)Gauge.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(-BarWidth / 2 - 150, FeetY + 230),
                new Vector2(46, SanityGauge.Height));

            // 提灯：清醒时是暖金，癫狂时火焰变青（10 §5.3）
            _lantern = Dot("Lantern", new Vector2(0.8f, 0.42f), 18, Palette.GoldLight);
            // 癫狂层：影子里长出的触须
            foreach (var (x, h, rot) in new[] { (-0.55f, 0.5f, 18f), (0.6f, 0.42f, -14f) })
            {
                var t = Ui.Box("Shadow", root, Palette.InkMid);
                t.sprite = Shapes.Silhouette(SilhouetteKind.Tentacle);
                var r = t.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.02f);
                r.sizeDelta = new Vector2(BodyHeight * h / 3, BodyHeight * h);
                r.anchoredPosition = new Vector2(BodyWidth * x, FeetY);
                r.localRotation = Quaternion.Euler(0, 0, rot);
                r.SetSiblingIndex(0);
                _shadow.Add(t);
            }
        }

        protected override void Update()
        {
            base.Update();
            float lun = LunacyFx.Current;
            _lantern.color = Color.Lerp(Palette.GoldLight, Palette.Lunacy, Mathf.Clamp01(lun * 2.5f));
            float a = Mathf.Clamp01((lun - 0.3f) / 0.5f) * 0.55f;
            foreach (var s in _shadow)
            {
                var c = s.color;
                c.a = a;
                s.color = c;
            }
        }

        /// <summary>理智：刷新理智条，并把强度交给 LunacyFx（10 §7.1）。返回变化量。</summary>
        public int SetSanity(int sanity, int max, int lunacyLine)
        {
            int delta = sanity - Sanity;
            Sanity = sanity;
            MaxSanity = max;
            _lunacyLine = lunacyLine;
            Gauge.SetValue(sanity, max, lunacyLine, max - _clarityMargin);
            if (LunacyFx.Instance != null) LunacyFx.Instance.SetSanity(sanity, max, lunacyLine, _clarityMargin);
            return delta;
        }

        /// <summary>
        /// 理智条的预告（10 §6.2）：敌人本回合的精神攻击总量（意图模糊时不显示，否则等于泄露数值）、
        /// 下回合开始的侵蚀和光环、心防层数。每批事件播完后调用。
        /// </summary>
        public void RefreshGauge(BattleEngine engine)
        {
            var p = engine.Player;
            _clarityMargin = p.ClarityMargin;
            int mind = 0;
            foreach (var en in engine.State.AliveEnemies)
            {
                if (en.Stunned || en.Intent == null) continue;
                var v = engine.GetIntent(en);
                if (!v.Hidden && (v.Type & IntentType.Mind) != 0) mind += v.SanityDamage;
            }
            bool ongoing = engine.State.Outcome == BattleOutcome.Ongoing;
            var (erosion, aura) = ongoing ? engine.UpcomingSanityLoss() : (0, 0);
            Gauge.SetForecast(ongoing ? mind : 0, !engine.IntentsBlurred, erosion, aura);
            Gauge.SetWard(p.Statuses.Get(StatusId.Ward));
            Gauge.SetValue(p.Sanity, p.MaxSanity, p.LunacyThreshold, p.ClarityThreshold);
        }

        /// <summary>悬停 / 拖动一张牌：预告它的理智代价（X 代价按当前能量算）。card 为 null 时清掉。</summary>
        public void PreviewCost(BattleEngine engine, Card card)
        {
            if (card == null || engine == null)
            {
                Gauge.SetCostPreview(0);
                return;
            }
            bool mad = engine.UsesMadFace(card);
            int x = card.Def.XCost ? engine.Player.Energy : 0;
            Gauge.SetCostPreview(card.SanityCost(mad, x));
        }

        public void SetStatuses(PlayerState p)
        {
            RememberStatuses(p);
            var chips = new List<(string, int, string, Color)>(StatusChips(p, true));
            foreach (var power in p.Powers)
                chips.Add((power.Name.Substring(0, 1), power.Amount, $"能力 · {power.Name} {power.Amount}", Palette.Gold));
            SetChips(chips);
            Gauge.SetWard(p.Statuses.Get(StatusId.Ward));
        }
    }
}
