namespace AgentRelay.Desktop;

internal static class CliTaskUiText
{
    public static void AddTo(Dictionary<string,string[]> entries)
    {
        void Add(string en,string cn,string tw,string ja,string ko,string es)=>entries[en]=[en,cn,tw,ja,ko,es];
        Add("New CLI task","新建 CLI 任务","新增 CLI 任務","CLI タスクを作成","새 CLI 작업","Nueva tarea CLI");
        Add("Work request","工作要求","工作要求","作業指示","작업 요청","Solicitud de trabajo");
        Add("Planned steps","交接计划","交接計畫","手順","계획 단계","Pasos previstos");
        Add("Current step","当前步骤","目前步驟","現在の手順","현재 단계","Paso actual");
        Add("New conversation folder (optional)","新会话工作目录（选填，留空使用调用方案）","新會話工作目錄（選填，留空使用呼叫方案）","新規会話の作業フォルダー（任意）","새 대화 작업 폴더(선택)","Carpeta de la nueva conversación (opcional)");
        Add("Create and send through CLI","创建并通过 CLI 发送","建立並透過 CLI 傳送","作成して CLI で送信","만들고 CLI로 전송","Crear y enviar por CLI");
        Add("Write the work request, choose the conversations and send it through CLI. A real call may incur model charges.","填写工作要求、选择双方会话，软件将直接通过 CLI 安排执行。真实调用可能产生模型费用。","填寫工作要求、選擇雙方會話，軟體將直接透過 CLI 執行；真實呼叫可能收費。","指示と会話を指定して CLI で実行します。モデル料金が発生する場合があります。","요청과 대화를 선택해 CLI로 실행합니다. 모델 요금이 발생할 수 있습니다.","Escribe la solicitud y elige conversaciones. La llamada CLI puede generar cargos.");
        Add("Choose the source conversation for exact replies.","请选择来源会话，回稿才能准确续接。","請選擇來源會話，回稿才能準確續接。","正確に返信する元の会話を選択。","정확한 회신을 위해 원본 대화를 선택하세요.","Elige la conversación de origen para responder con precisión.");
        Add("Require recipient → sender → recipient before completing","完成前依次交给接收方 → 发起方 → 接收方","完成前依序交給接收方 → 發起方 → 接收方","完了前に受信者 → 送信者 → 受信者","완료 전 수신자 → 발신자 → 수신자","Completar receptor → emisor → receptor antes de finalizar");
        Add("Allow enabling the connection directions required by this task","允许启用本任务所需的连接方向（后续同方向也将启用）","允許啟用任務所需連線方向（後續同方向亦啟用）","必要な方向を今後も有効化する","필요한 연결 방향을 이후에도 활성화","Permitir activar las direcciones necesarias también para futuras tareas");
        Add("Both conversations stay bound to this task. An unfinished planned step cannot be skipped by an early completion receipt.","双方会话持续绑定到本任务。计划尚有步骤时，不会因为中途回执说“完成”就跳过。","雙方會話持續綁定此任務；計畫未完成時，不會因中途回執而跳過步驟。","会話をタスクに固定し、未実施の手順を飛ばしません。","대화는 작업에 고정되며 남은 단계를 건너뛰지 않습니다.","Las conversaciones quedan vinculadas y no se omiten pasos pendientes.");
        Add("Sender and recipient must be different.","发送方与接收方不能相同。","發送方與接收方不能相同。","送信者と受信者は別にしてください。","발신자와 수신자가 같을 수 없습니다.","Emisor y receptor deben ser distintos.");
        Add("Enter the work request.","请填写工作要求。","請填寫工作要求。","作業指示を入力。","작업 요청을 입력하세요.","Escribe la solicitud.");
        Add("The recipient has no CLI calling method. Configure it in Agents first.","接收方尚未配置 CLI 调用方案，请先到 Agent 管理配置。","接收方尚未設定 CLI，請先到 Agent 管理設定。","受信者の CLI を Agent 管理で設定してください。","Agent 관리에서 수신자의 CLI를 설정하세요.","Configura el CLI del receptor en Agentes.");
    }
}
