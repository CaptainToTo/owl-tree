
namespace OwlTree.StateMachine
{
    /// <summary>
    /// Implement to pass data to state update methods.
    /// </summary>
    public interface IStateData {}

    /// <summary>
    /// An empty container struct that can be used for a state machine's data type.
    /// </summary>
    public struct NoData : IStateData {}

    /// <summary>
    /// Represents a possible state in a state machine. Type T is a 
    /// container that can be used to pass 
    /// </summary>
    public abstract class State
    {
        /// <summary>
        /// The state machine this state belongs to.
        /// </summary>
        public StateMachine Machine { get; internal set; }

        /// <summary>
        /// True if this state is currently active in a state machine.
        /// </summary>
        public bool IsActive { get; internal set; } = false;

        /// <summary>
        /// The super state that encapsulates this state. May be null.
        /// </summary>
        public State SuperState { get; internal set; } = null;
        /// <summary>
        /// The sub state that this state encapsulates. May be null.
        /// </summary>
        public State SubState { get; internal set; } = null;

        /// <summary>
        /// True if this state is the root state of a chain.
        /// </summary>
        public bool IsRoot => SuperState == null && IsActive;

        /// <summary>
        /// Invoked when this state's super state gets swapped. Provides the previous and new super states.
        /// Invoked after the swapped states' OnEnter and OnExit have been invoked.
        /// </summary>
        public virtual void OnSuperStateSwap(State prevSuper, State newSuper) { }

        /// <summary>
        /// Invoked when this state's sub state gets swapped. Provides the previous and new sub states.
        /// Invoked after the swapped state's OnEnter and OnExit have been invoked.
        /// </summary>
        public virtual void OnSubStateSwap(State prevSub, State newSub) { }

        /// <summary>
        /// Swap this state
        /// </summary>
        public void SwapTo(State to)
        {
            Machine.SwapStates(this, to);
        }

        /// <summary>
        /// Invoked when this state is entered. This happens when swapping to 
        /// the state, when this state is inserted into the machine, and when the state machine is initialized.
        /// Invoked after the previous state has its OnExit invoked.
        /// If this state was just inserted into the machine (with SetRoot or AddSubState), then from will be null.
        /// </summary>
        public virtual void OnEnter(State from, IStateData data) { }

        /// <summary>
        /// Invoked when this state is exited, or removed. Invoked before the next state's OnEnter is invoked.
        /// If this state was just removed, then to will be null.
        /// </summary>
        public virtual void OnExit(State to, IStateData data) { }

        /// <summary>
        /// Simulation loop callback that can be attached to an engine's logical update loop.
        /// </summary>
        public virtual void LogicUpdate(IStateData data) { }

        /// <summary>
        /// Simulation loop callback that can be attached to an engine's physics loop.
        /// </summary>
        public virtual void PhysicsUpdate(IStateData data) { }

        /// <summary>
        /// Simulation loop callback that can be attached to an engine's render loop.
        /// </summary>
        public virtual void RenderUpdate(IStateData data) { }

        public override string ToString()
        {
            return GetType().ToString();
        }
    }
}
