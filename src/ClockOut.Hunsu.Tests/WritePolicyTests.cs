// 기능: 쓰기 명령 규칙(Core/Write/WritePolicy)과 설정의 공지 확인 칸 시험. 화면·엑셀 없이.
// 사용법: dotnet test
// 의존: Core/Write, Core/Settings, Core/BridgeResponse.
using System.Text.Json.Nodes;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Core.Write;

namespace ClockOut.Hunsu.Tests;

public class WritePolicyTests
{
    [Theory]
    [InlineData("write.sheets.unhide", true)]
    [InlineData("write.", false)]
    [InlineData("goto", false)]
    [InlineData("app.back", false)]
    [InlineData("", false)]
    public void IsWrite_OnlyPrefixedNames(string cmd, bool expected) => Assert.Equal(expected, WritePolicy.IsWrite(cmd));

    [Fact]
    public void Notice_ShownUntilAcknowledgedForThisVersion()
    {
        Assert.True(WritePolicy.NeedsNotice(""));
        Assert.True(WritePolicy.NeedsNotice(null));
        Assert.True(WritePolicy.NeedsNotice("old"));
        Assert.False(WritePolicy.NeedsNotice(WritePolicy.NoticeVersion));
    }

    [Fact]
    public void WriteAck_RoundTripsThroughSettings()
    {
        var s = new HunsuSettings().WithWriteAck(WritePolicy.NoticeVersion);
        Assert.Equal(WritePolicy.NoticeVersion, s.WriteAck);
        Assert.Equal(WritePolicy.NoticeVersion, HunsuSettings.FromJson(s.ToJson()).WriteAck);
        Assert.Equal("", HunsuSettings.FromJson("{}").WriteAck);
    }

    [Fact]
    public void Confirmed_AddsFlagAndKeepsArgs()
    {
        var o = JsonNode.Parse(WritePolicy.Confirmed("{\"names\":\"a\\tb\"}"))!.AsObject();
        Assert.Equal("1", (string?)o["confirm"]);
        Assert.Equal("a\tb", (string?)o["names"]);
        Assert.Equal("1", (string?)JsonNode.Parse(WritePolicy.Confirmed(null))!["confirm"]);
        Assert.Equal("1", (string?)JsonNode.Parse(WritePolicy.Confirmed("{broken"))!["confirm"]);
    }

    [Fact]
    public void ReadPlan_AndResult_ParseBridgeResponses()
    {
        var plan = WritePolicy.ReadPlan(BridgeResponse.Parse("""{"ok":true,"proto":0,"data":{"phase":"plan","count":3,"summary":"숨은 시트 3개를 보이게 합니다."}}"""));
        Assert.Equal(3, plan!.Count);
        Assert.Contains("3개", WritePolicy.ConfirmBody(plan));
        Assert.Null(WritePolicy.ReadPlan(BridgeResponse.Parse("""{"ok":false,"proto":0,"code":"command_failed","error":"x"}""")));

        var res = WritePolicy.ReadResult(BridgeResponse.Parse("""{"ok":true,"proto":0,"data":{"phase":"apply","changed":3,"message":"끝","undoCmd":"write.sheets.rehide","undoArgs":"{\"names\":\"A\"}"}}"""));
        Assert.Equal(3, res!.Changed);
        Assert.Equal("write.sheets.rehide", res.UndoCommand);
        Assert.Equal("{\"names\":\"A\"}", res.UndoArgs);
    }

    [Fact]
    public void ReadResult_IgnoresUndoThatIsNotAWriteCommand()
    {
        var res = WritePolicy.ReadResult(BridgeResponse.Parse("""{"ok":true,"proto":0,"data":{"changed":1,"message":"m","undoCmd":"goto","undoArgs":"{}"}}"""));
        Assert.Null(res!.UndoCommand);
        Assert.Null(res.UndoArgs);
    }
}
