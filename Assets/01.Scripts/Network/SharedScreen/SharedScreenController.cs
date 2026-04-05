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
        public event Action<uint> onPresenterChanged;
        private Renderer _screenRenderer;

        [SyncVar(hook = nameof(OnPresenterChanged))]
        private uint _presenterId;     // 현재 발표자의 netId. 0이면 자유 모드
        [SyncVar] private int _currentPage;      // 현재 페이지 번호
        [SyncVar] private float _zoomLevel;      // 줌 배율
        [SyncVar] private FileType _fileType;    // 현재 파일 타입
        [SyncVar] private string _fileName;      // 현재 파일 이름

        // 청크 데이터
        private byte[] _receiveBuffer;           // 조립 중인 전체 데이터
        private int _receivedChunkCount;         // 지금까지 받은 청크 수
        private int _expectedChunkCount;         // 총 몇 개 받아야 하는지
        private bool _isReceiving;               // 수신 중인지 여부

        public uint PresenterId => _presenterId;
        public bool HasPresenter => _presenterId != 0;
        public bool IsPresenter(uint netId) => _presenterId == netId;

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

        private void OnPresenterChanged(uint oldValue, uint newValue)
        {
            onPresenterChanged?.Invoke(newValue);
        }


        // ──────────────────서버에 발표자(_presenterId) 설정/해제 알림 ────────────────────────

        [Command(requiresAuthority = false)]
        public void CmdSetPresenter(NetworkConnectionToClient sender = null)
        {
            if (sender?.identity == null) return;
            _presenterId = sender.identity.netId;
        }

        [Command(requiresAuthority = false)]
        public void CmdReleasePresenter(NetworkConnectionToClient sender = null)
        {
            if (sender?.identity == null) return;
            if (_presenterId == sender.identity.netId)
            {
                _presenterId = 0;
                RpcClearScreen();
            }
        }

        // ──────────────────── 서버에 파일 다운로드 요청 ───────────────────────────────

        [Command(requiresAuthority = false)]
        public void CmdSendFileStart(string fileName, FileType fileType, int totalSize, int totalChunkCount, NetworkConnectionToClient sender = null)
        {
            //파일을 열 때 자동으로 발표자가 되고, 발표자가 있으면 다른 사람은 파일 열기/페이지 조작 불가

            // 발표자가 이미 있고, 내가 아니면 거부
            if (_presenterId != 0 && sender?.identity?.netId != _presenterId)
                return;
            // 파일 여는 사람이 자동으로 발표자
            if (sender?.identity != null)
                _presenterId = sender.identity.netId;

            RpcReceiveFileStart(fileName, fileType, totalSize, totalChunkCount);
        }

        [Command(requiresAuthority = false)]
        public void CmdSendFileChunk(int chunkIndex, byte[] data)
        {
            RpcReceiveFileChunk(chunkIndex, data);
        }

        [Command(requiresAuthority = false)]
        public void CmdSendFileEnd(int totalSize)
        {
            RpcReceiveFileEnd(totalSize);
        }

        // ──────────────────── 서버에 페이지 변경 요청 ───────────────────────────────
        [Command(requiresAuthority = false)]
        public void CmdChangePage(int newPage, NetworkConnectionToClient sender = null)
        {
            if (newPage < 0)
                return;
            // TODO: 최대 페이지 체크 (totalPageCount 필드 필요)

            // 발표자 모드일 때, 발표자만 조작 가능
            if (_presenterId != 0 && sender?.identity?.netId != _presenterId)
                return;

            // SyncVar 업데이트 (서버에서만 가능)
            // 모든 클라이언트에게 리렌더 알림
            _currentPage = newPage; // 서버 측 SyncVar 값 갱신
            RpcPageChanged(newPage); // → 현재 접속 중인 클라이언트들에게 즉시 페이지 번호 전달
        }


        // ──────────────────── 모드 클라이언트가 파일 다운로드 ─────────────────────────

        [ClientRpc]
        private void RpcReceiveFileStart(string fileName, FileType fileType, int totalSize, int totalChunkCount)
        {
            // 버퍼 초기화
            _receiveBuffer = new byte[totalSize];
            _receivedChunkCount = 0;
            _expectedChunkCount = totalChunkCount;
            _isReceiving = true;
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

            Buffer.BlockCopy(data, 0, _receiveBuffer, offset, data.Length);
            _receivedChunkCount++;
        }

        [ClientRpc]
        private void RpcReceiveFileEnd(int totalSize)
        {
            // 검증 — 받은 데이터 크기가 totalSize와 맞는지 확인
            if (_receiveBuffer.Length != totalSize)
            {
                Debug.LogError($"_receiveBuffer.Length {_receiveBuffer.Length} != totalSize {totalSize}");
                return;
            }
            // 수신 상태 종료
            _isReceiving = false; 
            _currentPage = 0;  // 첫 페이지부터
            RenderToScreen();
        }

        // ──────────────────── 모든 클라이언트가 새 페이지 렌더 ─────────────────────────
        [ClientRpc]
        private void RpcPageChanged(int page)
        {
            _currentPage = page;
            RenderToScreen();
        }

        [ClientRpc]
        private void RpcClearScreen()
        {
            _receiveBuffer = null;
            _receivedChunkCount = 0;
            _expectedChunkCount = 0;
            _isReceiving = false;
            _currentPage = 0;

            if (_screenRenderer != null)
                _screenRenderer.material.mainTexture = null;
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
            if (tex != null)
            {
                _screenRenderer.enabled = true;  // 텍스처 넣기 전에 렌더러 켜기
                _screenRenderer.material.mainTexture = tex;
            }
        }

    }
}