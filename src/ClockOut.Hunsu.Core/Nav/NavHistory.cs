// 기능: 최근 이동 기록. 사용자가 고른 칸(통합 문서·시트·주소)을 브라우저 방문 기록처럼 쌓고, 뒤로·앞으로 가기를 돕는다. 앱이 상태를 갖는다(다리는 모름).
//       범위를 끄는 동안처럼 짧은 사이(SettleMs) 안에 바뀐 선택은 한 줄로 합친다. 뒤로 가기로 생긴 선택은 기록하지 않는다(되돌아온 자리를 또 쌓지 않음).
// 사용법: var h = new NavHistory(); h.Observe(book, sheet, addr, nowMs); var e = h.Back(nowMs); → 그 자리로 goto;  h.ToJson(book, nowMs) → 모듈 주제 app.history
// 의존: System.Text.Json. 시각은 ms(호출하는 쪽의 시계). 셀 값은 담지 않는다(주소만).
using System.Text;
using System.Text.Json;

namespace ClockOut.Hunsu.Core.Nav;

public sealed record NavEntry(string Book, string Sheet, string Addr, long At)
{
    public bool SamePlace(NavEntry o) => Book == o.Book && Sheet == o.Sheet && Addr == o.Addr;

    /// <summary>"$B$3:$C$9" → "B3:C9" (보여 줄 때).</summary>
    public string ShortAddr => Addr.Replace("$", "", StringComparison.Ordinal);
}

public sealed class NavHistory(int capacity = 30, long settleMs = 1000)
{
    private readonly List<NavEntry> _items = [];
    private int _index = -1; // 지금 자리(뒤로 가면 앞쪽 기록은 남겨 두어 앞으로 갈 수 있다)
    private NavEntry? _expect; // 뒤로·앞으로 가기로 보낸 자리: 그 선택이 돌아오면 기록하지 않는다
    private long? _lastObservedAt;

    public int Count => _items.Count;
    public int Index => _index;
    public NavEntry? Current => _index >= 0 ? _items[_index] : null;
    public bool CanBack => _index > 0;
    public bool CanForward => _index >= 0 && _index < _items.Count - 1;

    /// <summary>바뀐 기록이 있으면 true.</summary>
    public bool Observe(string book, string sheet, string addr, long nowMs)
    {
        if (string.IsNullOrEmpty(addr)) return false;
        var e = new NavEntry(book ?? "", sheet ?? "", addr, nowMs);
        long since = _lastObservedAt is long last ? nowMs - last : -1;
        _lastObservedAt = nowMs;
        if (_expect != null && _expect.SamePlace(e)) { _expect = null; return false; }
        _expect = null;
        if (Current is { } cur && cur.SamePlace(e)) return false;
        // 앞으로 갈 기록은 새 이동이 생기면 버린다(브라우저와 같음)
        if (_index < _items.Count - 1) _items.RemoveRange(_index + 1, _items.Count - _index - 1);
        // 짧은 사이에 또 바뀌면(끌기, 빠른 화살표) 마지막 줄을 바꿔 쓴다. 단, 첫 줄은 남긴다(출발점).
        if (since >= 0 && since < settleMs && _items.Count > 1) _items[^1] = e;
        else _items.Add(e);
        // 같은 자리를 다시 가면 예전 줄을 지우고 맨 뒤에 둔다(목록에 같은 칸이 두 번 보이지 않게)
        for (int i = _items.Count - 2; i >= 0; i--) if (_items[i].SamePlace(e)) _items.RemoveAt(i);
        while (_items.Count > capacity) _items.RemoveAt(0);
        _index = _items.Count - 1;
        return true;
    }

    public NavEntry? Back() => Move(-1);

    public NavEntry? Forward() => Move(+1);

    /// <summary>목록의 한 줄을 눌러 그 자리로 갈 때: 기록 순서는 바꾸지 않고 지금 자리만 옮긴다.</summary>
    public NavEntry? Jump(int index)
    {
        if (index < 0 || index >= _items.Count) return null;
        _index = index;
        _expect = _items[index];
        return _expect;
    }

    private NavEntry? Move(int step)
    {
        int next = _index + step;
        if (_index < 0 || next < 0 || next >= _items.Count) return null;
        _index = next;
        _expect = _items[next];
        return _expect;
    }

    /// <summary>모듈에 넘길 data: 최근 것이 위. book이 주어지면 그 통합 문서의 기록만(다른 책으로는 goto가 갈 수 없음).</summary>
    public string ToJson(string? book, long nowMs)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            int shown = 0;
            w.WriteStartArray("items");
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var e = _items[i];
                if (book != null && e.Book != book) continue;
                w.WriteStartObject();
                w.WriteNumber("index", i);
                w.WriteString("sheet", e.Sheet);
                w.WriteString("addr", e.Addr);
                w.WriteString("label", e.Sheet.Length > 0 ? e.Sheet + "!" + e.ShortAddr : e.ShortAddr);
                w.WriteString("ago", Ago(nowMs - e.At));
                w.WriteBoolean("current", i == _index);
                w.WriteEndObject();
                shown++;
            }
            w.WriteEndArray();
            w.WriteNumber("count", shown);
            w.WriteBoolean("canBack", CanBack);
            w.WriteBoolean("canForward", CanForward);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Ago(long ms) => ms switch
    {
        < 10_000 => "방금",
        < 60_000 => $"{ms / 1000}초 전",
        < 3_600_000 => $"{ms / 60_000}분 전",
        _ => $"{ms / 3_600_000}시간 전",
    };
}
