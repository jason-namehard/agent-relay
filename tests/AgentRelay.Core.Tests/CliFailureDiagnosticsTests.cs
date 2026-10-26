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
        StringAssert.Contains(message, "没有可用的登录");
        StringAssert.Contains(message, "单独登录");
        Assert.DoesNotContain("输出格式", message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AuthenticationRefusalWithExitCodeZeroBlocksAndShowsTheRecipesLoginCommand()
    {
        var recipe = new CliRecipe(1, "fixture", "fixture", new("codebuddy", ["-p", "{prompt}"], null, null), null, [], "", "", "", "",
            "claude-json", "codebuddy");
        var failure = CliFailureDiagnostics.Classify(new(0, "", "Authentication required. Please use /login command to sign in", false), recipe);
        Assert.AreEqual("auth_required", failure.Kind); Assert.IsTrue(failure.Blocking);
        StringAssert.Contains(failure.Message, "在终端运行 codebuddy");
    }

    [TestMethod]
    public void ExhaustedPlanBlocksAutomaticDelivery()
    {
        var failure = CliFailureDiagnostics.Classify(new(1, "", "ProviderBusinessError: [1113][Insufficient balance or no resource package. Please recharge.]", false));
        Assert.AreEqual("quota_exhausted", failure.Kind); Assert.IsTrue(failure.Blocking);
        StringAssert.Contains(failure.Message, "余额不足");
    }

    [TestMethod]
    public void TimeoutWarnsAboutUncertainSubmission()
    {
        var message = CliFailureDiagnostics.Explain(new(-1, "", "", true));
        StringAssert.Contains(message, "可能已经提交");
        StringAssert.Contains(message, "不要重复发送");
    }
}
