using OwlTree;

public class ProjectProtocols : RpcProtocols
{
    public override Type[] GetProtocol(uint rpcId)
    {
        throw new NotImplementedException();
    }

    public override int GetRpcCalleeParam(uint rpcId)
    {
        throw new NotImplementedException();
    }

    public override RpcCaller GetRpcCaller(uint rpcId)
    {
        throw new NotImplementedException();
    }

    public override int GetRpcCallerParam(uint rpcId)
    {
        throw new NotImplementedException();
    }

    public override string GetRpcName(uint rpcId)
    {
        throw new NotImplementedException();
    }

    public override string GetRpcParamName(uint rpcId, int paramInd)
    {
        throw new NotImplementedException();
    }

    public override Protocol GetSendProtocol(uint rpcId)
    {
        throw new NotImplementedException();
    }

    public override bool IsInvokeOnCaller(uint rpcId)
    {
        throw new NotImplementedException();
    }

    protected override void InvokeRpc(uint rpcId, NetworkObject target, object[] args)
    {
        throw new NotImplementedException();
    }
}