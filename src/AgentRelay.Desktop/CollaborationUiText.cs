namespace AgentRelay.Desktop;

internal static class CollaborationUiText
{
    public static void AddTo(Dictionary<string,string[]> entries)
    {
        void Add(string en,string cn,string tw,string ja,string ko,string es)=>entries[en]=[en,cn,tw,ja,ko,es];
        Add("Collaboration","协作方式","協作方式","進め方","협업 방식","Colaboración");
        Add("Single handoff","单次交接","單次交接","1 回の引き継ぎ","단일 전달","Envío único");
        Add("Keep revising until the review passes","持续修改与验收，直到通过","持續修改與驗收，直到通過","レビューに通るまで修正を続ける","검토를 통과할 때까지 수정 반복","Revisar hasta que se apruebe");
        Add("The reviewer checks each version. The task finishes only after the reviewer passes it and the author accepts and records the deliverable.",
            "验收者检查每一版。只有验收通过、作者确认接受并写明交付物后，任务才算完成。","驗收者檢查每一版。只有驗收通過、作者確認接受並寫明交付物後，任務才算完成。",
            "レビュー担当が各版を確認します。合格し、作成者が受け入れて成果物を記録した時点で完了します。","검토자가 각 버전을 확인합니다. 통과 후 작성자가 수락하고 결과물을 기록해야 완료됩니다.",
            "El revisor comprueba cada versión. La tarea termina solo cuando se aprueba y el autor acepta y registra el entregable.");
        Add("Recipient does the work; sender reviews","接收方完成工作，发送方验收","接收方完成工作，傳送方驗收","受信者が作業し、送信者がレビュー","수신자가 작업하고 발신자가 검토","El receptor trabaja; el emisor revisa");
        Add("Sender already has the work; recipient reviews","发送方已有成果，接收方验收","傳送方已有成果，接收方驗收","送信者の成果を受信者がレビュー","발신자의 결과물을 수신자가 검토","El emisor ya tiene el trabajo; el receptor revisa");
        Add("Acceptance criteria","验收标准","驗收標準","受け入れ基準","승인 기준","Criterios de aceptación");
        Add("Maximum review rounds","最多验收轮数","最多驗收輪數","最大レビュー回数","최대 검토 횟수","Rondas de revisión máximas");
        Add("Time limit per turn (minutes)","单轮时限（分钟）","單輪時限（分鐘）","1 回あたりの制限時間（分）","회차별 제한 시간(분)","Límite por turno (minutos)");
        Add("Send each next turn through CLI automatically once that Agent's CLI is verified (may incur model charges)",
            "对方 CLI 已验证时，后续每一轮自动通过 CLI 发送（可能产生模型费用）","對方 CLI 已驗證時，後續每一輪自動透過 CLI 傳送（可能產生模型費用）",
            "相手の CLI が検証済みなら次の回を CLI で自動送信（モデル料金が発生する場合あり）","상대 CLI가 검증되면 다음 회차를 CLI로 자동 전송(모델 요금 발생 가능)",
            "Enviar cada turno por CLI automáticamente cuando el CLI esté verificado (puede generar cargos)");
        Add("Enter a whole number from {0} to {1}.","请输入 {0} 到 {1} 之间的整数。","請輸入 {0} 到 {1} 之間的整數。","{0}～{1} の整数を入力してください。","{0}에서 {1} 사이의 정수를 입력하세요.","Introduce un número entero de {0} a {1}.");
        Add("Chosen conversations stay bound to this task. Without a source conversation, turns for the sender are copied manually.",
            "所选会话持续绑定到本任务。未选来源会话时，交给发送方的轮次需手动复制提示词。","所選會話持續綁定此任務。未選來源會話時，交給傳送方的輪次需手動複製提示詞。",
            "選んだ会話はこのタスクに固定されます。元の会話を選ばない場合、送信者への回は手動でコピーします。","선택한 대화는 이 작업에 고정됩니다. 원본 대화가 없으면 발신자 차례는 수동으로 복사합니다.",
            "Las conversaciones elegidas quedan vinculadas. Sin conversación de origen, los turnos del emisor se copian a mano.");
        Add("Relay sends this turn to {0} through CLI automatically.","本轮将由 Relay 自动通过 CLI 发送给 {0}。","本輪將由 Relay 自動透過 CLI 傳送給 {0}。","この回は Relay が CLI で {0} に自動送信します。","이번 회차는 Relay가 CLI로 {0}에게 자동 전송합니다.","Relay envía este turno a {0} por CLI automáticamente.");
        Add("Not received · Allow resending","确认未收到 · 允许重发","確認未收到 · 允許重傳","未着を確認 · 再送を許可","미수신 확인 · 재전송 허용","No recibido · Permitir reenviar");
        Add("Check the target conversation first. Allow one more CLI send only if this turn did not arrive; otherwise the Agent may do the work twice.",
            "请先检查目标会话。只有确认本轮没有送达时才允许再发送一次，否则 Agent 可能重复处理。","請先檢查目標會話。只有確認本輪沒有送達時才允許再傳送一次，否則 Agent 可能重複處理。",
            "先に相手の会話を確認してください。この回が届いていない場合のみ再送を許可します。届いていると二重に作業される可能性があります。","먼저 대상 대화를 확인하세요. 이번 회차가 도착하지 않은 경우에만 재전송을 허용하세요. 그렇지 않으면 작업이 중복될 수 있습니다.",
            "Revisa primero la conversación. Permite otro envío solo si este turno no llegó; si no, el agente podría repetir el trabajo.");
        Add("Implementing","实现中","實作中","作業中","작업 중","Implementando");
        Add("Revising","修改中","修改中","修正中","수정 중","Revisando cambios");
        Add("Reviewing","验收中","驗收中","レビュー中","검토 중","En revisión");
        Add("Accepting the result","确认接受","確認接受","受け入れ確認","결과 수락 확인","Aceptando el resultado");
        Add("Paused · Waiting for your decision","已暂停 · 等待你决定","已暫停 · 等待你決定","一時停止 · 判断待ち","일시 중지 · 결정 대기","En pausa · Esperando tu decisión");
        Add("Paused","已暂停","已暫停","一時停止","일시 중지","En pausa");
        Add("Delivered","已交付","已交付","納品済み","전달 완료","Entregado");
        Add("Waiting for {0} · {1}","等待 {0} · {1}","等待 {0} · {1}","{0} を待機中 · {1}","{0} 대기 중 · {1}","Esperando a {0} · {1}");
        Add("{0} is processing · {1}","{0} 正在处理 · {1}","{0} 正在處理 · {1}","{0} が処理中 · {1}","{0} 처리 중 · {1}","{0} está trabajando · {1}");
        Add("Review loop · Author {0} · Reviewer {1} · Review round {2}/{3}","持续修改与验收 · 作者 {0} · 验收者 {1} · 第 {2}/{3} 轮验收","持續修改與驗收 · 作者 {0} · 驗收者 {1} · 第 {2}/{3} 輪驗收",
            "修正とレビュー · 作成者 {0} · レビュー担当 {1} · レビュー {2}/{3} 回目","수정·검토 · 작성자 {0} · 검토자 {1} · 검토 {2}/{3}회차","Revisión · Autor {0} · Revisor {1} · Ronda {2}/{3}");
        Add("Open items","待解决问题","待解決問題","未解決の項目","미해결 항목","Pendientes");
        Add("Deliverable","交付物","交付物","成果物","결과물","Entregable");
        Add("Next: if it passes, {0} accepts; if changes are needed, {0} revises.","下一步：通过则由 {0} 确认接受；需要修改则交回 {0} 修改。","下一步：通過則由 {0} 確認接受；需要修改則交回 {0} 修改。",
            "次：合格なら {0} が受け入れ、修正が必要なら {0} が修正します。","다음: 통과하면 {0}이(가) 수락하고, 수정이 필요하면 {0}이(가) 수정합니다.","Siguiente: si se aprueba, {0} acepta; si hay cambios, {0} los hace.");
        Add("Next: if {0} accepts, the task is delivered; a new change goes back to {1} for review.","下一步：{0} 确认接受即交付；如再修改，则交回 {1} 复验。","下一步：{0} 確認接受即交付；如再修改，則交回 {1} 複驗。",
            "次：{0} が受け入れると納品。新たな変更は {1} の再レビューへ戻ります。","다음: {0}이(가) 수락하면 전달 완료, 새 변경은 {1}의 재검토로 돌아갑니다.","Siguiente: si {0} acepta, se entrega; un cambio nuevo vuelve a {1} para revisión.");
        Add("Next: {0} reviews the submission.","下一步：由 {0} 验收提交内容。","下一步：由 {0} 驗收提交內容。","次：{0} が提出物をレビューします。","다음: {0}이(가) 제출물을 검토합니다.","Siguiente: {0} revisa la entrega.");
        Add("Turn {0} of at most {1}","第 {0} 轮，最多 {1} 轮","第 {0} 輪，最多 {1} 輪","{0} 回目（最大 {1} 回）","{0}회차 (최대 {1}회)","Turno {0} de {1} como máximo");
        Add("Note for the next Agent (optional)","给下一位 Agent 的说明（选填）","給下一位 Agent 的說明（選填）","次の Agent へのメモ（任意）","다음 Agent에게 남길 메모(선택)","Nota para el siguiente agente (opcional)");
        Add("Continue with {0} more rounds","追加 {0} 轮并继续","追加 {0} 輪並繼續","{0} 回追加して続行","{0}회 추가 후 계속","Continuar con {0} rondas más");
        Add("Continue","继续","繼續","続行","계속","Continuar");
        Add("submitted","已提交","已提交","提出","제출","Entregado para revisión");
        Add("changes_requested","要求修改","要求修改","修正依頼","수정 요청","Cambios solicitados");
        Add("approved","验收通过","驗收通過","合格","승인","Aprobado");
        Add("accepted","作者已接受","作者已接受","作成者が受け入れ","작성자 수락","Aceptado por el autor");
        Add("failed","处理失败","處理失敗","失敗","실패","Fallido");
        Add("Review history","验收记录","驗收紀錄","レビュー履歴","검토 기록","Historial de revisión");
        Add("CLI blocked · Action needed","CLI 受阻 · 需要处理","CLI 受阻 · 需要處理","CLI 停止中 · 対応が必要","CLI 차단됨 · 조치 필요","CLI bloqueado · Requiere acción");
        Add("Check CLI setup (no model call)","检查 CLI 配置（不调用模型）","檢查 CLI 設定（不呼叫模型）","CLI 設定を確認（モデル呼び出しなし）","CLI 설정 확인(모델 호출 없음)","Revisar configuración CLI (sin llamar al modelo)");
        Add("CLI capability","CLI 能力","CLI 能力","CLI 機能","CLI 기능","Capacidad CLI");
        Add("New conversations verified","新会话已验证","新會話已驗證","新規会話は検証済み","새 대화 검증됨","Conversaciones nuevas verificadas");
        Add("New conversations not verified","新会话未验证","新會話未驗證","新規会話は未検証","새 대화 미검증","Conversaciones nuevas sin verificar");
        Add("Resume verified","续接已验证","續接已驗證","再開は検証済み","이어가기 검증됨","Reanudación verificada");
        Add("Resume not verified","续接未验证","續接未驗證","再開は未検証","이어가기 미검증","Reanudación sin verificar");
        Add("Proven by a real handoff","已由真实交接证实","已由真實交接證實","実際の引き継ぎで確認済み","실제 전달로 확인됨","Comprobado con un envío real");
        Add("I have resolved this · Allow automatic sends","我已处理 · 允许自动发送","我已處理 · 允許自動傳送","対応済み · 自動送信を許可","처리 완료 · 자동 전송 허용","Ya lo resolví · Permitir envíos automáticos");
        Add("A probe or one real handoff whose Agent writes its receipt verifies the CLI. Only verified CLIs receive turns automatically; you can still send by hand.",
            "探针通过，或一次真实交接中 Agent 写回了回执，即可验证 CLI。只有已验证的 CLI 会自动接收轮次；仍可手动发送。","探針通過，或一次真實交接中 Agent 寫回了回執，即可驗證 CLI。只有已驗證的 CLI 會自動接收輪次；仍可手動傳送。",
            "プローブ、または Agent が応答を書いた実際の引き継ぎ 1 回で CLI を検証します。自動送信は検証済みの CLI のみで、手動送信は可能です。","프로브 또는 Agent가 회신을 기록한 실제 전달 1회로 CLI를 검증합니다. 검증된 CLI만 자동 수신하며 수동 전송은 가능합니다.",
            "Una prueba o un envío real en el que el agente escribe su recibo verifica el CLI. Solo los verificados reciben turnos automáticos; aún puedes enviar a mano.");
    }
}
