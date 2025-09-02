using library;
using OwlTree;

var connection = new Connection(new Connection.Args
{
    role = NetRole.Server
});

var myObj = connection.Spawn<Class1>();
var encodable = new TestEncode();
encodable.value = 7;
myObj.MyRpc(encodable);

connection.ExecuteQueue();
connection.ExecuteQueue();
connection.ExecuteQueue();
