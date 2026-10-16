using Microsoft.AspNetCore.Components;

namespace AasxEditor.Components.Pages;

/// <summary>
/// Home 화면을 영역별로 나눈 하위 컴포넌트의 공통 기반. 상태와 핸들러는 모두 Home 에 있다.
/// 하위 컴포넌트에서 일어난 이벤트도 Home 이 처리한 것처럼 Home.StateHasChanged 로 화면 전체를 갱신하도록
/// 이벤트 처리를 Home 에 위임한다 (Home 이 다시 그려지면 Host 파라미터를 받은 하위 컴포넌트도 같이 다시 그려진다).
/// </summary>
public abstract class HomeSection : ComponentBase, IHandleEvent
{
    [Parameter, EditorRequired] public Home Host { get; set; } = default!;

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
        => ((IHandleEvent)Host).HandleEventAsync(callback, arg);
}
