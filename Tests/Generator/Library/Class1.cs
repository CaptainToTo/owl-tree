using OwlTree;

namespace library;

public struct TestEncode : IEncodable
{
    public int value;

    public int ByteLength() => 4;

    public void FromBytes(ReadOnlySpan<byte> bytes)
    {
        value = BitConverter.ToInt32(bytes);
    }

    public void InsertBytes(Span<byte> bytes)
    {
        BitConverter.TryWriteBytes(bytes, value);
    }
}

public struct Enc2 : IEncodable
{
    public int ByteLength()
    {
        throw new NotImplementedException();
    }

    public void FromBytes(ReadOnlySpan<byte> bytes)
    {
        throw new NotImplementedException();
    }

    public void InsertBytes(Span<byte> bytes)
    {
        throw new NotImplementedException();
    }
}

public class MyNetObj : NetworkObject
{
    [Rpc(RpcPerms.AnyToAll, InvokeOnCaller = true)]
    public virtual void MyRpc(TestEncode e, [CallerId] ClientId caller = default)
    {
        Connection.Log($"value sent from {Id}: {e.value}, from player {caller}");
    }

    [Rpc]
    public virtual void MyRpc2(int i)
    {

    }

    [Rpc]
    public virtual void MyTestRpc(int i) { }
}

public class MySubClass : MyNetObj
{
    [Rpc]
    public virtual void SubRpc1() {}
}

public class Class1 : MySubClass
{
}

public class Class2 : NetworkObject { }

public class Class3 : MyNetObj { }