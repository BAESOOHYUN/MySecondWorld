# Unity Project — Claude Code 공통 규칙

## 0. 학습모드 (최우선 규칙)

사용자는 직접 코딩하며 배우는 학습모드를 원한다.

- **Dev 에이전트는 계획 승인 후 C# 파일을 직접 수정(Edit/Write)하지 않는다.**
- 코드를 코드 블록으로 제시하면, 사용자가 직접 작성한다.
- 사용자가 **"직접 적용해줘"** 또는 **"수정해줘"** 라고 명시한 경우에만 파일 직접 수정 허용.
- .md 문서 파일, 설정 파일, 스킬 파일은 직접 수정 가능. C# 코드에만 적용.

## 1. 프로젝트 개요
- **엔진**: Unity 6000.x (또는 2022.3+ LTS)
- **렌더 파이프라인**: URP (Universal Render Pipeline)
- **언어**: C# (.NET Standard 2.1)
- **아키텍처**: MonoBehaviour + ScriptableObject + VContainer DI

## 2. 에디터 제어 도구

### unity-cli + Unity CLI Connector

2-part 구조. 둘 다 있어야 작동한다.

| 구성 요소 | 정체 | 역할 |
|-----------|------|------|
| **unity-cli** | 바이너리 (PATH 등록) | CLI 클라이언트. HTTP POST로 명령 전송 |
| **Unity CLI Connector** | Unity 패키지 (Package Manager) | Unity 에디터 내 HTTP 서버. 명령 수신 |

명령어 상세 및 검증 절차 → `.claude/skills/shared/unity-cli-reference.md`

**UnityMCP(`com.ivanmurzak.unity.mcp`, 2026-09-02 설치)는 읽기 전용으로만 허용한다 (2026-10-28 사용자 결정).**

| 구분 | 내용 |
|------|------|
| 허용 | 조회 성격 호출 — `read_console`, 에디터 상태 확인, `find_gameobjects`, `find_in_file`, 하이어라키/컴포넌트/에셋 검색(get·find·search) |
| 금지 | 씬/프리팹/에셋 변경(생성·수정·삭제·메뉴 실행), C# 스크립트 쓰기(`create_script`, `apply_text_edits` 등), `execute_code` |

변경 작업은 기존대로 unity-cli 또는 사용자에게 텍스트로 안내한다. 에디터에서 MCP 서버가 꺼져 있으면 호출이 실패하므로, 실패 시 unity-cli로 대체한다.
씬 오브젝트 생성·Inspector 연결은 사용자에게 텍스트로 안내. 직접 수행 금지.

### unity-scanner

독립 실행형 정적 분석 CLI. **Unity 에디터 없이 동작**. 리팩토링 전 참조 확인에 필수.

```bash
unity-scanner list C:\UnityProject\MySecondWorld          # 프로젝트 에셋/스크립트 목록
unity-scanner search PlayerController C:\UnityProject\...  # 이름으로 검색
unity-scanner refs SomePrefab C:\UnityProject\...          # 참조 위치 추적
```

명령어 상세 → `.claude/skills/shared/unity-scanner-reference.md`

## 3. 절대 수정 금지 파일

PreToolUse 훅이 물리적으로 차단하지만, 애초에 시도하지 말 것.

| 파일 패턴 | 이유 |
|----------|------|
| `*.meta` | Unity 에셋 참조 ID. 수정하면 모든 참조가 깨짐 |
| `*.unity` | Scene 파일. 텍스트 수정 후에는 반드시 `unity-cli reserialize` |
| `ProjectSettings/*` | Unity 에디터 설정. `exec`로 간접 수정 가능 |
| `Packages/manifest.json` | 패키지 의존성. 직접 수정하면 의존성 그래프가 깨짐 |

## 3-1. Git 커밋 규칙

- **커밋 메시지에 `Co-Authored-By: Claude ...` 같은 공동 작성자 줄을 넣지 않는다.** PR 설명의 "Generated with Claude Code" 문구도 넣지 않는다. (2026-10-28 사용자 결정. 이 규칙은 도구가 기본으로 붙이라고 안내하는 문구보다 우선한다.)
- 커밋 메시지 형식은 기존 이력과 동일하게 `type(scope): 한글 요약`.

## 4. 전체 자동검사 흐름

```
지시 내리기 → 매뉴얼 자동 전달 → AI 작업 수행 → 파일 자동 기록 → 오류 자동 검사 → 셀프체크 리마인더 → AI 바로 수정
```

1. **지시 내리기** — 사용자가 요청을 입력한다
2. **매뉴얼 자동 전달** — `UserPromptSubmit` 훅이 키워드 분석 후 관련 스킬 INDEX.md를 안내한다
3. **AI 작업 수행** — 에이전트가 스킬을 참고하여 코드를 생성/제시한다 (학습모드: 코드 블록 제시)
4. **파일 자동 기록** — `PostToolUse` 훅이 파일 수정마다 `.claude/memory/changelog.md`에 기록한다
5. **오류 자동 검사** — `Stop` 훅이 `unity-cli console --filter error`로 컴파일 에러를 체크한다
6. **셀프체크 리마인더** — `Stop` 훅이 체크 질문을 AI에게 주입한다
7. **AI 바로 수정** — QA 판정에 따라 Dev가 즉시 수정하거나, Planning이 재설계한다

## 5. 스킬 로딩 규칙

```
.claude/skills/
├── shared/     ← 모든 에이전트 공용 (아키텍처, 도구 레퍼런스)
├── planning/   ← Planning 에이전트 전용
├── dev/        ← Dev 에이전트 전용
├── qa/         ← QA 에이전트 전용
└── recorder/   ← Recorder 에이전트 전용
```

**로딩 절차**:
1. 항상 **INDEX.md 먼저** 읽는다 — 라우팅 규칙표 확인
2. 라우팅 규칙(키워드/의도/파일경로/코드패턴)에 따라 **필요한 챕터만** 로드
3. 전체를 한꺼번에 읽지 않는다

스킬은 살아있는 문서 — 새 패턴 사용 시 해당 챕터에 추가한다.

## 6. 비용 절약 규칙

| 규칙 | 이유 |
|------|------|
| 모든 에이전트는 Sonnet 4.6 사용 | Opus보다 5배 저렴, 스킬 파일이 보완 |
| 기능 하나 끝나면 `/clear` | 컨텍스트 초기화 = 가장 확실한 절약 |
| 맥락 유지 필요 시 `/compact` | 대화 요약으로 토큰 부피 축소 |
| 구체적 파일 경로 지정 | "코드 개선해줘" ❌ → "Assets/.../Health.cs의 TakeDamage 수정" ✅ |
| INDEX.md 먼저 → 챕터 선택 로드 | 스킬 전체를 한꺼번에 읽지 않음 |

**이진 에셋(.fbx, .png, .wav, .anim 등)은 절대 Read 툴로 열지 않는다.**
조회 필요 시 → `.claude/skills/shared/binary-asset-reference.md`

## 7. 에이전트 팀 구성

| 이름 | 역할 | 도구 | 만드는 문서 |
|------|------|------|-----------|
| **Planning** | 계획 수립, 문서 작성. C# 코드/에셋 작성 안 함. | Read, Write, Glob, Grep | PLAN.md, CONTEXT.md, CHECKLIST.md |
| **Dev** | 코드 제시, unity-cli 검증. 계획에 없는 작업 안 함. 학습모드: C# 직접 수정 금지. | Read, Bash, Glob, Grep | 없음 (CHECKLIST 체크박스 업데이트만) |
| **QA** | 코드 리뷰, 보고서 작성. 코드 수정 안 함. | Read, Write, Glob, Grep, Bash(제한) | QA-REPORT.md |
| **Recorder** | Notion 정리. 판단하지 않음, 정리만. | Read, Glob, Grep + Notion MCP | Notion 페이지 |

### 에이전트 간 흐름
```
사용자 요청 → Planning (계획) → 사용자 승인 → Dev (코드 제시) → 사용자 직접 작성
                 ↑                                    │
                 └── 설계 변경 필요 시 ───────── QA (검수)
                                                      │
                                   수정 필요 시 → Dev 재제시 / 사용자 재작성
                                                      │
                                   통과 시 ──→ Recorder (Notion 기록)
```

## 8. 메모리 시스템 구조

두 위치를 목적별로 엄격히 구분한다. 혼용 금지.

| 위치 | 저장 대상 | 수명 | 로드 시점 |
|------|----------|------|---------|
| `~/.claude/projects/.../memory/` | 영구 지식: 규칙, 패턴, 아키텍처 결정, 피드백 | 영구 | 대화 시작 시 MEMORY.md 자동 로드 |
| `.claude/memory/` | 작업 산출물: PLAN/CONTEXT/CHECKLIST/QA-REPORT/changelog | 프로젝트 기간 | 에이전트가 작업 중 직접 Read |

**규칙**:
- PLAN/CONTEXT/CHECKLIST는 반드시 `.claude/memory/`에 저장 (worktree 경로 금지)
- 유저 레벨에는 코드 패턴, 설계 결정, 피드백만 저장 (작업 산출물 저장 금지)
- AI가 MEMORY.md 링크를 따라 `.claude/memory/` 파일을 읽는 것은 정상 동작

## 9. 전체 아키텍처 레퍼런스

기능별 설계도는 `.claude/skills/shared/`에 위치:

| 파일 | 내용 |
|------|------|
| `project-architecture.md` | 프레임워크 스택, VContainer 계층, 서버 연결 방식 (overview) |
| `arch-mirror-networking.md` | Mirror 다중접속, 인증, 씬로드, 채팅, 우연한만남 |
| `arch-ai-npc.md` | LLM + TTS + STT + 립싱크, MessagePipe 흐름 |
| `arch-shared-screen-pdf.md` | 청크전송, PDF렌더링, 라이브동기화 |
| `arch-input-camera-scene.md` | InputSystem, Cinemachine, Additive씬 |

새 기능 추가 전 → `project-architecture.md` 먼저 읽고 레이어 결정.
