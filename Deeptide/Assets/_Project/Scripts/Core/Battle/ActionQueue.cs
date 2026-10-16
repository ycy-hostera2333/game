using System.Collections.Generic;
using Deeptide.Core.Actions;

namespace Deeptide.Core
{
    /// <summary>
    /// 动作队列（策划/08 §3.1）：效果按入队顺序结算；触发器用 AddToTop 插队，先于剩余效果结算。
    /// </summary>
    public sealed class ActionQueue
    {
        readonly LinkedList<GameAction> _list = new LinkedList<GameAction>();

        public int Count => _list.Count;

        public void AddToBottom(GameAction action) => _list.AddLast(action);

        /// <summary>按参数顺序插到队首：AddToTop(a, b) 之后先执行 a 再执行 b。</summary>
        public void AddToTop(params GameAction[] actions)
        {
            for (int i = actions.Length - 1; i >= 0; i--)
                _list.AddFirst(actions[i]);
        }

        public void Clear() => _list.Clear();

        internal GameAction Pop()
        {
            var first = _list.First.Value;
            _list.RemoveFirst();
            return first;
        }
    }
}
