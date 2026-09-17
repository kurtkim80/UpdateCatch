# 📡 UpdateCatch

> 관심 있는 소프트웨어와 Frontier AI 모델의 릴리즈 소식을 자동으로 수집하고, **Vector DB**에 색인하여 **.NET Razor 웹 대시보드**에서 AI 시맨틱(자연어) 검색과 핵심 요약을 제공하는 플랫폼입니다.

![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Razor Pages](https://img.shields.io/badge/ASP.NET%20Core-Razor%20Pages-blue.svg)
![Vector DB](https://img.shields.io/badge/Vector%20DB-Cosine%20Similarity-orange.svg)
![GitHub Actions](https://img.shields.io/badge/GitHub%20Actions-Automated%20Collector-green.svg)

---

## 🌟 주요 기능

1. **자동 수집 파이프라인 (GitHub Actions)**:
   - 6시간 주기 Cron 스케줄러로 서버 비용 없이 신규 릴리즈 자동 감지 & 수집.
   - 수집된 데이터 및 벡터 임베딩을 저장소의 `data/` 디렉터리에 자동으로 커밋 & 동기화.
2. **지원 및 특화 수집 타겟**:
   - 💻 **VS Code**: 에디터, 터미널, Copilot AI 섹션별 청킹 및 핵심 3줄 요약.
   - 🐍 **Python (CPython)**: 신규 문법(PEP), 성능(JIT/GIL), 지원 중단(Deprecations) 및 보안 패치.
   - 🤖 **Frontier AI 모델 (Gemini, Claude, GPT)**: API 체인지로그, 가격/스펙 변동, 크로스 모델 비교.
3. **AI 시맨틱 검색 (Vector DB)**:
   - 키워드가 정확히 일치하지 않아도 *"최근 보안 취약점 패치된 버전"*, *"터미널 개선"* 등 의도 기반 검색.
   - 코사인 유사도 점수 산출 및 관련 섹션 원문 발췌.
4. **.NET 10 Razor 모던 웹 대시보드**:
   - 반응형 타임라인 피드 뷰, 태그 필터링, 원문 링크 제공.

---

## 🏗️ 솔루션 아키텍처

- **`src/UpdateCatch.Core`**: 도메인 엔티티(`SoftwareTarget`, `ReleaseItem`, `VectorDocument`), 크롤러(GitHub API, RSS, Web), 텍스트 청커, 임베딩(`Gemini`, `Fallback TF-IDF`), `JsonFileVectorDatabase` (코사인 유사도 내장).
- **`src/UpdateCatch.Collector`**: GitHub Actions에서 단독 실행되는 콘솔 배치 프로그램.
- **`src/UpdateCatch.Web`**: ASP.NET Core Razor Pages 웹 애플리케이션.
- **`targets.json`**: 모니터링 타겟 설정 파일.
- **`data/`**: 수집된 릴리즈(`releases.json`) 및 벡터 임베딩(`vectors.json`).

---

## 🚀 빠른 시작 (로컬 실행)

### 1. 업데이트 수집기 실행
```bash
# GitHub 토큰 및 Gemini API 키 설정 (선택 사항: 키 미설정 시 Fallback 임베딩으로 즉시 동작)
export GITHUB_TOKEN="your_github_token"
export GEMINI_API_KEY="your_gemini_api_key"

# 수집 실행
dotnet run --project src/UpdateCatch.Collector
```

### 2. 웹 대시보드 실행
```bash
dotnet run --project src/UpdateCatch.Web
```
브라우저에서 `http://localhost:5000` (또는 터미널에 표시된 포트)로 접속합니다.

---

## ⚙️ GitHub Actions 연동 가이드

1. 이 저장소를 GitHub 리포지토리에 푸시합니다:
   ```bash
   git remote add origin https://github.com/YOUR_USER/UpdateCatch.git
   git push -u origin main
   ```
2. 저장소의 **Settings > Secrets and variables > Actions**에서 다음 Secrets를 추가합니다 (선택 사항):
   - `GEMINI_API_KEY`: 고품질 768차원 텍스트 임베딩을 위한 Google Gemini API Key
3. 저장소의 **Settings > Actions > General > Workflow permissions**에서:
   - **Read and write permissions**를 선택하고 저장합니다. (GitHub Actions가 수집한 `data/`를 저장소에 커밋할 수 있도록 권한 부여)
4. 이제 매 6시간마다 자동으로 최신 릴리즈가 수집되어 저장소에 반영됩니다!

---

## ➕ 새로운 모니터링 타겟 추가하기

루트의 `targets.json` 파일에 새로운 항목을 추가하고 Git에 커밋하기만 하면 됩니다:

```json
{
  "Id": "rust",
  "Name": "Rust Programming Language",
  "Category": "Programming Language",
  "Type": "GitHubRelease",
  "SourceUrl": "https://api.github.com/repos/rust-lang/rust/releases",
  "WebChangelogUrl": "https://blog.rust-lang.org/",
  "Description": "Rust 언어 공식 릴리즈 및 체인지로그"
}
```
