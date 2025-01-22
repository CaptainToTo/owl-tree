
using System;
using System.Collections.Generic;

namespace OwlTree.StateMachine
{
    /// <summary>
    /// A hierarchical finite state machine. Type T represents a data container that can be passed
    /// to states' update methods.
    /// </summary>
    public class StateMachine
    {
        public delegate void SingleState(State state);
        public delegate void DoubleState(State state1, State state2);
        public delegate void DoubleStateInd(State state1, State state2, int ind);
        public delegate void StateIndex(int i, State state);

        private State _root;
        private IStateData _swapData;

        /// <summary>
        /// Get's an iterable of the active state chain.
        /// </summary>
        public IEnumerable<State> States => GetEnumerable();

        private IEnumerable<State> GetEnumerable()
        {
            var cur = _root;
            while (cur != null)
            {
                yield return cur;
                cur = cur.SubState;
            }
        }

        /// <summary>
        /// The length of the state chain. The number of active states in this machine.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Creates a new state machine, using the given states as the initial state chain.
        /// </summary>
        public StateMachine(IEnumerable<State> states, IStateData swapData)
        {
            _swapData = swapData;
            var prev = _root;
            foreach (var state in states)
            {
                if (_root == null)
                    _root = state;
                else
                {
                    prev.SubState = state;
                    state.SuperState = prev;
                }
                state.Machine = this;
                state.IsActive = true;
                prev = state;
                Count++;
            }

            foreach (var state in states)
                state.OnEnter(null, _swapData);
        }

        /// <summary>
        /// Creates a new state machine, using the given state as the root of the active state chain.
        /// </summary>
        public StateMachine(State root, IStateData swapData)
        {
            _swapData = swapData;
            Count = 1;
            _root = root;
            _root.Machine = this;
            _root.IsActive = true;
            _root.OnEnter(null, _swapData);
        }

        /// <summary>
        /// Run all active states' logical update.
        /// </summary>
        public void LogicUpdate(IStateData data)
        {
            var cur = _root;
            while (cur != null)
            {
                cur.LogicUpdate(data);
                cur = cur.SubState;
            }
        }

        /// <summary>
        /// Run all active states' physics update.
        /// </summary>
        public void PhysicsUpdate(IStateData data)
        {
            var cur = _root;
            while (cur != null)
            {
                cur.PhysicsUpdate(data);
                cur = cur.SubState;
            }
        }

        /// <summary>
        /// Run all active states' render update.
        /// </summary>
        public void RenderUpdate(IStateData data)
        {
            var cur = _root;
            while (cur != null)
            {
                cur.RenderUpdate(data);
                cur = cur.SubState;
            }
        }
        
        /// <summary>
        /// Gets the state at the specified index on the state chain, where the root state is index 0.
        /// </summary>
        public State Get(int i)
        {
            if (i < 0 || Count <= i)
                throw new IndexOutOfRangeException($"Cannot remove state at an invalid index");
            
            var cur = _root;
            while (i > 0)
            {
                cur = cur.SubState;
                i--;
            }
            return cur;
        }

        /// <summary>
        /// Gets the first state that is of the given type S. Returns null if no such state exists
        /// in the active state chain.
        /// </summary>
        public S Get<S>() where S : State
        {
            var t = typeof(S);
            var cur = _root;
            while (cur != null)
            {
                if (cur.GetType() == t)
                    return (S)cur;
                cur = cur.SubState;
            }
            return null;
        }

        /// <summary>
        /// Gets the first state that is of the given type s. Returns null if no such state exists
        /// in the active state chain.
        /// </summary>
        public State Get(Type s)
        {
            var cur = _root;
            while (cur != null)
            {
                if (cur.GetType() == s)
                    return cur;
                cur = cur.SubState;
            }
            return null;
        }

        /// <summary>
        /// Gets the index of the given state. If the state cannot be found in the active state chain,
        /// returns -1.
        /// </summary>
        public int IndexOf(State state)
        {
            if (state == null)
                return -1;
            
            var cur = _root;
            int i = 0;
            while (cur != null)
            {
                if (cur == state)
                    return i;
                cur = cur.SubState;
                i++;
            }
            return -1;
        }

        /// <summary>
        /// Gets the index of the first state found with the given type. If such a state cannot be found in the active state chain,
        /// returns -1.
        /// </summary>
        public int IndexOf<S>() where S : State
        {
            var t = typeof(S);
            var cur = _root;
            int i = 0;
            while (cur != null)
            {
                if (cur.GetType() == t)
                    return i;
                cur = cur.SubState;
                i++;
            }
            return -1;
        }

        /// <summary>
        /// Returns true if this machine contains the given state in the active state chain.
        /// </summary>
        public bool Contains(State state)
        {
            if (state == null)
                return false;

            var cur = _root;
            while (cur != null)
            {
                if (cur == state)
                    return true;
                cur = cur.SubState;
            }
            return false;
        }

        /// <summary>
        /// Returns true if this machine contains a state with the given type in the active state chain.
        /// </summary>
        public bool Contains<S>() where S : State
        {
            var t = typeof(S);
            var cur = _root;
            while (cur != null)
            {
                if (cur.GetType() == t)
                    return true;
                cur = cur.SubState;
            }
            return false;
        }

        public event DoubleStateInd OnStateSwap;

        /// <summary>
        /// Swap an active state 'from' to a new state 'to'.
        /// </summary>
        public void SwapStates(State from, State to)
        {
            int ind = IndexOf(from);
            if (ind == -1)
                throw new ArgumentException($"Cannot swap from state of type {from.GetType()} because this state machine does not contain that state.");
            
            if (to == null)
            {
                RemoveState(from);
                return;
            }

            from.OnExit(to, _swapData);
            from.IsActive = false;

            to.SuperState = from.SuperState;
            to.SubState = from.SubState;

            if (from.SuperState != null)
                from.SuperState.SubState = to;
            if (from.SubState != null)
                from.SubState.SuperState = to;

            if (from != to)
            {
                from.SuperState = null;
                from.SubState = null;
            }

            if (from == _root)
                _root = to;

            to.IsActive = true;
            to.Machine = this;
            to.OnEnter(from, _swapData);

            to.SuperState?.OnSubStateSwap(from, to);
            to.SubState?.OnSuperStateSwap(from, to);

            OnStateSwap?.Invoke(from, to, ind);
        }

        /// <summary>
        /// Swap an active state at index 'from' to a new state 'to'.
        /// </summary>
        public void SwapStatesAt(int ind, State to)
        {
            if (ind < 0 || Count <= ind)
                throw new IndexOutOfRangeException($"{ind} is not a valid index in the active state chain.");
            SwapStates(Get(ind), to);
        }

        public event StateIndex OnInsertState;

        /// <summary>
        /// Inserts the given state into the active state chain as the root state.
        /// If the new root is itself a state chain, then the previous root will appended to
        /// the last state in the new chain.
        /// </summary>
        public void SetRoot(State root)
        {
            if (_root != null)
                _root.SuperState = root;
            
            var cur = root;
            while (cur.SubState != null)
                cur = cur.SubState;

            cur.SubState = _root;

            Count++;

            _root = root;
            _root.IsActive = true;
            _root.Machine = this;
            _root.OnEnter(null, _swapData);

            cur.SubState?.OnSuperStateSwap(null, cur);

            OnInsertState?.Invoke(0, root);
        }

        /// <summary>
        /// Inserts the given sub state below the given super state. If the sub state is itself a state chain,
        /// then that state chain will be inserted completely into the active state chain.
        /// </summary>
        public void AddSubState(State super, State sub)
        {
            if (!Contains(super))
                throw new ArgumentException($"Cannot add a sub state to the state of type {super.GetType()} because it is not contained in this state machine.");
            
            var cur = sub;
            while (cur.SubState != null)
            {
                cur.IsActive = true;
                cur.Machine = this;
                cur = cur.SubState;
            }
            
            cur.SubState = super.SubState;
            super.SubState.SuperState = cur;

            super.SubState = sub;
            sub.SuperState = super;

            Count++;

            var end = cur;
            cur = sub;
            while (cur != end.SubState)
            {
                cur.OnEnter(null, _swapData);
                cur = cur.SubState;
            }

            super.OnSubStateSwap(end.SubState, sub);
            end.SubState?.OnSuperStateSwap(super, end);

            OnInsertState?.Invoke(IndexOf(super), sub);
        }

        public void AddSubStateAt(int i, State sub)
        {
            if (i < 0 || Count <= i)
                throw new IndexOutOfRangeException($"Cannot remove state at an invalid index");
            
            AddSubState(Get(i), sub);
        }

        public event StateIndex OnRemoveState;

        /// <summary>
        /// Removes the given state from the active state chain.
        /// </summary>
        public void RemoveState(State state)
        {
            if (!Contains(state))
                throw new ArgumentException($"Cannot remove state on type {state.GetType()} because this state doesn't exist in this state machine.");
            
            state.OnExit(null, _swapData);
            state.IsActive = false;
            
            if (state.SuperState != null)
                state.SuperState.SubState = state.SubState;
            
            if (state.SubState != null)
                state.SubState.SuperState = state.SuperState;
            
            if (state == _root)
                _root = state.SubState;
            
            var super = state.SuperState;
            var sub = state.SubState;

            state.SuperState = null;
            state.SubState = null;

            Count--;

            super?.OnSubStateSwap(state, sub);
            sub?.OnSuperStateSwap(state, super);

            OnRemoveState?.Invoke(IndexOf(state), state);
        }

        /// <summary>
        /// Removes the state at the given index from the active state chain.
        /// </summary>
        public void RemoveAt(int i)
        {
            if (i < 0 || Count <= i)
                throw new IndexOutOfRangeException($"Cannot remove state at an invalid index");
            
            RemoveState(Get(i));
        }

        public override string ToString()
        {
            var str = "State Machine: ";
            if (_root == null)
                return str + "Empty";
            var cur = _root;
            while (cur != null)
            {
                str += cur.ToString();
                if (cur.SubState != null)
                    str += " -> ";
                cur = cur.SubState;
            }

            return str;
        }
    }
}
