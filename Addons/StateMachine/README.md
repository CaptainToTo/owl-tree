# Network State Machine

A hierarchical finite state machine implementation with netcode. Copy these files into your project to use them.

## State Machine

The state machine does not require a connection, and can be used outside of a networked environment.


A `StateMachine` contains a list of active states that can be added to, removed, and swapped between.

Create a new `StateMachine` by specifying the beginning list of states, and some data that will be passed to the states.

```cs
public struct StateData : IStateData
{
    // important program state, user input, object references, etc...
}

var machine = new StateMachine(
    [new GroundedState(), new IdleState()], // starting state, first state will be the root
    someStateData // special state data that will be given to states when they are swapped
);
```

You can then manipulate states through the machine:

```cs
machine.SwapStates(0, new AirborneState()); // swap state at index 0 with a new airborne state

IdleState idle = machine.Get<IdleState>(); // find states based on type

machine.SwapState(idle, new MoveState()); // swap states by reference
```

States can be manipulated by either reference, or by index in the machine.

States have update methods that can run simulation code. Each of these expects some IStateData to be provided. You can run them from the machine using:
```cs
machine.LogicUpdate(someStateData);
machine.PhysicsUpdate(someStateData);
machine.RenderUpdate(someStateData);
```

These three updates only distinguish common update loops in different engines. There is no difference between them other than name, what you choose to put in them, and when you choose to run them.

To create new states, inherit from the `State` class:
```cs
public class PlayerIdle : State
{
    // invoked when this state is being swapped to, or inserted into a machine
    public override void OnEnter(State from, IStateData data) { ... }

    // invoked when this state is being swapped from, or removed from a machine
    public override void OnExit(State to, IStateData data) { ... }

    // invoked when this state's super state is swapped
    public override void OnSuperStateSwap(State prevSuper, State newSuper) { ... }

    // invoked when this state's sub state is swapped
    public override void OnSubStateSwap(State prevSub, newSub) { ... }

    public override void LogicUpdate(IStateData data) { ... }

    public override void PhysicsUpdate(IStateData data) { ... }

    public override void RenderUpdate(IStateData data) { ... }
}
```

## Network State Machine

The `NetworkStateMachine` is a `NetworkObject` that can be spawned by a connection. It will require the state machine it will synchronize, and all possible states to synchronize.
```cs
// states
var grounded = new GroundedState();
var airborne = new AirborneState();
var idle = new IdleState();
var moving = new MoveState();

// init state machine
StateData data = new StateData{...};
var machine = new StateMachine([grounded, idle], data);

// spawn netcode on connection
if (connection.IsAuthority)
{
    var netcode = connection.Spawn<NetworkStateMachine>();
    netcode.Initialize(machine, [grounded, airborne, idle, moving]);
    SendStateMachineNetcode(netcode.Id);
}

// clients wait for netcode to be received
[Rpc(RpcPerms.AuthorityToClients)]
public virtual void SendStateMachineNetcode(NetworkId id)
{
    connection.WaitForObject(id, (obj) => {
        var netcode = (NetworkStateMachine)obj;
        netcode.Initialize(machine, [grounded, airborne, idle, moving]);
    });
}


// state changes will now be synchronized
machine.SwapStates(grounded, airborne);
```

You can control who has authority over the state machine, allowing clients to control specific state in the session. Only the session authority can give authority over a state machine to clients:
```cs
netcode.SetAuthority(otherClient); // fails if called on client
```

## Note

State machines are not given a separate csproj because the OwlTree source generator does not support multiple project processing yet.