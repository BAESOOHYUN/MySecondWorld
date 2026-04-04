using UnityEngine;
using Mirror;
using System;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 월드 Quad에 붙는 컴포넌트
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class SharedScreenController : NetworkBehaviour
    {
        private Renderer _screenRenderer;

        [SyncVar] private uint _presenterId;     // 현재 발표자의 netId. 0이면 자유 모드
        [SyncVar] private int _currentPage;      // 현재 페이지 번호
        [SyncVar] private float _zoomLevel;      // 줌 배율
        [SyncVar] private FileType _fileType;    // 현재 파일 타입
        [SyncVar] private string _fileName;      // 현재 파일 이름

        // 청크 데이터
        private byte[] _receiveBuffer;           // 조립 중인 전체 데이터
        private int _receivedChunkCount;         // 지금까지 받은 청크 수
        private int _expectedChunkCount;         // 총 몇 개 받아야 하는지
        private bool _isReceiving;               // 수신 중인지 여부


        //TODO 읽기 전용 프로퍼티
        //hook 있으면 — 값이 바뀔 때 자동으로 메서드 호출
        // [SyncVar(hook = nameof(OnPageChanged))]

        /*
            서버를 거치는 3단계 흐름 
            발표자가 호출:  CmdSendFileStart("slides.pdf", FileType.PDF, 524288, 8)
            ↓ 네트워크로 서버에 전달
            서버가 실행:    RpcReceiveFileStart("slides.pdf", FileType.PDF, 524288, 8)
            ↓ 네트워크로 모든 클라이언트에 전달
            모든 클라이언트: RpcReceiveFileStart 본문 실행
        */

        private void Awake()
        {
            _screenRenderer = GetComponent<Renderer>();
            if (_screenRenderer == null)
                Debug.LogError($"Renderer missing on {gameObject.name}", this);
        }

        [Command]
        public void CmdSendFileStart(string fileName, FileType fileType, int totalSize, int totalChunkCount)
        {
            RpcReceiveFileStart(fileName, fileType, totalSize, totalChunkCount);
        }

        [Command] // 반복 호출
        public void CmdSendFileChunk(int chunkIndex, byte[] data)
        {
            RpcReceiveFileChunk(chunkIndex, data);
        }

        [Command]
        public void CmdSendFileEnd(int totalSize)
        {
            RpcReceiveFileEnd(totalSize);
        }


        [ClientRpc]
        private void RpcReceiveFileStart(string fileName, FileType fileType, int totalSize, int totalChunkCount)
        {
            // 버퍼 초기화
            _receiveBuffer = new byte[totalSize];
            _receivedChunkCount = 0;
            _expectedChunkCount = totalChunkCount;
            // _isReceiving = true
            _isReceiving = true;
            // 메타데이터 저장 : TODO VrSync?? 뭐 쓴댔음. 여기가 아니라.
            _fileName = fileName;
            _fileType = fileType; 
        }

        [ClientRpc]
        private void RpcReceiveFileChunk(int chunkIndex, byte[] data) 
        {
            // 핵심: 청크를 버퍼의 "올바른 위치"에 복사
            // offset(시작 위치) 계산
            var offset = chunkIndex * SharedScreenConstants.ChunkSize; 
            // 버퍼 받는 쪽이라 전체를 곱해서 원본 사이즈로 만듬.
            // 보내는 쪽(FileChunkSender)에서는 "파일 전체 크기/ChunkSize"로 나눔

            // 복사 .BlockCopy(원본, 원본시작위치, 대상, 대상시작위치, 복사할길이)
            Buffer.BlockCopy(data, 0, _receiveBuffer, offset, data.Length);
            // 전송 완료 판단
            _receivedChunkCount++;
        }

        [ClientRpc]
        private void RpcReceiveFileEnd(int totalSize)
        {
            // 검증 — 받은 데이터 크기가 totalSize와 맞는지 확인
            if (_receiveBuffer.Length != totalSize)
            {
                Debug.LogError("_receiveBuffer.Length != totalSize");
                return;
            }
            // 수신 상태 종료
            _isReceiving = false; 
            _currentPage = 0;  // 첫 페이지부터
            RenderToScreen();
        }


        /// <summary>
        /// Command — 서버에 페이지 변경 요청
        /// </summary>
        [Command]
        public void CmdChangePage(int newPage)
        {
            if (newPage < 0)
                return;
            // TODO: 최대 페이지 체크 (totalPageCount 필드 필요)

            // SyncVar 업데이트 (서버에서만 가능)
            // 모든 클라이언트에게 리렌더 알림
            _currentPage = newPage;
            RpcPageChanged();
        }


        /// <summary>
        ///  ClientRpc — 모든 클라이언트가 새 페이지 렌더
        /// </summary>
        [ClientRpc]
        private void RpcPageChanged()
        {
            RenderToScreen();
        }

        public void RequestPrevPage()
        {
            CmdChangePage(_currentPage - 1);
        }


        public void RequestNextPage()
        {
            CmdChangePage(_currentPage + 1);
        }

        // 공통: 현재 상태로 Quad에 렌더링
        private void RenderToScreen()
        {
            if (_receiveBuffer == null)
                return;

            // 렌더링 호출 — 완성된 _receiveBuffer를 화면에 표시
            Texture2D tex = SharedScreenRenderer.Render(_receiveBuffer, _fileType, _currentPage);
            // 월드 Quad에 표시
            if (tex != null)
                _screenRenderer.material.mainTexture = tex;
        }
    }
}