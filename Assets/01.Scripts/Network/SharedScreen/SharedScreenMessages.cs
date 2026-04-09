using Mirror;

/// <summary>
/// Mirror 청크 전송용 NetworkMessage 구조체들을 정의하는 파일
/// </summary>
namespace MySecondWorld.Network
{
    public enum FileType
    {
        Unknown,
        PDF,
        Image
    }

    public struct FileStartMessage : NetworkMessage
    {
        public string fileName;
        public FileType fileType;
        public int totalSize;
        public int totalChunkCount;
    }

    public struct FileChunkMessage : NetworkMessage
    {
        public int chunkIndex;
        public byte[] data;
    }

    public struct FileEndMessage : NetworkMessage
    {
        public int totalSize; // 검증용: Start의 totalSize와 비교
    }
}