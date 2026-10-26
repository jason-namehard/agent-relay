using AgentRelay.Core;

namespace AgentRelay.Core.Tests;

[TestClass]
public sealed class CliFailureDiagnosticsTests
{
    [TestMethod]
    public void MissingModelOffersExplicitHostSelectionOrExistingSession()
    {
        var message = CliFailureDiagnostics.Explain(new(1, "", "Error: Select a model before sending a message (traceId: fixture)", false));
        StringAssert.Contains(message, "尚未选择可用模型");
        StringAssert.Contains(message, "已有且已配置模型的会话");
        Assert.DoesNotContain("--model", message, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture", message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AuthenticationFailureDoesNotLookLikeAnOutputFormatProblem()
    {
        var message = CliFailureDiagnostics.Explain(new(0, "{\"error\":\"Authentication required\"}", "", false));
        StringAssert.Contains(message, "尚未完成登录");
        Assert.DoesNotContain("输出格式", message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TimeoutWarnsAboutUncertainSubmission()
    {
        var message = CliFailureDiagnostics.Explain(new(-1, "", "", true));
        StringAssert.Contains(message, "可能已经提交");
        StringAssert.Contains(message, "不要重复发送");
    }
}
