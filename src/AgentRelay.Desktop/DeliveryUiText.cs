namespace AgentRelay.Desktop;

internal static class DeliveryUiText
{
    public static void AddTo(Dictionary<string,string[]> entries)
    {
        void Add(string en,string cn,string tw,string ja,string ko,string es)=>entries[en]=[en,cn,tw,ja,ko,es];
        Add("New automatic transfer task","新建自动传递任务","新增自動傳遞任務","自動転送タスクを作成","자동 전달 작업 만들기","Nueva tarea automática");
        Add("New manual transfer task","新建手动传递任务","新增手動傳遞任務","手動転送タスクを作成","수동 전달 작업 만들기","Nueva tarea manual");
        Add("Transfer turn {0}","第 {0} 轮传递","第 {0} 輪傳遞","転送 {0} 回目","전달 {0}회차","Turno de transferencia {0}");
        Add("Check Agent bridge","检查 Agent 桥接","檢查 Agent 橋接","Agent 接続を確認","Agent 브리지 확인","Revisar puente del Agent");
        Add("Recheck and restore bridge","重新检测并恢复桥接","重新檢測並恢復橋接","再確認して接続を復元","재검사 후 브리지 복구","Revisar y restaurar el puente");
        Add("Bridge recovery result","桥接恢复结果","橋接恢復結果","接続復元の結果","브리지 복구 결과","Resultado de recuperación");
        Add("Probe tested at {0}. This is a saved result, separate from the current connection check.","测试时间：{0}。这是保存的探针结果，与当前连接检查分开显示。","測試時間：{0}。這是儲存的探針結果，與目前連線檢查分開顯示。","テスト日時：{0}。保存済み結果であり、現在の接続確認とは別です。","테스트 시간: {0}. 저장된 결과이며 현재 연결 검사와는 별개입니다.","Prueba realizada el {0}. Es un resultado guardado, separado de la conexión actual.");
        Add("Cancel task","取消任务","取消任務","タスクを中止","작업 취소","Cancelar tarea");
        Add("Cancel this task now? Relay will end the task immediately, stop waiting for receipts and stop scheduling further deliveries, even if a delivery is in progress. Work already started in the host may continue and must be stopped there separately. Existing files will be kept.","确定取消此任务？取消后，Relay 将立即结束任务，停止等待回执和后续投递；即使正在传输，本任务也会结束。宿主中已开始的工作可能继续执行，需在该宿主中另行停止。已有文件将保留。","確定取消此任務？Relay 將立即結束任務、停止等待回執與後續投遞，即使正在傳輸。宿主中已開始的工作可能繼續，需另行停止；已有檔案保留。","今すぐ中止しますか？送信中でも Relay のタスクと今後の配信を終了します。ホスト側で開始済みの処理は別途停止が必要です。ファイルは保持します。","지금 취소할까요? 전송 중이어도 Relay 작업과 이후 전달을 종료합니다. 호스트에서 시작한 작업은 별도로 중지해야 합니다. 파일은 보존됩니다.","¿Cancelar ahora? Relay termina la tarea y futuras entregas incluso si está enviando. El trabajo ya iniciado en el anfitrión puede continuar y debe detenerse allí. Se conservan los archivos.");
        Add("Copy bridge diagnostic prompt","复制桥接诊断提示词","複製橋接診斷提示詞","接続診断プロンプトをコピー","브리지 진단 프롬프트 복사","Copiar diagnóstico del puente");
        Add("Diagnostic prompt copied. Paste it into an Agent for local checks; do not start new probes or tasks.","诊断提示词已复制。粘贴给 Agent 做本机检查；不要额外发起探针或任务。","診斷提示詞已複製。貼給 Agent 做本機檢查；不要額外發起探針或任務。","診断プロンプトをコピーしました。実モデルを呼ばずに接続を調査するよう Agent に渡してください。","진단 프롬프트를 복사했습니다. 실제 모델 호출 없이 브리지를 조사하도록 Agent에게 전달하세요.","Diagnóstico copiado. Pégalo en un Agent para revisar el puente sin nuevas llamadas al modelo.");
        Add("Saved failure at {0}. Automatic sends remain paused until you resolve it; use the connection check to see the current status.","这是 {0} 保存的失败记录，自动发送已暂停。请运行连接检查了解当前状态，处理后再解除阻塞。","這是 {0} 儲存的失敗記錄，自動傳送已暫停。請檢查目前連線，處理後再解除阻塞。","{0} に保存された失敗です。自動送信は停止中です。現在の接続を確認してください。","{0}에 저장된 실패입니다. 자동 전송은 중지되어 있습니다. 현재 연결 상태를 확인하세요.","Fallo guardado el {0}; envío automático pausado. Revisa la conexión actual antes de desbloquear.");
        Add("Sending to {0}…","正在发送给 {0}…","正在傳送給 {0}…","{0} に送信中…","{0}에 전송 중…","Enviando a {0}…");
        Add("Bridge blocked · {0}","桥接受阻 · {0}","橋接受阻 · {0}","接続がブロック · {0}","브리지 차단 · {0}","Puente bloqueado · {0}");
        Add("Delivery unconfirmed · {0}","投递未确认 · {0}","投遞未確認 · {0}","送信未確認 · {0}","전송 미확인 · {0}","Entrega sin confirmar · {0}");
        Add("Automatic delivery not verified · {0}","自动投递待验证 · {0}","自動投遞待驗證 · {0}","自動送信の検証待ち · {0}","자동 전송 검증 대기 · {0}","Envío automático sin verificar · {0}");
        Add("Paused · Bridge needs attention","已暂停 · 请检查桥接","已暫停 · 請檢查橋接","一時停止 · 接続を確認","일시 중지 · 브리지 확인 필요","Pausado · Revisa el puente");
        Add("This turn is already being sent. Wait for its result.","本轮正在发送，请等待结果，不要重复投递。","本輪正在傳送，請等待結果，不要重複投遞。","このターンは送信中です。結果をお待ちください。","이 차례는 전송 중입니다. 결과를 기다리세요.","Este turno ya se está enviando. Espera el resultado.");
        Add("This enables required routes that are paused or not yet authorized. It does not add a bridge or sign in to an Agent.","勾选后会启用本任务所需的已暂停或尚未授权方向；不会安装桥接，也不会替 Agent 登录。","勾選後會啟用本任務所需的已暫停或尚未授權方向；不會安裝橋接或替 Agent 登入。","必要な停止中・未承認の経路を有効にします。接続の追加やログインは行いません。","필요한 일시 중지·미승인 경로를 활성화합니다. 브리지 설치나 로그인은 하지 않습니다.","Activa rutas necesarias pausadas o sin autorizar. No instala puentes ni inicia sesión.");
    }
}
