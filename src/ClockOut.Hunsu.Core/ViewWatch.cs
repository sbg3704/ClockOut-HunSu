// 기능: 엑셀 화면 자리(스크롤 위치·확대·창 나눔·시트)를 적어 둔 글을 지난번과 비교해 "움직였나"만 알려 준다.
//       표시층 다시 묻기를 화면이 실제로 움직였을 때만 하려고 쓴다(매번 다리 매크로를 돌리면 엑셀 커서가 1초마다 '바쁨'으로 깜빡였다, 2026-11-17).
// 사용법: var w = new ViewWatch(); if (w.Moved(key)) { ...다시 묻기... }  key가 null(읽지 못함)이면 움직이지 않은 것으로 본다. 처음 한 번은 늘 움직인 것.
// 의존: 없음(순수 계산).
namespace ClockOut.Hunsu.Core;

public sealed class ViewWatch
{
    private string? _last;

    public bool Moved(string? key)
    {
        if (key == null || string.Equals(key, _last, StringComparison.Ordinal)) return false;
        _last = key;
        return true;
    }

    /// <summary>다음 Moved를 무조건 참으로(엑셀이 바뀌었거나 표시를 지웠을 때).</summary>
    public void Forget() => _last = null;
}
