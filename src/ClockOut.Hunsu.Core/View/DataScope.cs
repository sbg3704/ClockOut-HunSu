// 기능: 모듈이 구독한 주제들의 최신 값을 모아, 선언의 경로("selection.summary.sum")로 값을 찾는다.
// 사용법: var scope = new DataScope(); scope.Set("selection.summary", dataElement); scope.TryGet("selection.summary.sum", out var v)
// 의존: System.Text.Json. 주제 이름에 점이 들어가므로 "가장 긴 주제 이름이 앞부분과 맞는 것"을 고른다.
using System.Text.Json;

namespace ClockOut.Hunsu.Core.View;

public sealed class DataScope
{
    private readonly Dictionary<string, JsonElement> _topics = new(StringComparer.Ordinal);

    public void Set(string topic, JsonElement data) => _topics[topic] = data.Clone();

    public bool Has(string topic) => _topics.ContainsKey(topic);

    public bool TryGet(string path, out JsonElement value)
    {
        value = default;
        string? bestTopic = null;
        foreach (var t in _topics.Keys)
        {
            if ((path == t || path.StartsWith(t + ".", StringComparison.Ordinal)) && (bestTopic == null || t.Length > bestTopic.Length))
                bestTopic = t;
        }
        if (bestTopic == null) return false;
        value = _topics[bestTopic];
        if (path.Length == bestTopic.Length) return true;
        foreach (var part in path[(bestTopic.Length + 1)..].Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return false;
        }
        return true;
    }
}
