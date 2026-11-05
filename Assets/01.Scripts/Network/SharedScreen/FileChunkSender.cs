using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 로컬 파일을 byte[] 로 읽고, SharedScreenConstants.ChunkSize(12,000B)씩 잘라서 SharedScreenController의 Command를 호출
    /// byte[]를 보내는 쪽. <-> 받는 쪽(SharedScreenController.RpcReceiveFileChunk)
    /// </summary>
    public static class FileChunkSender
    {
        // (청크 프로토콜) 보내는 쪽: 로컬 파일을 ChunkSize씩 잘라 CmdSendFileStart → CmdSendFileChunk × N → CmdSendFileEnd 순서로 호출
        public static void SendFileFromLocalstore(SharedScreenController controller, string filePath, FileType fileType, bool allowDownload = false)
        {
            string fileName = Path.GetFileName(filePath);
            byte[] fileByte = File.ReadAllBytes(filePath);
            int chunkSize = SharedScreenConstants.ChunkSize;
            int totalSize = fileByte.Length;
            int totalChunkCount = Mathf.CeilToInt((float)totalSize / chunkSize);
            controller.CmdSendFileStart(fileName, fileType, totalSize, totalChunkCount, allowDownload);
            // 청크 자르기
            for (int i = 0; i < totalChunkCount; i++)
            {
                var offset = i * chunkSize;
                // 이번 청크 크기
                int remaining = totalSize - offset;                   // 아직 안 보낸 양
                int currChunkSize = Mathf.Min(remaining, chunkSize);  // 둘 중 작은 것
                // 잘라서 새 byte[] 만들기
                byte[] currChunk = new byte[currChunkSize];           // 이번 청크만큼 빈 배열
                Buffer.BlockCopy(fileByte, offset, currChunk, 0, currChunkSize);
                // 받는 쪽이랑 방향이 반대!
                // 받는 쪽  : BlockCopy(chunk → _receiveBuffer)  조각 청크를 전체 버퍼에 채움
                // 보내는 쪽: BlockCopy(fileByte → chunk)        전체 버퍼에 조각 청크를 채움
                controller.CmdSendFileChunk(i, currChunk);
            }
            controller.CmdSendFileEnd(totalSize);
        }

        // (청크 프로토콜) 보내는 쪽: URL에서 내려받은 파일을 같은 방식(Start → Chunk × N → End)으로 전송
        public static async UniTask SendFileFromUrlAsync(SharedScreenController controller, string url, FileType fileType, bool allowDownload = false, CancellationToken ct = default)
        {
            try
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    await request.SendWebRequest().WithCancellation(ct);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[FileChunkSender] URL 다운로드 실패: {request.error}");
                        return;
                    }

                    string fileName = Path.GetFileName(url);
                    byte[] fileByte = request.downloadHandler.data;
                    int chunkSize = SharedScreenConstants.ChunkSize;
                    int totalSize = fileByte.Length;
                    int totalChunkCount = Mathf.CeilToInt((float)totalSize / chunkSize);
                    controller.CmdSendFileStart(fileName, fileType, totalSize, totalChunkCount, allowDownload);

                    for (int i = 0; i < totalChunkCount; i++)
                    {
                        var offset = i * chunkSize;
                        int remaining = totalSize - offset;
                        int currChunkSize = Mathf.Min(remaining, chunkSize);
                        byte[] currChunk = new byte[currChunkSize];
                        Buffer.BlockCopy(fileByte, offset, currChunk, 0, currChunkSize);
                        controller.CmdSendFileChunk(i, currChunk);
                    }
                    controller.CmdSendFileEnd(totalSize);
                }
            }
            catch (OperationCanceledException)
            {
                // UI 닫힘 또는 취소 시 정상 종료
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileChunkSender] 파일 전송 중 예외 발생: {ex.Message}");
            }
        }
    }
}