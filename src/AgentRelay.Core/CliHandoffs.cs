using System.Text;

namespace AgentRelay.Core;

public sealed record CliHandoffRequest(string From,string To,string Prompt,string? OriginSessionId=null,
    string? TargetSessionId=null,bool ReviewAndApply=false,bool EnableDirection=false,
    string? OriginCwd=null,string? TargetCwd=null);

public sealed partial class RelayService
{
    public TaskView CreateCliHandoff(CliHandoffRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.Prompt))
            throw new RelayException("cli_work_required","请填写要交给接收 Agent 执行的工作要求。");
        if(request.From==request.To)throw new RelayException("manual_same_agent","发送方与接收方不能相同。");
        if(request.OriginSessionId is not null&&string.IsNullOrWhiteSpace(request.OriginSessionId)||
            request.TargetSessionId is not null&&string.IsNullOrWhiteSpace(request.TargetSessionId))
            throw new RelayException("session_id_required","请提供准确的会话 ID；新建目标会话请将 target_session_id 留为 null。");
        var agents=Agents();
        _=agents.SingleOrDefault(a=>a.AgentId==request.From)??throw new RelayException("agent_not_found","未找到发送 Agent。");
        _=agents.SingleOrDefault(a=>a.AgentId==request.To)??throw new RelayException("agent_not_found","未找到接收 Agent。");
        var target=CliExtension(request.To)??throw new RelayException("cli_recipe_missing","接收 Agent 尚未配置 CLI 调用方案，请先在 Agent 管理中配置。");
        if(request.TargetSessionId is not null&&target.Recipe.Resume is null)
            throw new RelayException("session_resume_unsupported","接收 Agent 的调用方案不支持续接已有会话，请选择新建会话或补充续接命令。");
        if(request.ReviewAndApply)
        {
            if(string.IsNullOrWhiteSpace(request.OriginSessionId))
                throw new RelayException("cli_origin_required","多轮 CLI 任务需要明确的来源会话，才能把回稿准确送回发送方。");
            if(CliExtension(request.From)?.Recipe.Resume is null||target.Recipe.Resume is null)
                throw new RelayException("session_resume_unsupported","多轮 CLI 任务要求双方均配置准确会话续接命令。");
        }
        foreach(var cwd in new[]{request.OriginCwd,request.TargetCwd})
            if(cwd is not null&&!Path.IsPathFullyQualified(cwd))throw new RelayException("session_cwd_invalid","会话项目目录需要完整路径。");
        var directions=request.ReviewAndApply?new[]{(request.From,request.To),(request.To,request.From)}:new[]{(request.From,request.To)};
        var permissions=ConnectionPermissions();
        foreach(var (from,to) in directions)
            if(!permissions.Any(p=>p.From==from&&p.To==to&&p.Decision=="allow")&&!request.EnableDirection)
                throw new RelayException("cli_direction_disabled","此 CLI 任务需要的连接方向尚未启用。请在连接图启用，或明确勾选允许本次启用所需方向。");

        var drafts=Path.Combine(Home,".cli-drafts");Directory.CreateDirectory(drafts);
        var source=Path.Combine(drafts,"work-"+Guid.NewGuid().ToString("N")+".md");
        TaskView created;
        File.WriteAllText(source,request.Prompt.Trim(),new UTF8Encoding(false));
        try{created=CreateFileHandoff(new(request.From,request.To,source,"请执行工作要求文件中的任务，并按任务计划写回执。"));}
        finally{File.Delete(source);}
        var taskId=created.Task.TaskId;
        if(request.OriginSessionId is not null)BindManualTaskSession(taskId,request.From,request.OriginSessionId,request.OriginCwd);
        if(request.TargetSessionId is not null)BindManualTaskSession(taskId,request.To,request.TargetSessionId,request.TargetCwd);
        else UseNewTaskSession(taskId,request.To);
        if(request.ReviewAndApply)ConfigureHandoffPlan(taskId,[request.To,request.From,request.To]);
        foreach(var (from,to) in directions)
            if(request.EnableDirection)SetPermission(from,to,"allow");
        store.Write(s=>
        {
            var task=s.Task(taskId);task.Mode="cli";task.AdapterId="cli:"+request.To;
            task.CliNewSessionCwd=request.TargetSessionId is null?request.TargetCwd:null;
            s.Save(task);s.Event(task,"cli_handoff.created","operator",Now);
            return true;
        });
        return Status(taskId);
    }

    public string FileHandoffPrompt(TaskRecord task)
    {
        if(!task.IsFileHandoff)throw new RelayException("file_handoff_required","此接口只用于文件或 CLI 工作交接。");
        string Name(string id)=>Agents().SingleOrDefault(a=>a.AgentId==id)?.DisplayName??id;
        var receipt=ManualReceiptPath(task);
        var context=Path.Combine(Path.GetDirectoryName(receipt)!,"context.json");
        var current=task.AwaitingAgentId??task.RecipientId;
        var requiredNext=task.HandoffPlan is {} plan&&task.ManualTurn+1<plan.Length;
        var template=new ManualReceipt(task.TaskId,task.AttemptId,current,task.ManualTurn,"done",requiredNext);
        File.WriteAllText(context,RelayJson.Write(new
        {
            sender_name=Name(task.ManualTurn==0?task.RequesterId:current==task.RequesterId?task.RecipientId:task.RequesterId),
            recipient_name=Name(current),current_file=task.ManualCurrentFile??task.RequestPath,original_request_file=task.RequestPath,request=task.ManualNextAction,
            reply_file=task.ResultPath,receipt_file=receipt,receipt_template=template,
            handoff_plan=task.HandoffPlan,required_next_agent=requiredNext?task.HandoffPlan![task.ManualTurn+1]:null,
            remaining_steps=task.HandoffPlan?.Skip(task.ManualTurn).Select(id=>new{agent_id=id,name=Name(id)}),
            instructions="Read current_file and perform the request. Write the receipt JSON to receipt_file when finished; do not ask the user to import your reply or create another handoff JSON. status: received/working/done/failed. Keep task_id, attempt_id, sender and turn unchanged. Follow handoff_plan: if required_next_agent is set, this task is not finished; save a real result_file, set needs_reply=true and provide concrete next_action for that Agent. Set needs_reply=false only when no required work remains. Never set needs_reply=false while also asking for further work in next_action. No greetings or repeated confirmations. Limit: 4 completion turns. Permanent deliverables belong in the project. Publish one complete receipt or rename a temporary file."
        }));
        return "A file is waiting for you. Read "+context+", perform the request and write its receipt. No extra acknowledgement message is needed.";
    }
}
