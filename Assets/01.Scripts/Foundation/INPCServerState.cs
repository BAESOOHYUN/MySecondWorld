namespace MySecondWorld.Network
{
    public interface INPCServerState
    {
        float FactionRelations { get; }
        string GetContextLine();
    }
}