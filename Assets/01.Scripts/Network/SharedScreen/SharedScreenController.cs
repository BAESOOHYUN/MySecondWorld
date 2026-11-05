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
    ///
    /// [청크 프로토콜] 파일을 ChunkSize(12,000B)씩 잘라 Start → Chunk × N → End 순서로 보낸다.
    /// 이 흐름에서 고친 두 문제 (master TODO의 C1·C2, known-issues 문서의 1·2번):
    ///
    /// C1 전송 중 입장자 (정상 사용자의 타이밍 버그)
    ///   문제: 전송 도중 바깥 존에 들어온 사람은 Start를 못 받았는데 Chunk만 받아, 수신 버퍼가 없어 예외가 났다.
    ///   해결: Start를 받은 사람(_transferRecipients)에게만 Chunk/End를 중계하고,
    ///         도중 입장자는 End 때 서버 캐시를 처음부터(SendCachedFileToTarget) 받는다.
    ///
    /// C2 서버 검증 (비정상·악의적 입력에 대한 방어)
    ///   문제: 서버가 클라이언트가 보낸 값을 믿고 있었다.
    ///     ① 발표자가 아닌 사람도 Chunk/End를 호출할 수 있었다 (발표 중인 버퍼를 덮어쓸 수 있음)
    ///     ② 범위 밖 chunkIndex / 버퍼를 넘는 크기는 서버 Buffer.BlockCopy 예외를 일으켰다
    ///     ③ 거대한 totalSize는 new byte[totalSize]로 서버 메모리를 소모시켰다
    ///     ④ End에서 클라가 보낸 totalSize를 그대로 모든 관객에게 중계했다
    ///   해결: 발표자 검사(sender), chunkIndex·크기 범위 검사, MaxFileSize 상한 + 청크 수 정합성 검사,
    ///         End는 서버가 Start에서 확정한 _cachedTotalSize를 쓴다.
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

        // 서버에서 발표자를 추적하고, 발표자가 로그아웃/접속 종료 시
        // _presenterId를 0으로 리셋하고 SendClearToObservers()를 호출하여 화면을 끔
        private NetworkConnectionToClient _presenterConn;
        // 관찰자 목록 : 이 SharedScreen 오브젝트만의 로컬 상태
        private HashSet<NetworkConnectionToClient> _outerObservers = new HashSet<NetworkConnectionToClient>();
        // [청크 프로토콜 · C1 전송 중 입장자] 이번 전송의 Start를 실제로 받은 사람들.
        //   문제: 전송 도중 외곽 존에 들어온 사람은 Start를 못 받았는데 뒤이은 Chunk만 받아, 버퍼가 없어 예외가 났다.
        //   해결: Chunk/End는 이 목록에만 중계하고, 도중 입장자는 End 때 서버 캐시를 처음부터(Start → Chunk → End) 받는다.
        private readonly HashSet<NetworkConnectionToClient> _transferRecipients = new();

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
            if (conn != null && (_presenterConn == conn || (conn.identity != null && _presenterId == conn.identity.netId)))
            {
                _presenterId = 0;
                _presenterConn = null;
                _currentPage = 0;
                _zoomLevel = 1f;
                _viewportRect = new Vector4(0, 0, 1, 1);
                SendClearToObservers();
                _isCacheReady = false;
                _transferRecipients.Clear();
                _cachedFileBuffer = null;
            }

            _outerObservers.Remove(conn);
        }


        // [SyncVar] 필드 hook 이벤트  
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
            _presenterConn = sender;
        }

        [Command(requiresAuthority = false)]
        public void CmdReleasePresenter(NetworkConnectionToClient sender = null)
        {
            if (sender?.identity == null) return;
            if (_presenterId == sender.identity.netId || _presenterConn == sender)
            {
                _presenterId = 0;    // 서버에서 SyncVar 세팅
                _presenterConn = null;
                _currentPage = 0;    // 늦게 온 사람도 "발표자 없음, 0페이지" 상태를 자동으로 받아야 함
                _zoomLevel = 1f;
                _viewportRect = new Vector4(0, 0, 1, 1);

                SendClearToObservers();
                _isCacheReady = false;
                _transferRecipients.Clear();
                _cachedFileBuffer = null;
            }
        }

        // [청크 프로토콜 1/3 Start] 발표자가 파일 정보를 알림 → 서버가 버퍼를 확보하고 이번 전송의 수신 대상을 확정
        //   [C1 전송 중 입장자] 이 시점의 _outerObservers만 _transferRecipients에 고정한다 (이후 입장자와 구분하려고)
        //   [C2 서버 검증] 발표자 검사 + 크기 상한(MaxFileSize) + 총 청크 수 정합성 — new byte[totalSize]로 메모리를 잡기 전에 검증
        [Command(requiresAuthority = false)]
        public void CmdSendFileStart(string fileName, FileType fileType, int totalSize, int totalChunkCount, bool allowDownload, NetworkConnectionToClient sender = null)
        {
            //파일을 열 때 자동으로 발표자가 되고, 발표자가 있으면 다른 사람은 파일 열기/페이지 조작 불가

            // 발표자가 이미 있고, 내가 아니면 거부
            if (_presenterId != 0 && sender?.identity?.netId != _presenterId)
                return;

            // [C2 서버 검증] 상한·정합성 검증 — 발표자 등록, 상태 변경, new byte[] 보다 먼저 해서 거짓 값이 서버 상태를 건드리지 못하게 한다
            //   totalSize: 0 이하이거나 MaxFileSize 초과면 거부 (거대한 값으로 서버 메모리를 소모시키는 것 방지)
            if (totalSize <= 0 || totalSize > SharedScreenConstants.MaxFileSize) return;
            //   totalChunkCount가 ceil(totalSize / ChunkSize)와 일치하는지 검사 (청크가 다 도착했는지가 아니라, 신고한 청크 수가 크기와 맞는지)
            if (totalChunkCount != (totalSize + SharedScreenConstants.ChunkSize - 1) / SharedScreenConstants.ChunkSize) return;

            // 내부 발표자 등록 : 파일 여는 사람이 자동으로 발표자
            if (sender?.identity != null)
            {
                _presenterId = sender.identity.netId;
                _presenterConn = sender;
            }

            _allowDownload = allowDownload;
            _fileName = fileName;        // 서버에서 SyncVar 세팅
            _fileType = fileType;        // 늦게 접속한 사람이 자동으로 현재 값을 받음

            _cachedFileName = fileName;
            _cachedFileBuffer = new byte[totalSize];
            _cachedFileType = fileType;
            _cachedTotalSize = totalSize;
            _cachedTotalChunkCount = totalChunkCount;
            _isCacheReady = false;
            _transferRecipients.Clear();

            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                _transferRecipients.Add(conn);
                TargetRpcReceiveFileStart(conn, _cachedFileName, _cachedFileType, _cachedTotalSize, _cachedTotalChunkCount);
            }
        }

        // [청크 프로토콜 2/3 Chunk] 발표자가 조각 1개를 보냄 → 서버가 버퍼에 이어 붙이고 수신 대상에게 중계
        //   [C2 서버 검증] 서버는 클라가 보낸 값을 믿지 않는다:
        //     ① 발표자 검사 — sender가 현재 발표자가 아니면 거부 (아무나 Chunk를 보내 발표 중인 버퍼를 덮어쓰는 것 방지)
        //     ② 범위 검사 — chunkIndex가 [0, 총 청크 수) 안인지, offset + data.Length가 버퍼를 넘지 않는지
        //        (잘못된 값이면 Buffer.BlockCopy가 서버에서 예외를 던진다)
        [Command(requiresAuthority = false)]
        public void CmdSendFileChunk(int chunkIndex, byte[] data, NetworkConnectionToClient sender = null)
        {
            if (sender?.identity == null || sender.identity.netId != _presenterId) return;
            if (_cachedFileBuffer == null || data == null) return;
            if (chunkIndex < 0 || chunkIndex >= _cachedTotalChunkCount) return;

            // offset = 이 청크가 원본 파일 전체(byte[] 버퍼)에서 시작하는 위치 (바이트 단위, 0부터)
            //   chunkIndex 0 → 0, 1 → 12000, 2 → 24000 ... (ChunkSize = 12000B)
            var offset = chunkIndex * SharedScreenConstants.ChunkSize;
            // 이 조각을 offset에 꽂았을 때 버퍼 끝을 넘으면 거부 ([C2 서버 검증] 범위 검사 — 마지막 청크는 ChunkSize보다 짧을 수 있다)
            if (offset + data.Length > _cachedFileBuffer.Length) return;
            
            Buffer.BlockCopy(data, 0, _cachedFileBuffer, offset, data.Length);

            foreach (var conn in new List<NetworkConnectionToClient>(_transferRecipients))
            {
                if (_outerObservers.Contains(conn)) // 전송 중 존을 나간 사람 제외
                    TargetRpcReceiveFileChunk(conn, chunkIndex, data);
            }
        }

        // [청크 프로토콜 3/3 End] 전송 끝 → 페이지 수 계산, 캐시 확정(_isCacheReady), 수신자에게 End 전달
        //   [C2 서버 검증] 발표자 검사 + 버퍼가 null이면(= Start 없이 End만 호출) 거부
        //   [C2 서버 검증] 수신자에게 보내는 총 크기는 클라가 보낸 totalSize 인자가 아니라 서버가 Start에서 확정한 _cachedTotalSize여야 한다.
        //       클라 값을 그대로 중계하면 거짓 크기가 모든 관객에게 퍼진다 (관객은 이 값으로 버퍼 크기 일치를 검증하기 때문).
        //       totalSize 인자는 FileChunkSender 호환용으로만 남겨 둔다.
        //   [C1 전송 중 입장자] 전송 도중 입장한 사람은 Start를 못 받았으므로 End 대신 SendCachedFileToTarget으로 처음부터 받는다.
        [Command(requiresAuthority = false)]
        public void CmdSendFileEnd(int totalSize, NetworkConnectionToClient sender = null)
        {
            if (sender?.identity == null || sender.identity.netId != _presenterId) return;
            if (_cachedFileBuffer == null) return;

            _currentPage = 0;  // 서버에서 SyncVar 세팅
            _isCacheReady = true;
            _cachedTotalPageCount = SharedScreenRenderer.GetPageCount(_cachedFileBuffer, _cachedFileType);
            
            foreach (var conn in new List<NetworkConnectionToClient>(_outerObservers))
            {
                if (_transferRecipients.Contains(conn))
                    TargetRpcReceiveFileEnd(conn, _cachedTotalSize, _cachedTotalPageCount);
                else
                    SendCachedFileToTarget(conn); // 전송 도중 입장한 사람은 여기서 처음부터 받는다
            }
            _transferRecipients.Clear();
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
                _transferRecipients.Clear();
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

        // [청크 프로토콜 수신 1/3 Start] 서버 → 수신 대상: 받을 파일의 크기만큼 수신 버퍼를 확보하고 수신 상태를 초기화
        // (외곽 존 안의 관찰자들(_outerObservers)에게 파일 수신 시작 알림)
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

        // [청크 프로토콜 수신 2/3 Chunk] 서버 → 수신 대상: 받은 조각을 chunkIndex × ChunkSize 위치에 복사해 원본을 조립
        [TargetRpc]
        private void TargetRpcReceiveFileChunk(NetworkConnection target, int chunkIndex, byte[] data)
        {
            // 핵심: 청크를 버퍼의 "올바른 위치"에 복사
            // offset = 이 청크가 원본 파일 전체(수신 버퍼)에서 시작하는 위치 (바이트 단위, 0부터)
            //   chunkIndex 0 → 0, 1 → 12000, 2 → 24000 ... 청크가 순서대로 오지 않아도 이 위치에 꽂으면 원본이 복원된다.
            //   아래 Buffer.BlockCopy(data, 0, 버퍼, offset, data.Length)는
            //   "조각 data의 0번째부터 data.Length바이트를 수신 버퍼의 offset 위치에 복사"라는 뜻이다.
            var offset = chunkIndex * SharedScreenConstants.ChunkSize;
            // 버퍼 받는 쪽이라 전체를 곱해서 원본 사이즈로 만듬.
            // 보내는 쪽(FileChunkSender)에서는 "파일 전체 크기/ChunkSize"로 나눔

            Buffer.BlockCopy(data, 0, _receiveBuffer, offset, data.Length);
            _receivedChunkCount++;
        }

        // [청크 프로토콜 수신 3/3 End] 서버 → 수신 대상: 버퍼 크기·받은 청크 수를 검증하고 문서(PDF 등)를 열 준비를 마친다
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

        // (청크 프로토콜) 특정 1명에게 서버 캐시를 Start → Chunk×N → End 순서로 재전송.
        // 늦게 온 사람이 외곽 트리거 진입하면 호출 ([C1 전송 중 입장자] 전송 도중 입장자는 End 때 여기로 보낸다)
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