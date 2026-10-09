# AASX JSON Editor

AASX(Asset Administration Shell) 파일을 브라우저 또는 데스크톱 창에서 열고, 트리로 탐색하고, JSON 으로 편집해 다시 AASX 로 저장하는 Blazor 앱이다. 코어(RCL) 하나에 Web 호스트(Blazor Server)와 Desktop 호스트(WPF + BlazorWebView)를 얹었다.

앱 설명·기능·구조는 [`Apps/AasxEditor/README.md`](Apps/AasxEditor/README.md) 에 있다.

## 저장소 구성

```
Apps/AasxEditor/
  AasxEditor.Core/        공유 코어 (RCL) — 페이지 · 모델 · 서비스 · wwwroot
  AasxEditor.Web/         Blazor Server 호스트
  AasxEditor.Desktop/     WPF + BlazorWebView 호스트
  Installer/              Inno Setup 스크립트 · build-installer.bat · Redist(WebView2)
  scripts/fetch-monaco.ps1   Monaco Editor 번들 내려받기(git 제외 자산)
  AasxEditor.sln          이 저장소의 빌드 진입점
  README.md               앱 설명
  24_AASX_JSON_EDITOR_SPEC.md   규격
external/ds2              DS2 라이브러리 서브모듈 — Ds2.Aasx · Ds2.Core 사용
```

## 받기 · 빌드 · 실행

```bash
git clone --recurse-submodules https://github.com/DualsoftDev/ds2-AasxEditor.git
cd ds2-AasxEditor
dotnet build Apps/AasxEditor/AasxEditor.sln -nologo

dotnet run --project Apps/AasxEditor/AasxEditor.Web        # 웹
dotnet run --project Apps/AasxEditor/AasxEditor.Desktop    # 데스크톱
```

이미 받은 뒤 서브모듈이 비어 있으면 `git submodule update --init --recursive`. `AasxEditor.sln` 은 `external/ds2` 의 `Ds2.Aasx` 와 `Ds2.Aasx.Tests` 도 포함한다.

데스크톱 설치본은 `Apps/AasxEditor/Installer/build-installer.bat` (Inno Setup 6 필요). 오프라인 설치용 WebView2 런타임 동봉 방법은 [`Apps/AasxEditor/README.md`](Apps/AasxEditor/README.md) 의 인스톨러 절을 본다.

## License

**Apache License 2.0** — [`LICENSE`](LICENSE) · [`NOTICE`](NOTICE)
