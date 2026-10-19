using System.Collections.Generic;
using System.Text;
using Deeptide.Core;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deeptide.View.Battle
{
    /// <summary>生命条 + 格挡 + 状态行，玩家和敌人共用。数值由事件推进，不直接读内核。</summary>
    public class CombatantView : MonoBehaviour
    {
        protected RectTransform Body;
        Vector2 _bodyHome;
        RectTransform _hpFill;
        TextMeshProUGUI _hpText, _blockText, _statusText;
        Image _blockBadge;

        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int Block { get; private set; }

        protected void BuildBase(string displayName, Vector2 bodySize, Color bodyColor)
        {
            var root = (RectTransform)transform;
            var body = Ui.Box("Body", root, bodyColor, true);
            Body = body.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -bodySize.y / 2 - 40), bodySize);
            _bodyHome = Body.anchoredPosition;
            Ui.Text("Name", Body, displayName, 26, Palette.Text).rectTransform.Fill();

            var bar = Ui.Bar("Hp", root, Palette.HpBack, Palette.Hp, out _hpFill);
            bar.Place(new Vector2(0.5f, 1), new Vector2(0, -bodySize.y - 62), new Vector2(bodySize.x, 26));
            _hpText = Ui.Text("Value", bar, "", 20, Palette.Text);
            _hpText.rectTransform.Fill();

            _blockBadge = Ui.Box("Block", root, Palette.Block);
            _blockBadge.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(-bodySize.x / 2 - 4, -bodySize.y - 62), new Vector2(40, 40));
            _blockText = Ui.Text("Value", _blockBadge.transform, "", 22, Palette.Text);
            _blockText.fontStyle = FontStyles.Bold;
            _blockText.rectTransform.Fill();
            _blockBadge.gameObject.SetActive(false);

            _statusText = Ui.Text("Statuses", root, "", 18, Palette.TextDim);
            _statusText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -bodySize.y - 96), new Vector2(bodySize.x + 80, 26));
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
            return delta;
        }

        public void SetStatuses(string text) => _statusText.text = text;

        /// <summary>停掉身体上的动画并放回原位，再开始新的抖动 / 前冲，避免位置越抖越偏。</summary>
        protected void ResetBody()
        {
            Tween.StopAll(Body);
            Body.anchoredPosition = _bodyHome;
        }

        public void Shake()
        {
            ResetBody();
            Tween.ShakeLocalPosition(Body, new Vector3(18, 6, 0), 0.25f, 22);
        }

        /// <summary>飘字从这里冒出（世界坐标）。</summary>
        public Vector3 WorldTop => Body.TransformPoint(new Vector3(0, Body.rect.height / 2, 0));

        protected static string StatusLine(Combatant c, IEnumerable<string> extra = null)
        {
            var sb = new StringBuilder();
            foreach (var kv in c.Statuses.Ordered())
            {
                if (sb.Length > 0) sb.Append("  ");
                sb.Append(StatusSet.NameOf(kv.Key)).Append(' ').Append(kv.Value);
            }
            if (extra != null)
                foreach (var s in extra)
                {
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append(s);
                }
            return sb.ToString();
        }
    }

    public sealed class EnemyView : CombatantView
    {
        public int Index { get; private set; }
        TextMeshProUGUI _intent, _move;
        bool _dead;

        public bool IsDead => _dead;

        public static EnemyView Create(Transform parent, int index, string displayName)
        {
            var rt = Ui.Rect($"Enemy {index}", parent);
            rt.sizeDelta = new Vector2(300, 470);
            var view = rt.gameObject.AddComponent<EnemyView>();
            view.Index = index;
            view.BuildBase(displayName, new Vector2(240, 300), Palette.Panel);

            view._intent = Ui.Text("Intent", rt, "", 28, Palette.Text);
            view._intent.fontStyle = FontStyles.Bold;
            view._intent.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(300, 36));
            view._move = Ui.Text("Move", view.Body, "", 18, Palette.TextDim);
            view._move.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(220, 24));
            return view;
        }

        public void SetIntent(BattleEngine engine)
        {
            var en = engine.State.Enemies[Index];
            if (en.IsDead)
            {
                _intent.text = "";
                _move.text = "";
                return;
            }
            var v = engine.GetIntent(en);
            _intent.text = IntentText(v);
            _intent.color = v.Blurred ? Palette.Accent : (v.Type & IntentType.Attack) != 0 ? Palette.Bad : Palette.Text;
            _move.text = v.Hidden || v.Blurred ? "" : v.MoveName;
        }

        public void SetStatuses(EnemyState en)
        {
            var extra = new List<string>();
            if (!en.Mindless) extra.Add($"恐惧 {en.Fear}/{en.Will}");
            SetStatuses(StatusLine(en, extra));
        }

        static string IntentText(IntentView v)
        {
            if (v.Stunned) return "震慑";
            if (v.Hidden) return "？";
            var parts = new List<string>();
            if ((v.Type & IntentType.Attack) != 0)
                parts.Add(v.Blurred ? "攻击 ?" : v.Hits > 1 ? $"攻击 {v.Damage}×{v.Hits}" : $"攻击 {v.Damage}");
            if ((v.Type & IntentType.Mind) != 0) parts.Add(v.Blurred ? "精神 ?" : $"精神 {v.SanityDamage}");
            if ((v.Type & IntentType.Defend) != 0) parts.Add("防御");
            if ((v.Type & IntentType.Buff) != 0) parts.Add("强化");
            if ((v.Type & IntentType.Debuff) != 0) parts.Add("削弱");
            if ((v.Type & IntentType.Summon) != 0) parts.Add("召唤");
            if (parts.Count == 0) parts.Add("？");
            return string.Join(" · ", parts);
        }

        public void Lunge()
        {
            ResetBody();
            Tween.PunchLocalPosition(Body, new Vector3(-70, 0, 0), 0.3f, 1);
        }

        public void Die()
        {
            _dead = true;
            var group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Tween.Alpha(group, 0.25f, 0.4f);
            _intent.text = "";
            _move.text = "";
        }
    }

    public sealed class PlayerView : CombatantView
    {
        RectTransform _sanityFill;
        TextMeshProUGUI _sanityText;
        Image _sanityBar;

        public int Sanity { get; private set; }
        public int MaxSanity { get; private set; }

        public static PlayerView Create(Transform parent)
        {
            var rt = Ui.Rect("Player", parent);
            rt.sizeDelta = new Vector2(300, 470);
            var view = rt.gameObject.AddComponent<PlayerView>();
            view.BuildBase("调查员", new Vector2(200, 280), Palette.Panel);

            var bar = Ui.Bar("Sanity", rt, Palette.Panel, Palette.Accent, out view._sanityFill);
            bar.Place(new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(240, 26));
            view._sanityBar = view._sanityFill.GetComponent<Image>();
            view._sanityText = Ui.Text("Value", bar, "", 18, Palette.Text);
            view._sanityText.rectTransform.Fill();
            return view;
        }

        /// <summary>理智条：癫狂线以下变成暗色（策划/01 §2.1 的癫狂线 = 4）。</summary>
        public int SetSanity(int sanity, int max, int lunacyLine)
        {
            int delta = sanity - Sanity;
            Sanity = sanity;
            MaxSanity = max;
            Ui.SetFill(_sanityFill, max > 0 ? (float)sanity / max : 0);
            bool lunatic = sanity <= lunacyLine;
            _sanityBar.color = lunatic ? Palette.AccentDark : Palette.Accent;
            _sanityText.text = lunatic ? $"理智 {sanity}/{max} · 癫狂" : $"理智 {sanity}/{max}";
            return delta;
        }

        public void SetStatuses(PlayerState p) => SetStatuses(StatusLine(p));
    }
}
