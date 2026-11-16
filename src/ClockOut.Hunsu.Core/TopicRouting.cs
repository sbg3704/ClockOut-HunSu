// 기능: 다리 신호 하나가 왔을 때 어떤 주제를 다시 조회할지 정한다. 켜진 모듈의 구독(data.on)만 본다 — 꺼진 모듈은 엑셀을 바쁘게 하지 않는다.
// 사용법: var topics = TopicRouting.TopicsFor(SignalCodec.Sel, enabledModules.SelectMany(m => m.Data));
//       "app."으로 시작하는 주제(예: app.history)는 앱이 스스로 채우는 주제라 다리에 묻지 않는다.
// 의존: Manifest/ModuleManifest(DataSubscription), SignalCodec. 같은 주제는 한 번만 돌려준다.
using ClockOut.Hunsu.Core.Manifest;

namespace ClockOut.Hunsu.Core;

public static class TopicRouting
{
    /// <summary>앱이 채우는 주제·명령의 앞글자. 다리 조회·명령으로 보내지 않는다.</summary>
    public const string AppPrefix = "app.";

    public static bool IsAppTopic(string topic) => topic.StartsWith(AppPrefix, StringComparison.Ordinal);

    public static IReadOnlyList<string> TopicsFor(int signalCode, IEnumerable<DataSubscription> subscriptions)
    {
        string name = SignalCodec.NameOf(signalCode);
        var result = new List<string>();
        foreach (var sub in subscriptions)
        {
            if (sub.On.Contains(name) && !IsAppTopic(sub.Topic) && !result.Contains(sub.Topic)) result.Add(sub.Topic);
        }
        return result;
    }

    /// <summary>모듈이 켜질 때 처음 한 번 채울 주제(구독에 on이 하나라도 있는 모든 주제).</summary>
    public static IReadOnlyList<string> AllTopics(IEnumerable<DataSubscription> subscriptions) =>
        [.. subscriptions.Select(s => s.Topic).Where(t => !IsAppTopic(t)).Distinct(StringComparer.Ordinal)];
}
