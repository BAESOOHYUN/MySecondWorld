namespace MySecondWorld.Save
{
    // 세이브 백엔드 인터페이스
    public interface ISaveBackend
    {
        void Save(string playerId, SaveData data);
        SaveData? Load(string playerId);
        void Delete(string playerId);
    }
}