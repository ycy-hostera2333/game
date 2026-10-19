using System;
using System.Collections;
using System.Collections.Generic;
using Deeptide.Core;
using UnityEngine;

namespace Deeptide.View.Battle
{
    /// <summary>
    /// 表现层和内核之间唯一的入口：输入变成 BattleCommand 交给内核（策划/08 §2 原则 3），
    /// 内核瞬时结算后，把这次新产生的事件整批交给 BattleDirector 回放。
    /// </summary>
    public sealed class BattleController
    {
        public readonly BattleEngine Engine;
        readonly BattleDirector _director;
        int _consumed;

        /// <summary>命令被内核拒绝时的原因（能量不足等），界面弹提示用。</summary>
        public event Action<string> Rejected;

        public BattleController(BattleSetup setup, BattleDirector director)
        {
            _director = director;
            Engine = BattleEngine.Start(setup);
        }

        /// <summary>开始回放开场事件（建玩家、生成敌人、第一回合抽牌）。界面接好 Controller 之后再调用。</summary>
        public void Begin() => Flush();

        /// <summary>动画播完、轮到玩家行动时才接受输入。</summary>
        public bool AcceptsInput =>
            _director.IsIdle && Engine.State.Outcome == BattleOutcome.Ongoing && Engine.State.Phase == BattlePhase.PlayerAction;

        public bool AwaitingChoice =>
            _director.IsIdle && Engine.State.Outcome == BattleOutcome.Ongoing && Engine.State.Phase == BattlePhase.WaitingForChoice;

        public int HandIndexOf(int uid) => Engine.Player.Hand.FindIndex(c => c.Uid == uid);

        public bool Play(int handIndex, int targetIndex = -1) => Run(BattleCommand.Play(handIndex, targetIndex));
        public bool EndTurn() => Run(BattleCommand.End());
        public bool Choose(int option) => Run(BattleCommand.Choose(option));

        bool Run(BattleCommand cmd)
        {
            if (!_director.IsIdle) return false;
            if (!Engine.Execute(cmd, out var error))
            {
                Rejected?.Invoke(error);
                return false;
            }
            Flush();
            return true;
        }

        void Flush()
        {
            var events = Engine.Log.Events;
            var batch = new List<BattleEvent>(events.Count - _consumed);
            for (; _consumed < events.Count; _consumed++) batch.Add(events[_consumed]);
            _director.Play(batch);
        }
    }

    /// <summary>
    /// 按顺序回放事件（策划/08 §2 原则 2）：每条事件交给 BattleScreen 处理，按返回的时长等待。
    /// 一批播完后让界面和内核对一次账（BattleScreen.SyncAll）。
    /// </summary>
    public sealed class BattleDirector : MonoBehaviour
    {
        /// <summary>全局倍速（策划/08 §9「游戏速度」）。</summary>
        public float Speed = 1f;

        readonly Queue<BattleEvent> _queue = new Queue<BattleEvent>();
        BattleScreen _screen;
        bool _running;

        public bool IsIdle => !_running && _queue.Count == 0;

        public void Bind(BattleScreen screen) => _screen = screen;

        public void Play(IEnumerable<BattleEvent> batch)
        {
            foreach (var e in batch) _queue.Enqueue(e);
            if (!_running && _queue.Count > 0) StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            _running = true;
            while (_queue.Count > 0)
            {
                float wait = _screen.Apply(_queue.Dequeue());
                if (wait > 0) yield return new WaitForSeconds(wait / Mathf.Max(0.01f, Speed));
            }
            _screen.SyncAll();
            _running = false;
            _screen.OnDirectorIdle();
        }
    }
}
