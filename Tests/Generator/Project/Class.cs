using library;
using OwlTree;

public class MyClass : NetworkObject
{
    [Rpc]
    public virtual void MyRpc(TestEncode e)
    {
        Connection.Log(e.value.ToString());
    }
}