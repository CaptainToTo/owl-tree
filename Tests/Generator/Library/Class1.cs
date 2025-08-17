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

public class MyNetObj : NetworkObject
{
    [Rpc(RpcPerms.AnyToAll, InvokeOnCaller = true)]
    public virtual void MyRpc(TestEncode e)
    {
        Connection.Log($"value sent from {Id}: {e.value}");
    }
}
