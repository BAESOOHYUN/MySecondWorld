using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Paroxe.PdfRenderer;
using R3;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 월드 Quad에 붙는 컴포넌트
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class SharedScreenController : NetworkBehaviour
    {
        public readonly Subject<uint> onPresenterChanged = new();
        public readonly Subject<bool> onAllowDownloadChanged = new();
        public readonly Subject<Unit> onFileReceived = new();
        public readonly Subject<int> onPageChanged = new();
        public readonly Subject<float> onZoomChanged = new();
        public readonly Subject<Vector4> onViewportRectChanged = new();

        private Renderer _screenRenderer;

        [SyncVar(hook = nameof(OnPresenterChanged))]
        private uint _presenterId;     // 현재 발표자의 netId. 0이면 발표자 없음
        [SyncVar(hook = nameof(OnAllowDownloadChanged))]
        private bool _allowDownload;             // 다운로드 허용 여부 (발표자가 설정)
        [SyncVar] private FileType _fileType;    // 현재 파일 타입
        [SyncVar] private string _fileName;      // 현재 파일 이름
        [SyncVar] private int _currentPage;      // 현재 페이지 번호
        [SyncVar(hook = nameof(OnZoomLevelChanged))]
        private float _zoomLevel = 1f;           // 줌 배율
        [SyncVar(hook = nameof(OnViewportRectChanged))]
        private Vector4 _viewportRect = new Vector4(0, 0, 1, 1); // (x, y, w, h) 페이지 기준 0~1
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
        public string CurrentFileName => _fileName;
        public float ZoomLevel => _zoomLevel;
        public Vector4 ViewportRect => _viewportRect;
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


        //  [SyncVar] 필드 hook 이벤트  
        private void OnPresenterChanged(uint oldValue, uint newValue)
        {
            onPresenterChanged.OnNext(newValue);
        }

        private void OnAllowDownloadChanged(bool oldValue, bool newValue)
        {
            onAllowDownloadChanged.OnNext(newValue);
        }

        private void OnZoomLevelChanged(float oldValue, float newValue)
        {
            onZoomChanged.OnNext(newValue);
        }

        private void OnViewportRectChanged(Vector4 oldValue, Vector4 newValue)
        {
            onViewportRectChanged.OnNext(newValue);
        }


        //  서버에 발표자(_presenterId) 설정/해제 알림

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
                _zoomLevel = 1f;
                _viewportRect = new Vector4(0, 0, 1, 1);

                SendClearToObservers();
                _isCacheReady = false;
                _cachedFileBuffer = null;
            }
        }

        // 서버에 파일 다운로드 요청

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
        // 외곽 존 진입/퇴장 

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
            // 발표자가 외곽을 벗어나면 자동 해제
            if (sender?.identity != null && _presenterId == sender.identity.netId)
            {
                _presenterId = 0;
                _currentPage = 0;
                _zoomLevel = 1f;
                _viewportRect = new Vector4(0, 0, 1, 1);
                SendClearToObservers();
                _isCacheReady = false;
                _cachedFileBuffer = null;
            }
            _outerObservers.Remove(sender);
        }

        // 서버에 페이지 변경 요청
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


        // 서버에 줌 변경 요청
        [Command(requiresAuthority = false)]
        public void CmdChangeZoom(float newZoom, NetworkConnectionToClient sender = null)
        {
            // 발표자 모드일 때, 발표자만 조작 가능
            if (_presenterId != 0 && sender?.identity?.netId != _presenterId)
                return;

            _zoomLevel = newZoom;
            SendZoomChangeToObservers(newZoom);
        }

        // 서버에 뷰포트 영역 변경 요청
        [Command(requiresAuthority = false)]
        public void CmdChangeViewportRect(Vector4 newRect, NetworkConnectionToClient sender = null)
        {
            if (_presenterId != 0 && sender?.identity?.netId != _presenterId)
                return;

            _viewportRect = newRect;
            SendViewportRectChangeToObservers(newRect);
        }

        // 외곽 존 안의 관찰자들(_outerObservers)에게 파일 수신 시작 알림
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
            if (_receiveBuffer.Length != totalSize)
            {
                Debug.LogError($"[SharedScreen] 버퍼 크기 불일치: {_receiveBuffer.Length} != {totalSize}");
                return;
            }
            if (_receivedChunkCount != _expectedChunkCount)
            {
                Debug.LogError($"[SharedScreen] 청크 손실: 받은 {_receivedChunkCount} / 예상 {_expectedChunkCount}. 파일 전송이 불완전합니다.");
                return;
            }
            _isReceiving = false;

            if (_fileType == FileType.PDF)
                _cachedPdfDocument = new PDFDocument(_receiveBuffer);

            _totalPageCount = totalPageCount;
            RenderToScreen();
            onFileReceived.OnNext(Unit.Default);

        }

        // 모든 클라이언트가 새 페이지 렌더
        [TargetRpc]
        private void TargetRpcPageChanged(NetworkConnection target, int page)
        {
            // _currentPage는 SyncVar 자동 동기화로 받음
            RenderToScreen(page);
            onPageChanged.OnNext(page);
        }

        [TargetRpc]
        private void TargetRpcZoomChanged(NetworkConnection target, float zoom)
        {
            RenderToScreen(_currentPage);
            ApplyQuadZoom();
            onZoomChanged.OnNext(zoom);
        }

        [TargetRpc]
        private void TargetRpcViewportRectChanged(NetworkConnection target, Vector4 rect)
        {
            ApplyQuadZoom();
            onViewportRectChanged.OnNext(rect);
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
            {
                _screenRenderer.material.mainTexture = null;
                _screenRenderer.material.mainTextureScale = Vector2.one;
                _screenRenderer.material.mainTextureOffset = Vector2.zero;
            }

        }

        // 페이지 렌더
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

            Texture2D tex;
            //줌 반영 방식 → 고해상도 품질, 발표자 줌 동기화에 적합하지만 성능 부담 있음
            //int resolution = Mathf.RoundToInt(1024 * Mathf.Clamp(_zoomLevel, 1f, 4f));
            //if (_fileType == FileType.PDF && _cachedPdfDocument != null)
            //    tex = SharedScreenRenderer.Render(_cachedPdfDocument, page, resolution);
            //else
            //    tex = SharedScreenRenderer.Render(_receiveBuffer, _fileType, page);

            // 고정 해상도 방식 → 성능 안정적, 단순히 화면 확대/축소만 하지만 화질 저하가 생김.
            // 전체 페이지를 고정 해상도로 렌더 (줌은 UV로 처리)
            if (_fileType == FileType.PDF && _cachedPdfDocument != null)
                tex = SharedScreenRenderer.Render(_cachedPdfDocument, page, 1024);
            else
                tex = SharedScreenRenderer.Render(_receiveBuffer, _fileType, page);

            if (tex != null)
            {
                if (_screenRenderer.material.mainTexture != null)
                {
                    Destroy(_screenRenderer.material.mainTexture);
                    _screenRenderer.material.mainTexture = null;
                }
                _screenRenderer.enabled = true;
                _screenRenderer.material.mainTexture = tex;

                // 텍스처 비율에 맞게 쿼드 스케일 조정
                Vector2 pageSize;
                if (_fileType == FileType.PDF && _cachedPdfDocument != null)
                    pageSize = SharedScreenRenderer.GetPageSize(_cachedPdfDocument, page);
                else
                    pageSize = SharedScreenRenderer.GetPageSize(_receiveBuffer, _fileType, page);

                if (pageSize.x > 0 && pageSize.y > 0)
                {
                    float aspect = pageSize.x / pageSize.y;
                    Vector3 scale = transform.localScale;
                    scale.x = scale.y * aspect;
                    transform.localScale = scale;
                }

                ApplyQuadZoom();
            }
        }

        /// <summary>
        /// Material의 tiling/offset으로 발표자 Viewport 영역만 Quad에 표시.
        /// _viewportRect = (x, y, w, h) 페이지 기준 0~1, UI 좌표계 (좌상단=0,0).
        /// UV 좌표계는 좌하단=0,0이므로 Y 반전 필요.
        /// </summary>
        private void ApplyQuadZoom()
        {
            if (_screenRenderer == null) return;
            Material mat = _screenRenderer.material;

            // Quad UV는 0~1 범위만 유효 — 페이지 경계 스크롤 시 ny가 범위 밖일 수 있으므로 클램프
            float vx = Mathf.Clamp01(_viewportRect.x);
            float vy = Mathf.Clamp(_viewportRect.y, 0f, Mathf.Max(0f, 1f - _viewportRect.w));
            float vw = Mathf.Clamp01(_viewportRect.z);
            float vh = Mathf.Clamp01(_viewportRect.w);

            // UV Y 반전: UI (0=상단) → UV (0=하단)
            float uvOffsetY = 1f - vy - vh;

            mat.mainTextureScale = new Vector2(vw, vh);
            mat.mainTextureOffset = new Vector2(vx, uvOffsetY);
        }

        // 내부 헬퍼 

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

                // 현재 페이지/줌/스크롤 상태도 전송
                if (_currentPage != 0)
                    TargetRpcPageChanged(conn, _currentPage);
                if (_zoomLevel != 1f)
                    TargetRpcZoomChanged(conn, _zoomLevel);
                if (_viewportRect != new Vector4(0, 0, 1, 1))
                    TargetRpcViewportRectChanged(conn, _viewportRect);
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

        private void SendZoomChangeToObservers(float zoom)
        {
            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                TargetRpcZoomChanged(conn, zoom);
            }
        }

        private void SendViewportRectChangeToObservers(Vector4 rect)
        {
            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                TargetRpcViewportRectChanged(conn, rect);
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