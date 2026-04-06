using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;
using System.Collections;
using Paroxe.PdfRenderer;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 월드 Quad에 붙는 컴포넌트
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class SharedScreenController : NetworkBehaviour
    {
        public event Action<uint> onPresenterChanged;
        public event Action<bool> onAllowDownloadChanged;
        public event Action onFileReceived;
        public event Action<int> onPageChanged;

        private Renderer _screenRenderer;

        [SyncVar(hook = nameof(OnPresenterChanged))]
        private uint _presenterId;     // 현재 발표자의 netId. 0이면 발표자 없음
        [SyncVar(hook = nameof(OnAllowDownloadChanged))]
        private bool _allowDownload;             // 다운로드 허용 여부 (발표자가 설정)
        [SyncVar] private FileType _fileType;    // 현재 파일 타입
        [SyncVar] private string _fileName;      // 현재 파일 이름
        [SyncVar] private int _currentPage;      // 현재 페이지 번호
        [SyncVar] private float _zoomLevel;      // 줌 배율
        private int _totalPageCount;             // 클라이언트용 (UI에서 참조)


        // 청크 데이터
        private byte[] _receiveBuffer;           // 조립 중인 전체 데이터
        private int _receivedChunkCount;         // 지금까지 받은 청크 수
        private int _expectedChunkCount;         // 총 몇 개 받아야 하는지
        private bool _isReceiving;               // 수신 중인지 여부
        private PDFDocument _cachedPdfDocument;

        // 서버 전용 캐시
        private string _cachedFileName;
        private byte[] _cachedFileBuffer; // 완성된 파일 byte[]
        private FileType _cachedFileType;
        private int _cachedTotalSize;
        private int _cachedTotalChunkCount;
        private int _cachedTotalPageCount;  // 서버용 (Cmd에서 범위 체크, 캐시 전송, TargetRpc로 전달할 때 필요)
        private bool _isCacheReady; // 캐시 완성 여부(전송 중에 진입한 사람에게 미완성 데이터 안 보내려고)

        // 관찰자 목록 : 이 SharedScreen 오브젝트만의 로컬 상태
        private HashSet<NetworkConnectionToClient> _outerObservers = new HashSet<NetworkConnectionToClient>();

        // 프로퍼티
        public uint PresenterId => _presenterId;
        public bool HasPresenter => _presenterId != 0;
        public bool IsPresenter(uint netId) => _presenterId == netId;
        public int CurrentPage => _currentPage;
        public int TotalPageCount => _totalPageCount;
        public bool AllowDownload => _allowDownload;
        public FileType CurrentFileType => _fileType;
        public byte[] FileBuffer => _receiveBuffer;
        public bool IsReceiving => _isReceiving;
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

        private void OnDestroy()
        {
            _cachedPdfDocument?.Dispose();
            _cachedPdfDocument = null;
        }

        public override void OnStartServer()
        {
            NetworkServer.OnDisconnectedEvent += OnObserverDisconnected;
        }

        public override void OnStopServer()
        {
            NetworkServer.OnDisconnectedEvent -= OnObserverDisconnected;
        }

        public void OnObserverDisconnected(NetworkConnectionToClient conn)
        {
            _outerObservers.Remove(conn);
        }


        // ────────────────── [SyncVar] 필드 hook 이벤트 ────────────────── 
        private void OnPresenterChanged(uint oldValue, uint newValue)
        {
            onPresenterChanged?.Invoke(newValue);
        }

        private void OnAllowDownloadChanged(bool oldValue, bool newValue)
        {
            onAllowDownloadChanged?.Invoke(newValue);
        }


        // ────────────────── 서버에 발표자(_presenterId) 설정/해제 알림 ────────────────────────

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
                _presenterId = 0;    // 서버에서 SyncVar 세팅
                _currentPage = 0;    // 늦게 온 사람도 "발표자 없음, 0페이지" 상태를 자동으로 받아야 함

                SendClearToObservers();
                _isCacheReady = false;
                _cachedFileBuffer = null;
            }
        }

        // ──────────────────── 서버에 파일 다운로드 요청 ───────────────────────────────

        [Command(requiresAuthority = false)]
        public void CmdSendFileStart(string fileName, FileType fileType, int totalSize, int totalChunkCount, bool allowDownload, NetworkConnectionToClient sender = null)
        {
            //파일을 열 때 자동으로 발표자가 되고, 발표자가 있으면 다른 사람은 파일 열기/페이지 조작 불가

            // 발표자가 이미 있고, 내가 아니면 거부
            if (_presenterId != 0 && sender?.identity?.netId != _presenterId)
                return;
            // 파일 여는 사람이 자동으로 발표자
            if (sender?.identity != null)
                _presenterId = sender.identity.netId;

            _allowDownload = allowDownload;
            _fileName = fileName;        // 서버에서 SyncVar 세팅
            _fileType = fileType;        // 늦게 접속한 사람이 자동으로 현재 값을 받음

            _cachedFileName = fileName;
            _cachedFileBuffer = new byte[totalSize];
            _cachedFileType = fileType;
            _cachedTotalSize = totalSize;
            _cachedTotalChunkCount = totalChunkCount;
            _isCacheReady = false;

            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                TargetRpcReceiveFileStart(conn, _cachedFileName, _cachedFileType, _cachedTotalSize, _cachedTotalChunkCount);
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdSendFileChunk(int chunkIndex, byte[] data)
        {
            var offset = chunkIndex * SharedScreenConstants.ChunkSize;

            if (_cachedFileBuffer == null) 
                return;
            
            Buffer.BlockCopy(data, 0, _cachedFileBuffer, offset, data.Length);

            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                TargetRpcReceiveFileChunk(conn, chunkIndex, data);
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdSendFileEnd(int totalSize)
        {
            _currentPage = 0;  // 서버에서 SyncVar 세팅

            _isCacheReady = true;
            _cachedTotalPageCount = SharedScreenRenderer.GetPageCount(_cachedFileBuffer, _cachedFileType);
            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                TargetRpcReceiveFileEnd(conn, totalSize, _cachedTotalPageCount);
            }
        }
        // ──────────────────── 외곽 존 진입/퇴장 ────────────────────────────────────

        [Command(requiresAuthority = false)]
        public void CmdEnterOuterZone(NetworkConnectionToClient sender = null)
        {
            _outerObservers.Add(sender);

            if (_isCacheReady)
                SendCachedFileToTarget(sender);
        }

        [Command(requiresAuthority = false)]
        public void CmdExitOuterZone(NetworkConnectionToClient sender = null)
        {
            _outerObservers.Remove(sender);

            // 발표자가 외곽을 벗어나면 자동 해제
            if (sender?.identity != null && _presenterId == sender.identity.netId)
            {
                _presenterId = 0;
                _currentPage = 0;
                SendClearToObservers();
                _isCacheReady = false;
                _cachedFileBuffer = null;
            }
        }
         
        // ──────────────────── 서버에 페이지 변경 요청 ───────────────────────────────
        [Command(requiresAuthority = false)]
        public void CmdChangePage(int newPage, NetworkConnectionToClient sender = null)
        {
            // 페이지 오류일때 무시
            if (newPage < 0 || (_cachedTotalPageCount > 0 && newPage >= _cachedTotalPageCount))
                return;

            // 발표자 모드일 때, 발표자만 조작 가능
            if (_presenterId != 0 && sender?.identity?.netId != _presenterId)
                return;

            // SyncVar 업데이트 (서버에서만 가능)
            // 외곽 존 관찰자들에게 리렌더 알림
            _currentPage = newPage; // 서버 측 SyncVar 값 갱신
            SendPageChangeToObservers(newPage); // 외곽 존 관찰자들에게 페이지 번호 전달
        }


        // ──────────────────── 모드 클라이언트가 파일 다운로드 ─────────────────────────

        [TargetRpc]
        private void TargetRpcReceiveFileStart(NetworkConnection target, string fileName, FileType fileType, int totalSize, int totalChunkCount)
        {
            _cachedPdfDocument?.Dispose();
            _cachedPdfDocument = null;

            // 버퍼 초기화
            _receiveBuffer = new byte[totalSize];
            _receivedChunkCount = 0;
            _expectedChunkCount = totalChunkCount;
            _isReceiving = true;
            _fileName = fileName;      // Rpc 파라미터로 즉시 세팅
            _fileType = fileType;      // Rpc 파라미터로 즉시 세팅
        }

        [TargetRpc]
        private void TargetRpcReceiveFileChunk(NetworkConnection target, int chunkIndex, byte[] data)
        {
            // 핵심: 청크를 버퍼의 "올바른 위치"에 복사
            // offset(시작 위치) 계산
            var offset = chunkIndex * SharedScreenConstants.ChunkSize;
            // 버퍼 받는 쪽이라 전체를 곱해서 원본 사이즈로 만듬.
            // 보내는 쪽(FileChunkSender)에서는 "파일 전체 크기/ChunkSize"로 나눔

            Buffer.BlockCopy(data, 0, _receiveBuffer, offset, data.Length);
            _receivedChunkCount++;
        }

        [TargetRpc]
        private void TargetRpcReceiveFileEnd(NetworkConnection target, int totalSize, int totalPageCount)
        {
            // 검증 — 받은 데이터 크기가 totalSize와 맞는지 확인
            if (_receiveBuffer.Length != totalSize)
            {
                Debug.LogError($"_receiveBuffer.Length {_receiveBuffer.Length} != totalSize {totalSize}");
                return;
            }
            // 수신 상태 종료
            _isReceiving = false;

            if (_fileType == FileType.PDF)
                _cachedPdfDocument = new PDFDocument(_receiveBuffer);

            _totalPageCount = totalPageCount;
            RenderToScreen();
            onFileReceived?.Invoke();

        }

        // ──────────────────── 모든 클라이언트가 새 페이지 렌더 ─────────────────────────
        [TargetRpc]
        private void TargetRpcPageChanged(NetworkConnection target, int page)
        {
            // _currentPage는 SyncVar 자동 동기화로 받음
            RenderToScreen(page);
            onPageChanged?.Invoke(page);
        }

        [TargetRpc]
        private void TargetRpcClearScreen(NetworkConnection target)
        {
            // _receiveBuffer는 유지 (관객 로컬 열람용)
            _receivedChunkCount = 0;
            _expectedChunkCount = 0;
            _isReceiving = false;
            _cachedPdfDocument?.Dispose();
            _cachedPdfDocument = null;

            if (_screenRenderer != null)
                _screenRenderer.material.mainTexture = null;

        }

        // ──────────────────── 페이지 렌더 ─────────────────────────
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
            RenderToScreen(_currentPage);
        }

        private void RenderToScreen(int page)
        {
            if (_receiveBuffer == null)
                return;

            // 렌더링 호출 — 완성된 _receiveBuffer를 화면에 표시
            Texture2D tex;
            if (_fileType == FileType.PDF && _cachedPdfDocument != null)
                tex = SharedScreenRenderer.Render(_cachedPdfDocument, page);
            else
                tex = SharedScreenRenderer.Render(_receiveBuffer, _fileType, page);

            if (tex != null)
            {
                // 이전 텍스처 해제
                if (_screenRenderer.material.mainTexture != null)
                {
                    Destroy(_screenRenderer.material.mainTexture);
                    _screenRenderer.material.mainTexture = null;
                }
                _screenRenderer.enabled = true;  // 텍스처 넣기 전에 렌더러 켜기
                _screenRenderer.material.mainTexture = tex;
            }
        }

        // ──────────────────── 내부 헬퍼 ──────────────────── 

        // 특정 1명에게 캐시 전송. 늦게 온 사람이 외곽 트리거 진입하면 호출
        private void SendCachedFileToTarget(NetworkConnectionToClient conn)
        {
            if (_isCacheReady)
            {
                int chunkSize = SharedScreenConstants.ChunkSize;
                TargetRpcReceiveFileStart(conn, _cachedFileName, _cachedFileType, _cachedTotalSize, _cachedTotalChunkCount);

                for (int i = 0; i < _cachedTotalChunkCount; i++)
                {
                    var offset = i * chunkSize;
                    int remaining = _cachedTotalSize - offset;
                    int currChunkSize = Mathf.Min(remaining, chunkSize);
                    byte[] currChunk = new byte[currChunkSize];
                    Buffer.BlockCopy(_cachedFileBuffer, offset, currChunk, 0, currChunkSize);
                    TargetRpcReceiveFileChunk(conn, i, currChunk);
                }
                TargetRpcReceiveFileEnd(conn, _cachedTotalSize, _cachedTotalPageCount);
            }
        }

        // _outerObservers 순회하며 TargetRpcPageChanged 전송
        private void SendPageChangeToObservers(int page)
        {
            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                TargetRpcPageChanged(conn, page);
            }
        }

        private void SendClearToObservers()
        {
            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                TargetRpcClearScreen(conn);
            }
        }
    }
}