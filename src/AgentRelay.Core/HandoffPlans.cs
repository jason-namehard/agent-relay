namespace AgentRelay.Core;

public sealed partial class RelayService
{
    public TaskView ConfigureHandoffPlan(string taskId, string[] expectedRecipients) => store.Write(s =>
    {
        var task = s.Task(taskId);
        if (!task.IsFileHandoff || task.State != TaskState.AwaitingManual || task.ManualTurn != 0 ||
            task.ManualReceiptJson is not null || task.CliFileTurn is not null)
            throw new RelayException("handoff_plan_started", "交接计划只能在首次投递或回执前设置；已开始任务不能改写已执行步骤。");
        if (expectedRecipients.Length is < 1 or > MaxManualTurns || expectedRecipients[0] != task.RecipientId ||
            expectedRecipients.Any(id => id != task.RequesterId && id != task.RecipientId) ||
            expectedRecipients.Skip(1).Where((id, index) => id == expectedRecipients[index]).Any())
            throw new RelayException("handoff_plan_invalid", "交接计划须从接收方开始，在双方之间交替，包含 1 至 4 个处理步骤。");
        task.HandoffPlan = expectedRecipients.ToArray();
        s.Save(task);
        s.Event(task, "handoff.plan_configured", "operator", Now, RelayJson.Write(task.HandoffPlan));
        return ManualView(task);
    });
}
