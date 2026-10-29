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
        /// <summary>把一条命令交给内核执行；失败时给出原因。</summary>
        public delegate bool CommandSink(BattleCommand cmd, out string error);

        public readonly BattleEngine Engine;
        readonly BattleDirector _director;
        readonly CommandSink _execute;
        int _consumed;

        /// <summary>命令被内核拒绝时的原因（能量不足等），界面弹提示用。</summary>
        public event Action<string> Rejected;

        /// <summary>单场战斗（灰盒场景）：自己开一场，命令直接交给 BattleEngine。</summary>
        public BattleController(BattleSetup setup, BattleDirector director)
        {
            _director = director;
            Engine = BattleEngine.Start(setup);
            _execute = Engine.Execute;
        }

        /// <summary>跑局里的战斗：引擎归 RunEngine 所有，命令经它转发（打完那一刻它要结算奖励、存档）。</summary>
        public BattleController(BattleEngine engine, CommandSink execute, BattleDirector director)
        {
            _director = director;
            Engine = engine;
            _execute = execute;
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
        public bool UsePotion(int slot, int targetIndex = -1) => Run(BattleCommand.Potion(slot, targetIndex));

        bool Run(BattleCommand cmd)
        {
            if (!_director.IsIdle) return false;
            if (!_execute(cmd, out var error))
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

        /// <summary>这一批里还没播的事件（AudioDirector 往后看：这次扣格挡是不是被打的、这次掉理智是谁造成的）。</summary>
        public IEnumerable<BattleEvent> Upcoming => _queue;

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
                // 某个视图抛异常时只跳过这条表现，协程不能死：死了 _running 一直为真，输入就永久锁住
                float wait;
                try { wait = _screen.Apply(_queue.Dequeue()); }
                catch (Exception ex) { Debug.LogException(ex); wait = 0; }
                if (wait > 0) yield return new WaitForSeconds(wait / Mathf.Max(0.01f, Speed));
            }
            try { _screen.SyncAll(); }
            catch (Exception ex) { Debug.LogException(ex); }
            _running = false;
            _screen.OnDirectorIdle();
        }
    }
}
