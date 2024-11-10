using OwlTree;

[RpcIdRegistry]
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
}

public class TestEncodable : IEncodable
{
    public int ByteLength()
    {
        throw new NotImplementedException();
    }

    public void FromBytes(ReadOnlySpan<byte> bytes)
    {
        throw new NotImplementedException();
    }

    public bool InsertBytes(Span<byte> bytes)
    {
        throw new NotImplementedException();
    }
}

class Program
{
    static Radio? radio = null;

    static NetworkList<Capacity8, int> list = new NetworkList<Capacity8, int>();

    public class ClassA : NetworkObject
    {
        [Rpc(RpcCaller.Any, InvokeOnCaller = true, RpcProtocol = Protocol.Tcp), AssignRpcId((int)IdRegistry.ExampleRpcIds.C)]
        public virtual void Test() {
            Console.WriteLine("instance");
        }

        public int test;
    }

    public class ClassAProxy : ClassA
    {

        public override void Test() {
            Console.WriteLine("test");
            base.Test();
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