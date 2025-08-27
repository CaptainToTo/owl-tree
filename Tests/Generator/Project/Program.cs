using library;
using OwlTree;

var connection = new Connection(new Connection.Args
{
    role = NetRole.Server
});

var myObj = connection.Spawn<MyNetObj>();
var encodable = new TestEncode();
encodable.value = 7;
myObj.MyRpc(encodable);

connection.ExecuteQueue();
connection.ExecuteQueue();
connection.ExecuteQueue();
