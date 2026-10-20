using System.Collections.Generic;
using Deeptide.Core;
using Deeptide.Core.Cards;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 手牌：扇形布局、悬停让位、拖拽出牌（策划/08 §9）。卡牌尺寸按 策划/10 §4.1：手牌 240 × 336，悬停放大到 1.5 倍。
    /// 悬停 / 拖动代价牌时让理智条预告要付的理智（10 §6.2）。
    /// 牌的增减只跟着 CardMoved 事件走；每批事件播完后再用 Sync 对一次内核状态，保证不会漂移。
    /// 坐标原点在屏幕底边中点（本节点 pivot = (0.5, 0)），单位是 1920×1080 参考分辨率。
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        /// <summary>不需要目标的牌拖过这条线（离底边的高度）就打出。</summary>
        public const float PlayLine = 400;
        public const float HoverScale = 1.5f;

        static readonly Vector2 DrawPilePos = new Vector2(-860, 80);
        static readonly Vector2 DiscardPilePos = new Vector2(860, 80);
        static readonly Vector2 ExhaustPilePos = new Vector2(860, 210);
        static readonly Vector2 PlaySpot = new Vector2(0, 540);
        static readonly Vector2 AimSpot = new Vector2(0, 380);

        readonly List<CardView> _cards = new List<CardView>();
        readonly List<CardView> _playing = new List<CardView>();
        BattleScreen _screen;
        RectTransform _root;
        CardView _hover, _dragging;
        bool _aiming;

        public IReadOnlyList<CardView> Cards => _cards;

        public void Init(BattleScreen screen)
        {
            _screen = screen;
            _root = (RectTransform)transform;
        }

        // ------------------------------------------------------------------ 事件

        /// <summary>处理一条 CardMoved 事件，返回建议的等待时间（秒）。</summary>
        public float OnCardMoved(BattleEvent e)
        {
            if (e.To == PileKind.Hand)
            {
                var back = _playing.Find(v => v.Uid == e.CardUid);
                if (back != null) _playing.Remove(back);
                if (back == null && Find(e.CardUid) != null) return 0;

                var view = back != null ? back : CardView.Create(_root, this, e.CardUid, e.Id);
                if (back == null)
                {
                    view.Rect.anchoredPosition = PilePos(e.From);
                    view.Rect.localScale = Vector3.one * 0.35f;
                }
                _cards.Insert(Mathf.Clamp(e.Value, 0, _cards.Count), view);
                _screen.RefreshCard(view);
                Layout();
                return 0.09f;
            }

            if (e.From == PileKind.Hand)
            {
                var view = Find(e.CardUid);
                if (view == null) return 0;
                _cards.Remove(view);
                if (_hover == view) SetHover(null);
                if (e.To == PileKind.Play)
                {
                    _playing.Add(view);
                    view.Group.blocksRaycasts = false;
                    view.Rect.SetAsLastSibling();
                    MoveTo(view, PlaySpot, 0, 1.15f, 0.15f);
                    Layout();
                    return 0.22f;
                }
                FlyAway(view, e.To);
                Layout();
                return 0.07f;
            }

            if (e.From == PileKind.Play)
            {
                var view = _playing.Find(v => v.Uid == e.CardUid);
                if (view == null) return 0;
                _playing.Remove(view);
                FlyAway(view, e.To);
                return 0.12f;
            }

            return 0;
        }

        /// <summary>和内核手牌对齐：补上缺的、删掉多的、按内核顺序重排。正常情况下什么都不用改。</summary>
        public void Sync(BattleEngine engine)
        {
            foreach (var v in _playing) Destroy(v.gameObject);
            _playing.Clear();

            var hand = engine.Player.Hand;
            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                if (hand.Exists(c => c.Uid == _cards[i].Uid)) continue;
                if (_hover == _cards[i]) _hover = null;
                Destroy(_cards[i].gameObject);
                _cards.RemoveAt(i);
            }
            for (int i = 0; i < hand.Count; i++)
            {
                int idx = _cards.FindIndex(v => v.Uid == hand[i].Uid);
                var view = idx >= 0 ? _cards[idx] : CardView.Create(_root, this, hand[i].Uid, hand[i].DeckId);
                if (idx >= 0) _cards.RemoveAt(idx);
                _cards.Insert(i, view);
            }
            Layout();
        }

        // ------------------------------------------------------------------ 布局

        public void Layout(float duration = 0.18f)
        {
            int n = _cards.Count;
            float spacing = n <= 1 ? 0 : Mathf.Min(185f, 1080f / (n - 1));
            int hover = _hover != null ? _cards.IndexOf(_hover) : -1;

            for (int i = 0; i < n; i++)
            {
                var view = _cards[i];
                if (view == _dragging) continue;
                float off = i - (n - 1) / 2f;
                var pos = new Vector2(off * spacing, 165 - off * off * 3.5f);
                float rot = -off * 3f;
                float scale = 1f;
                if (hover >= 0)
                {
                    if (i == hover)
                    {
                        // 放大后整张牌露出来：底边离屏幕底 10px
                        pos.y = CardView.Size.y * HoverScale / 2 + 10;
                        rot = 0;
                        scale = HoverScale;
                    }
                    else pos.x += Mathf.Sign(i - hover) * 70;
                }
                view.Rect.SetSiblingIndex(i);
                MoveTo(view, pos, rot, scale, duration);
            }
            if (hover >= 0) _cards[hover].Rect.SetAsLastSibling();
            foreach (var p in _playing) p.Rect.SetAsLastSibling();
        }

        static void MoveTo(CardView view, Vector2 pos, float rot, float scale, float duration)
        {
            Tween.StopAll(view.Rect);
            if (duration <= 0)
            {
                view.Rect.anchoredPosition = pos;
                view.Rect.localRotation = Quaternion.Euler(0, 0, rot);
                view.Rect.localScale = Vector3.one * scale;
                return;
            }
            Tween.UIAnchoredPosition(view.Rect, pos, duration, Ease.OutCubic);
            Tween.LocalRotation(view.Rect, Quaternion.Euler(0, 0, rot), duration, Ease.OutCubic);
            Tween.Scale(view.Rect, scale, duration, Ease.OutCubic);
        }

        void FlyAway(CardView view, PileKind to)
        {
            view.Group.blocksRaycasts = false;
            Tween.StopAll(view.Rect);
            const float d = 0.22f;
            if (to == PileKind.None)
            {
                Tween.Scale(view.Rect, 1.4f, d);
                Tween.Alpha(view.Group, 0, d);
            }
            else
            {
                Tween.UIAnchoredPosition(view.Rect, PilePos(to), d, Ease.InCubic);
                Tween.Scale(view.Rect, 0.3f, d, Ease.InCubic);
                Tween.LocalRotation(view.Rect, Quaternion.Euler(0, 0, to == PileKind.Draw ? 30 : -30), d);
                if (to == PileKind.Exhaust) Tween.Alpha(view.Group, 0, d);
            }
            Destroy(view.gameObject, d + 0.05f);
        }

        static Vector2 PilePos(PileKind pile)
        {
            switch (pile)
            {
                case PileKind.Draw: return DrawPilePos;
                case PileKind.Discard: return DiscardPilePos;
                case PileKind.Exhaust: return ExhaustPilePos;
                default: return PlaySpot;
            }
        }

        CardView Find(int uid) => _cards.Find(v => v.Uid == uid);

        // ------------------------------------------------------------------ 输入

        public void Hover(CardView view, bool on)
        {
            if (_dragging != null || !_cards.Contains(view)) return;
            if (on) SetHover(view);
            else if (_hover == view) SetHover(null);
            Layout(0.12f);
        }

        /// <summary>悬停的牌：显示双面牌的另一面，理智条预告它的代价。</summary>
        void SetHover(CardView view)
        {
            if (_hover != null && _hover != view) _hover.SetHover(false);
            _hover = view;
            if (view != null) view.SetHover(true);
            PreviewCost(view);
        }

        void PreviewCost(CardView view)
        {
            var engine = _screen.Controller?.Engine;
            var card = view != null && engine != null ? engine.FindCard(view.Uid) : null;
            _screen.Player.PreviewCost(engine, card);
        }

        public void BeginDrag(CardView view, PointerEventData e)
        {
            if (_dragging != null || !_cards.Contains(view)) return;
            var controller = _screen.Controller;
            if (!controller.AcceptsInput) return;
            int index = controller.HandIndexOf(view.Uid);
            if (!controller.Engine.CanPlay(index, out var error))
            {
                _screen.Toast(error);
                return;
            }

            SetHover(null);
            _dragging = view;
            PreviewCost(view);
            _aiming = controller.Engine.FindCard(view.Uid).Def.Target == CardTarget.Enemy;
            view.Group.blocksRaycasts = false;
            view.Rect.SetAsLastSibling();
            MoveTo(view, _aiming ? AimSpot : ToLocal(e.position), 0, 1.15f, _aiming ? 0.1f : 0);
            Layout(0.12f);
            Drag(view, e);
        }

        public void Drag(CardView view, PointerEventData e)
        {
            if (_dragging != view) return;
            var local = ToLocal(e.position);
            if (_aiming) _screen.Aim(AimSpot + new Vector2(0, CardView.Size.y * 0.6f), local, _screen.EnemyUnder(e));
            else view.Rect.anchoredPosition = local;
        }

        public void EndDrag(CardView view, PointerEventData e)
        {
            if (_dragging != view) return;
            _dragging = null;
            view.Group.blocksRaycasts = true;
            _screen.StopAim();
            PreviewCost(null);

            var controller = _screen.Controller;
            int index = controller.HandIndexOf(view.Uid);
            bool played = false;
            if (_aiming)
            {
                var enemy = _screen.EnemyUnder(e);
                if (enemy != null) played = controller.Play(index, enemy.Index);
            }
            else if (ToLocal(e.position).y > PlayLine) played = controller.Play(index);

            if (!played) Layout();
        }

        Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, _screen.Camera, out var local);
            return local;
        }
    }
}
