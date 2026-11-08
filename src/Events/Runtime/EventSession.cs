using System;
using System.Collections;
using System.Collections.Generic;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 一次事件运行的资源边界：物品、订阅、协程和完成状态都归这里，
    /// 保证成功、失败、取消都只清理一次。
    /// </summary>
    internal sealed class EventSession
    {
        private static readonly Dictionary<GameGridInventory, EventSession> Active = new Dictionary<GameGridInventory, EventSession>();

        internal static void Cancel(GameGridInventory grid)
        {
            if (Active.TryGetValue(grid, out EventSession session)) session.End(false);
        }

        private readonly List<GameItem> _items = new List<GameItem>();
        private readonly List<Action> _cleanups = new List<Action>();
        private readonly List<Coroutine> _routines = new List<Coroutine>();
        private bool _ended;
        private bool _completionScheduled;

        internal EventSession(GameGridInventory grid, FlowRuntime runtime)
        {
            Active.Add(grid, this);
            Grid = grid;
            Runtime = runtime;
        }

        internal GameGridInventory Grid { get; }

        internal FlowRuntime Runtime { get; }

        internal void TrackItem(GameItem item)
        {
            _items.Add(item);
        }

        internal void AddCleanup(Action cleanup)
        {
            _cleanups.Add(cleanup);
        }

        internal void RunRoutine(IEnumerator routine)
        {
            _routines.Add(EventCoroutineHost.Run(routine));
        }

        internal void Complete()
        {
            End();
        }

        /// <summary>结束条件满足后先保留演示画面，再延迟清理。</summary>
        internal void CompleteAfter(float seconds)
        {
            if (_ended || _completionScheduled)
            {
                return;
            }

            _completionScheduled = true;
            RunRoutine(CompleteAfterRoutine(seconds));
        }

        private IEnumerator CompleteAfterRoutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            End();
        }

        internal void Fail(string reason)
        {
            if (!_ended)
            {
                FFModEntry.Log.Warning("[FluxForge.Events] " + reason);
            }

            End();
        }

        private void End(bool notifyCompletion = true)
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            Active.Remove(Grid);

            for (int i = 0; i < _routines.Count; i++)
            {
                EventCoroutineHost.Stop(_routines[i]);
            }

            _routines.Clear();

            for (int i = _cleanups.Count - 1; i >= 0; i--)
            {
                _cleanups[i].Invoke();
            }

            _cleanups.Clear();

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                _items[i].Destroy();
            }

            _items.Clear();
            GameGridInventory grid = Grid;
            if (notifyCompletion && FlowEventApi.EventCompleted != null)
            {
                FlowEventApi.EventCompleted(grid);
            }
        }
    }
}
