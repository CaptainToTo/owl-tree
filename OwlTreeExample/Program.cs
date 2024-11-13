using OwlTree;

[IdRegistry]
static class IdRegistry
{
    public const int FirstRpcId = 30;

    public enum ExampleRpcIds
    {
        Test = RpcId.FIRST_RPC_ID,
        B,
        Test2 = FirstRpcId,
        C,
        D,
        E,
        F
    }

    public enum TypeIds
    {
        A = NetworkObject.FIRST_TYPE_ID,
        B,
        C
    }
}

class Program
{
    static Radio? radio = null;

    static NetworkList<Capacity8, int> list = new NetworkList<Capacity8, int>();

    public class ClassA : NetworkObject
    {
        [Rpc(RpcCaller.Any, InvokeOnCaller = true, RpcProtocol = Protocol.Tcp), AssignRpcId((int)IdRegistry.ExampleRpcIds.B)]
        public virtual void Test(float i, [RpcCaller] ClientId caller = default) {
            Console.WriteLine("instance");
        }
    }

    public class ClassAProxy : ClassA
    {

        public override void Test(float i, ClientId caller = default) {
            if (!IsActive)
                throw new InvalidOperationException("Attempted to call RPC " + Connection.Protocols.GetRpcName(1) + "on an inactive NetworkObject with id:" + Id.ToString());
            
            if (Connection == null)
                throw new InvalidOperationException("RPCs can only be called on an active connection.");
            
            if (
                (Connection.Protocols.GetRpcCaller(1) == (RpcCaller)Connection.NetRole) ||
                (Connection.Protocols.GetRpcCaller(1) == RpcCaller.Any && !i_IsReceivingRpc)
            )
            {
                object[] args = new object[]{i, Connection.LocalId};
                i_OnRpcCall.Invoke(
                    (ClientId)args[Connection.Protocols.GetRpcCalleeParam(1)],
                    new RpcId(1),
                    Id,
                    Connection.Protocols.GetSendProtocol(1),
                    args
                );

                if (Connection.Protocols.IsInvokeOnCaller(1))
                    base.Test(i, Connection.LocalId);
            }
            else if (i_IsReceivingRpc)
            {
                base.Test(i, caller);
            }
            else
            {
                throw new InvalidOperationException($"This connection does not have the permission to call the RPC {Connection.Protocols.GetRpcName(1)} on NetworkObject {Id}.");
            }
        }
    }

    static void Main(string[] args)
    {
        radio = new Radio();

        radio.RPC_Test();
        return;

        list.Add(2);
        list.Add(10);
        list.Add(8);
        list.Add(500);
        if (args[0] == "s")
        {
            var server = new Connection(new Connection.Args
            {
                role = Connection.Role.Server,
                verbosity = Logger.LogRule.Verbose
            });
            server.OnClientConnected += (ClientId id) => {
                if (radio == null)
                    radio = server.Spawn<Radio>();
            };
            Loop(server);
        }
        else if (args[0] == "c")
        {
            var client = new Connection(new Connection.Args
            {
                role = Connection.Role.Client,
                verbosity = Logger.LogRule.Verbose
            });
            client.OnObjectSpawn += (obj) => {
                radio = (Radio)obj;
                // radio.RPC_PingServer("Hello from client: 0");
            };
            Loop(client);
        }
    }

    public static void Loop(Connection connection)
    {
        int tick = 0;
        while (true)
        {
            Console.WriteLine("Tick: " + tick);
            tick++;
            connection.ExecuteQueue();
            if (connection.NetRole == Connection.Role.Server && radio != null)
            {
                radio.RPC_ListTest(list);
            }
            else if (connection.NetRole == Connection.Role.Client && radio != null)
            {
                radio.RPC_SendPosition(0.25f, 0.25f, 0.25f);
            }
            Thread.Sleep(1000);
        }
    }
}