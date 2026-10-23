namespace AgentRelay.Desktop;

public static class UiText
{
    private static readonly Dictionary<string, string[]> Entries = CreateEntries();

    public static string Translate(string source, string language)
    {
        var column = language switch
        {
            "en" => 0, "zh-CN" => 1, "zh-TW" => 2,
            "ja" => 3, "ko" => 4, "es" => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported UI language.")
        };
        // Task bodies, paths, JSON and external errors are opaque content.
        return Entries.TryGetValue(source, out var translations) ? translations[column] : source;
    }

    private static Dictionary<string, string[]> CreateEntries()
    {
        var entries = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Add(string en, string cn, string tw, string ja, string ko, string es, params string[] aliases)
        {
            string[] translations = [en, cn, tw, ja, ko, es];
            entries.Add(en, translations);
            foreach (var alias in aliases) entries.Add(alias, translations);
        }

        Add("Home", "首页", "首頁", "ホーム", "홈", "Inicio");
        Add("Agents", "Agent 管理", "Agent 管理", "Agent管理", "Agent 관리", "Agentes");
        Add("Connections", "连接", "連線", "接続", "연결", "Conexiones");
        Add("Inbox", "收件箱", "收件匣", "受信箱", "받은 편지함", "Bandeja de entrada");
        Add("Adapters", "适配器", "配接器", "アダプター", "어댑터", "Adaptadores");
        Add("Audit", "审计", "稽核", "監査", "감사", "Auditoría");
        Add("Settings", "设置", "設定", "設定", "설정", "Configuración");
        Add("Refresh", "刷新", "重新整理", "更新", "새로 고침", "Actualizar");
        Add("LOCAL COORDINATION", "本机协作", "本機協作", "ローカル連携", "로컬 협업", "COORDINACIÓN LOCAL");
        Add("Explicit handoffs.\nDurable results.", "明确交接。\n持久结果。", "明確交接。\n持久成果。", "明示的な引き継ぎ。\n結果を確実に保存。", "명확한 인계.\n지속되는 결과.", "Traspasos explícitos.\nResultados duraderos.");
        Add("Local coordination · Explicit authorization · Event driven", "本机协作 · 明确授权 · 事件驱动", "本機協作 · 明確授權 · 事件驅動", "ローカル連携 · 明示的な承認 · イベント駆動", "로컬 협업 · 명시적 승인 · 이벤트 기반", "Coordinación local · Autorización explícita · Por eventos", "本机协作 · 明确授权 · 事件驱动");
        Add("Agent Relay — Demo", "Agent Relay — 演示", "Agent Relay — 示範", "Agent Relay — デモ", "Agent Relay — 데모", "Agent Relay — Demostración");
        Add("DEMO · Fictional agents and preset results · No models called or active configuration read", "DEMO · 虚构 Agent 与预设结果 · 未调用模型，未读取现用配置", "DEMO · 虛構 Agent 與預設結果 · 未呼叫模型，未讀取現用設定", "DEMO · 架空のAgentと既定の結果 · モデル呼び出し・使用中の設定の読み取りなし", "DEMO · 가상 Agent와 사전 설정 결과 · 모델 호출 및 사용 중인 설정 읽기 없음", "DEMO · Agentes ficticios y resultados predefinidos · Sin llamadas a modelos ni lectura de la configuración activa", "DEMO · 虚构 Agent 与预设结果 · 未调用模型，未读取现用配置");
        Add("Relay data directory", "Relay 数据目录", "Relay 資料目錄", "Relayデータディレクトリ", "Relay 데이터 디렉터리", "Directorio de datos de Relay");

        Add("Start your first handoff", "开始首次交接", "開始首次交接", "最初の引き継ぎを開始", "첫 인계 시작", "Inicia tu primer traspaso");
        Add("Explore connections and results in an isolated demo; register and validate real paths in Agents to get started.", "先用隔离演示查看连接与结果；正式接入从 Agents 登记和验证真实路径开始。", "先用隔離示範查看連線與結果；正式接入請從 Agent 登記並驗證真實路徑開始。", "隔離されたデモで接続と結果を確認し、Agentで実際のパスを登録・検証して利用を開始します。", "격리된 데모에서 연결과 결과를 확인한 후 Agent에서 실제 경로를 등록하고 검증하세요.", "Explora las conexiones y los resultados en una demo aislada; registra y valida las rutas reales en Agentes para empezar.", "先用隔离演示查看连接与结果；正式接入从 Agents 登记和验证真实路径开始。");
        Add("Open Demo", "打开演示", "開啟示範", "デモを開く", "데모 열기", "Abrir demostración");
        Add("Register Agent", "登记 Agent", "登記 Agent", "Agentを登録", "Agent 등록", "Registrar agente");
        Add("Registered Agents", "已登记 Agent", "已登記 Agent", "登録済みAgent", "등록된 Agent", "Agentes registrados");
        Add("Pending Tasks", "待处理任务", "待處理任務", "保留中のタスク", "대기 중인 작업", "Tareas pendientes");
        Add("Needs Attention", "需要关注", "需要關注", "対応が必要", "주의 필요", "Requiere atención");
        Add("Retention", "保留期限", "保留期限", "保持期間", "보관 기간", "Retención");
        Add("days", "天", "天", "日", "일", "días");
        Add("Failures, timeouts, unknown delivery status, and results requiring manual continuation.", "失败、超时、投递未知，以及需要人工继续的结果。", "失敗、逾時、投遞狀態不明，以及需要人工繼續的結果。", "失敗、タイムアウト、配信状態不明、および手動での続行が必要な結果。", "실패, 시간 초과, 전달 상태 불명 및 수동 계속이 필요한 결과입니다.", "Fallos, tiempos de espera agotados, entregas de estado desconocido y resultados que requieren continuación manual.", "失败、超时、投递未知，以及需要人工继续的结果。");

        Add("Agent Registry", "Agent 登记", "Agent 登記", "Agent登録簿", "Agent 등록부", "Registro de agentes");
        Add("Registration and path validation only read host files; validate again after correcting paths.", "登记和路径校验只读取宿主文件；纠正路径后需重新验证。", "登記與路徑驗證只讀取宿主檔案；修正路徑後須重新驗證。", "登録とパス検証はホストファイルの読み取りのみを行います。パス修正後は再検証してください。", "등록과 경로 검증은 호스트 파일을 읽기만 합니다. 경로 수정 후 다시 검증하세요.", "El registro y la validación de rutas solo leen archivos del host; vuelve a validar tras corregir las rutas.", "登记和路径校验只读取宿主文件；纠正路径后需重新 Validate。");
        Add("Agent ID", "Agent ID", "Agent ID", "Agent ID", "Agent ID", "Agent ID");
        Add("Host", "宿主", "宿主", "ホスト", "호스트", "Host");
        Add("Validation", "验证", "驗證", "検証", "검증", "Validación");
        Add("Config path", "配置路径", "設定路徑", "設定パス", "설정 경로", "Ruta de configuración");
        Add("CLI path (optional)", "CLI 路径（选填）", "CLI 路徑（選填）", "CLIパス（任意）", "CLI 경로(선택)", "Ruta de CLI (opcional)");
        Add("Host version (optional)", "宿主版本（选填）", "宿主版本（選填）", "ホストのバージョン（任意）", "호스트 버전(선택)", "Versión del host (opcional)");
        Add("Instruction paths (one per line)", "指令文件路径（每行一个）", "指令檔案路徑（每行一個）", "指示ファイルのパス（1行に1件）", "지침 파일 경로(한 줄에 하나)", "Rutas de instrucciones (una por línea)");
        Add("Discover", "发现", "探索", "検出", "검색", "Detectar");
        Add("Browse config", "浏览配置", "瀏覽設定", "設定を参照", "설정 찾아보기", "Examinar configuración");
        Add("Register / Correct Path", "登记 / 修正路径", "登記 / 修正路徑", "登録 / パスを修正", "등록 / 경로 수정", "Registrar / Corregir ruta");
        Add("Validate", "验证", "驗證", "検証", "검증", "Validar");
        Add("Correct registry entry", "修正登记信息", "修正登記資訊", "登録情報を修正", "등록 정보 수정", "Corregir registro");
        Add("Only changes the Relay registry; does not write host files.", "仅修改 Relay 登记，不写宿主文件。", "僅修改 Relay 登記，不寫入宿主檔案。", "Relayの登録情報のみ変更します。ホストファイルには書き込みません。", "Relay 등록 정보만 수정하며 호스트 파일에는 쓰지 않습니다.", "Solo modifica el registro de Relay; no escribe archivos del host.", "仅修改 Relay 登记，不写宿主文件。");

        Add("Directed Connections", "有向连接", "有向連線", "方向別の接続", "방향별 연결", "Conexiones dirigidas");
        Add("Green: Allowed · Red: Explicit deny · Gray: Unavailable · Dashed: Default deny", "绿色：允许 · 红色：明确拒绝 · 灰色：不可用 · 虚线：默认拒绝", "綠色：允許 · 紅色：明確拒絕 · 灰色：無法使用 · 虛線：預設拒絕", "緑：許可 · 赤：明示的に拒否 · 灰：利用不可 · 破線：既定で拒否", "초록: 허용 · 빨강: 명시적 거부 · 회색: 사용 불가 · 점선: 기본 거부", "Verde: Permitido · Rojo: Denegación explícita · Gris: No disponible · Discontinuo: Denegado por defecto");
        Add("From", "发送方", "傳送方", "送信元", "보내는 쪽", "Origen");
        Add("To", "接收方", "接收方", "送信先", "받는 쪽", "Destino");
        Add("Permission", "权限", "權限", "権限", "권한", "Permiso");
        Add("Capability", "能力", "能力", "機能", "기능", "Capacidad");
        Add("Status", "状态", "狀態", "状態", "상태", "Estado");
        Add("Allow", "允许", "允許", "許可", "허용", "Permitir");
        Add("Deny", "拒绝", "拒絕", "拒否", "거부", "Denegar");
        Add("Allowed", "已允许", "已允許", "許可済み", "허용됨", "Permitido");
        Add("Denied", "已拒绝", "已拒絕", "拒否済み", "거부됨", "Denegado");
        Add("Default deny", "默认拒绝", "預設拒絕", "既定で拒否", "기본 거부", "Denegado por defecto");
        Add("Unavailable", "不可用", "無法使用", "利用不可", "사용 불가", "No disponible");
        Add("Unknown", "未知", "未知", "不明", "알 수 없음", "Desconocido");
        Add("Allow A → B", "允许 A → B", "允許 A → B", "A → Bを許可", "A → B 허용", "Permitir A → B");
        Add("Deny A → B", "拒绝 A → B", "拒絕 A → B", "A → Bを拒否", "A → B 거부", "Denegar A → B");
        Add("Change directed permission", "修改单向权限", "修改單向權限", "方向別の権限を変更", "방향별 권한 변경", "Cambiar permiso dirigido");
        Add("Only affects this Relay route direction; host-native permissions stay unchanged.", "仅影响该方向的 Relay 路由，不改变宿主原生权限。", "僅影響該方向的 Relay 路由，不變更宿主原生權限。", "この方向のRelayルートのみに適用され、ホスト固有の権限は変更しません。", "해당 방향의 Relay 경로에만 영향을 주며 호스트 자체 권한은 변경하지 않습니다.", "Solo afecta a esta dirección de la ruta de Relay; no cambia los permisos nativos del host.", "仅影响该方向的 Relay 路由，不改变宿主原生权限。");
        Add("Register agents to view directed connections.", "登记 Agent 后查看有向连接。", "登記 Agent 後查看有向連線。", "Agentを登録すると方向別の接続を表示できます。", "방향별 연결을 보려면 Agent를 등록하세요.", "Registra agentes para ver las conexiones dirigidas.");
        Add("Graph: explicit routes. Table: all directions + default deny.", "图：明确路由。表：全部方向与默认拒绝。", "圖：明確路由。表：全部方向與預設拒絕。", "グラフ：明示的なルート。表：全方向と既定の拒否。", "그래프: 명시적 경로. 표: 모든 방향 및 기본 거부.", "Gráfico: rutas explícitas. Tabla: todas las direcciones y denegación por defecto.");

        Add("Tasks & Inbox", "任务与收件箱", "任務與收件匣", "タスクと受信箱", "작업 및 받은 편지함", "Tareas y bandeja de entrada");
        Add("Refresh only reads the ledger. Recover handles pending deliveries and continuation, and may call verified adapters.", "刷新只读取账本。恢复会处理待投递和继续操作，可能调用已验证适配器。", "重新整理只讀取帳本。復原會處理待投遞與繼續操作，可能呼叫已驗證的配接器。", "更新は台帳の読み取りのみ行います。復旧は保留中の配信と続行を処理し、検証済みアダプターを呼び出す場合があります。", "새로 고침은 원장만 읽습니다. 복구는 대기 중인 전달과 계속 작업을 처리하며 검증된 어댑터를 호출할 수 있습니다.", "Actualizar solo lee el registro. Recuperar procesa entregas pendientes y continuaciones, y puede llamar a adaptadores verificados.", "Refresh 只读取账本。Recover 会处理待投递/继续操作，可能调用已验证适配器。");
        Add("Notifications", "通知", "通知", "通知", "알림", "Notificaciones");
        Add("Marking as read only changes the inbox, not task state.", "已读只改变收件箱，不改变任务状态。", "已讀只變更收件匣，不變更任務狀態。", "既読にすると受信箱のみ変更され、タスクの状態は変わりません。", "읽음 표시는 받은 편지함만 변경하며 작업 상태는 바꾸지 않습니다.", "Marcar como leído solo cambia la bandeja de entrada, no el estado de la tarea.", "已读只改变 Inbox，不改变任务状态。");
        Add("Event", "事件", "事件", "イベント", "이벤트", "Evento");
        Add("Message", "消息", "訊息", "メッセージ", "메시지", "Mensaje");
        Add("Created", "已创建", "已建立", "作成済み", "생성됨", "Creado");
        Add("Read", "已读", "已讀", "既読", "읽음", "Leído");
        Add("Mark Read", "标为已读", "標為已讀", "既読にする", "읽음으로 표시", "Marcar como leído");
        Add("Route", "路由", "路由", "ルート", "경로", "Ruta");
        Add("State", "状态", "狀態", "状態", "상태", "Estado");
        Add("Next action", "下一步操作", "下一步操作", "次の操作", "다음 작업", "Siguiente acción");
        Add("Deadline (UTC)", "截止时间 (UTC)", "截止時間 (UTC)", "期限 (UTC)", "마감 시간 (UTC)", "Fecha límite (UTC)");
        Add("Copy Prompt", "复制提示词", "複製提示詞", "プロンプトをコピー", "프롬프트 복사", "Copiar prompt");
        Add("Copied", "已复制", "已複製", "コピーしました", "복사됨", "Copiado");
        Add("Open Request", "打开请求", "開啟請求", "リクエストを開く", "요청 열기", "Abrir solicitud");
        Add("Open Result", "打开结果", "開啟結果", "結果を開く", "결과 열기", "Abrir resultado");
        Add("Recover", "恢复", "復原", "復旧", "복구", "Recuperar");
        Add("Recover task", "恢复任务", "復原任務", "タスクを復旧", "작업 복구", "Recuperar tarea");
        Add("Approve Dispatch", "批准投递", "核准投遞", "配信を承認", "전달 승인", "Aprobar envío");
        Add("Approve dispatch", "批准投递", "核准投遞", "配信を承認", "전달 승인", "Aprobar envío");
        Add("Retry", "重试", "重試", "再試行", "재시도", "Reintentar");
        Add("Retry task", "重试任务", "重試任務", "タスクを再試行", "작업 재시도", "Reintentar tarea");
        Add("Cancel", "取消", "取消", "キャンセル", "취소", "Cancelar");
        Add("Cancel Relay task", "取消 Relay 任务", "取消 Relay 任務", "Relayタスクをキャンセル", "Relay 작업 취소", "Cancelar tarea de Relay");
        Add("Recovery processes persisted transport and continuation outbox entries; it may call registered adapters.", "恢复会处理已持久化的运输和继续 outbox；可能调用已登记适配器。", "復原會處理已持久化的傳輸與繼續 outbox；可能呼叫已登記的配接器。", "復旧は永続化された転送と続行outboxを処理し、登録済みアダプターを呼び出す場合があります。", "복구는 저장된 전송 및 계속 outbox 항목을 처리하며 등록된 어댑터를 호출할 수 있습니다.", "La recuperación procesa el transporte y las continuaciones persistidos en outbox; puede llamar a adaptadores registrados.", "恢复会处理已持久化的运输和继续 outbox；可能调用已登记适配器。");
        Add("Only approves delivery; does not start a new model execution worker.", "仅批准投递；不启动新的模型执行 worker。", "僅核准投遞；不啟動新的模型執行 worker。", "配信のみ承認します。新しいモデル実行workerは起動しません。", "전달만 승인하며 새 모델 실행 worker는 시작하지 않습니다.", "Solo aprueba la entrega; no inicia un nuevo worker de ejecución del modelo.", "仅批准投递；不启动新的模型执行 worker。");
        Add("Creates a new attempt and may repeat side effects; does not automatically start an execution worker.", "将创建新尝试，可能重复副作用；不会自动启动执行 worker。", "將建立新嘗試，可能重複副作用；不會自動啟動執行 worker。", "新しい試行を作成し、副作用が重複する場合があります。実行workerは自動起動しません。", "새 시도를 만들며 부수 효과가 반복될 수 있습니다. 실행 worker는 자동 시작하지 않습니다.", "Crea un nuevo intento y puede repetir efectos secundarios; no inicia automáticamente un worker de ejecución.", "将创建新尝试，可能重复副作用；不会自动启动执行 worker。");
        Add("Stops further Relay dispatches; does not terminate agents already running in the host.", "停止 Relay 后续派发，不终止宿主中正在运行的 Agent。", "停止 Relay 後續派發，不終止宿主中正在執行的 Agent。", "Relayによる後続の配信を停止します。ホストで実行中のAgentは終了しません。", "Relay의 후속 전달을 중지하며 호스트에서 실행 중인 Agent는 종료하지 않습니다.", "Detiene los envíos posteriores de Relay; no finaliza agentes que ya se ejecutan en el host.", "停止 Relay 后续派发，不终止宿主中正在运行的 Agent。");
        Add("Delivery evidence:", "投递证据：", "投遞證據：", "配信の証跡：", "전달 증거:", "Evidencia de entrega:");
        Add("Audit:", "审计：", "稽核：", "監査：", "감사:", "Auditoría:");

        Add("Event Timeline", "事件时间线", "事件時間軸", "イベント履歴", "이벤트 타임라인", "Cronología de eventos");
        Add("Shown in ledger event order; task bodies are not included in the audit list.", "按账本事件顺序展示；任务正文不在审计列表中。", "依帳本事件順序顯示；任務本文不列入稽核清單。", "台帳のイベント順に表示します。タスク本文は監査一覧に含まれません。", "원장 이벤트 순서로 표시하며 작업 본문은 감사 목록에 포함되지 않습니다.", "Se muestra en el orden de eventos del registro; los cuerpos de las tareas no se incluyen en la lista de auditoría.", "按账本事件顺序展示；任务正文不在审计列表中。");
        Add("Task ID filter (empty = all)", "Task ID 筛选（留空为全部）", "Task ID 篩選（留空為全部）", "Task IDフィルター（空欄はすべて）", "Task ID 필터(비우면 전체)", "Filtro de Task ID (vacío = todos)");
        Add("Time (UTC)", "时间 (UTC)", "時間 (UTC)", "日時 (UTC)", "시간 (UTC)", "Hora (UTC)");
        Add("Task", "任务", "任務", "タスク", "작업", "Tarea");
        Add("Actor", "操作者", "操作者", "実行者", "실행자", "Actor");
        Add("Detail", "详情", "詳細資料", "詳細", "세부 정보", "Detalle");
        Add("Apply Filter", "应用筛选", "套用篩選", "フィルターを適用", "필터 적용", "Aplicar filtro");

        Add("Adapters & Migrations", "适配器与迁移", "配接器與遷移", "アダプターと移行", "어댑터 및 마이그레이션", "Adaptadores y migraciones");
        Add("Tests only run the explicitly specified CLI; source records do not automatically grant execution permissions.", "测试只运行明确指定的 CLI；来源记录不自动授予执行权限。", "測試只執行明確指定的 CLI；來源記錄不會自動授予執行權限。", "テストは明示されたCLIのみ実行します。出典の記録が実行権限を自動付与することはありません。", "테스트는 명시된 CLI만 실행하며 출처 기록이 실행 권한을 자동으로 부여하지 않습니다.", "Las pruebas solo ejecutan la CLI indicada explícitamente; los registros de origen no conceden permisos de ejecución automáticamente.", "Test 只运行明确指定的 CLI；来源记录不自动授予执行权限。");
        Add("Migrations", "迁移", "遷移", "移行", "마이그레이션", "Migraciones");
        Add("Adapter", "适配器", "配接器", "アダプター", "어댑터", "Adaptador");
        Add("Version", "版本", "版本", "バージョン", "버전", "Versión");
        Add("Verification", "验证状态", "驗證狀態", "検証状態", "검증 상태", "Verificación");
        Add("Executable", "可执行文件", "執行檔", "実行ファイル", "실행 파일", "Ejecutable");
        Add("AMQ executable", "AMQ 可执行文件", "AMQ 執行檔", "AMQ実行ファイル", "AMQ 실행 파일", "Ejecutable de AMQ");
        Add("Resumer kind", "继续适配器类型", "繼續配接器類型", "続行アダプターの種類", "계속 어댑터 유형", "Tipo de reanudador");
        Add("Test AMQ", "测试 AMQ", "測試 AMQ", "AMQをテスト", "AMQ 테스트", "Probar AMQ");
        Add("Test Executor", "测试执行器", "測試執行器", "実行アダプターをテスト", "실행기 테스트", "Probar ejecutor");
        Add("Test Resumer", "测试继续适配器", "測試繼續配接器", "続行アダプターをテスト", "계속 어댑터 테스트", "Probar reanudador");
        Add("Verify AMQ", "验证 AMQ", "驗證 AMQ", "AMQを検証", "AMQ 검증", "Verificar AMQ");
        Add("Verify executor", "验证执行器", "驗證執行器", "実行アダプターを検証", "실행기 검증", "Verificar ejecutor");
        Add("Verify resumer", "验证继续适配器", "驗證繼續配接器", "続行アダプターを検証", "계속 어댑터 검증", "Verificar reanudador");
        Add("Creates a verification queue in Relay's own directory without starting a model.", "在 Relay 自身目录创建验证队列，不启动模型。", "在 Relay 自身目錄建立驗證佇列，不啟動模型。", "Relay自身のディレクトリに検証用キューを作成します。モデルは起動しません。", "Relay 자체 디렉터리에 검증 대기열을 만들며 모델은 시작하지 않습니다.", "Crea una cola de verificación en el propio directorio de Relay sin iniciar un modelo.", "在 Relay 自身目录创建验证队列，不启动模型。");
        Add("Runs version and capability probes on the registered CLI without starting a model.", "执行已登记 CLI 的版本/能力探测，不启动模型。", "執行已登記 CLI 的版本與能力探測，不啟動模型。", "登録済みCLIのバージョンと機能を調査します。モデルは起動しません。", "등록된 CLI의 버전 및 기능을 확인하며 모델은 시작하지 않습니다.", "Comprueba la versión y las capacidades de la CLI registrada sin iniciar un modelo.", "执行已登记 CLI 的版本/能力探测，不启动模型。");
        Add("Runs help and version probes on the registered CLI without starting a model.", "执行已登记 CLI 的帮助/版本探测，不启动模型。", "執行已登記 CLI 的說明與版本探測，不啟動模型。", "登録済みCLIのヘルプとバージョンを調査します。モデルは起動しません。", "등록된 CLI의 도움말 및 버전을 확인하며 모델은 시작하지 않습니다.", "Consulta la ayuda y la versión de la CLI registrada sin iniciar un modelo.", "执行已登记 CLI 的帮助/版本探测，不启动模型。");

        Add("Config / manifest path", "配置 / 清单路径", "設定 / 資訊清單路徑", "設定 / マニフェストのパス", "설정 / 매니페스트 경로", "Ruta de configuración / manifiesto");
        Add("Format: json / toml", "格式：json / toml", "格式：json / toml", "形式：json / toml", "형식: json / toml", "Formato: json / toml");
        Add("Format", "格式", "格式", "形式", "형식", "Formato");
        Add("Manifest path", "清单路径", "資訊清單路徑", "マニフェストのパス", "매니페스트 경로", "Ruta del manifiesto");
        Add("Proof task ID", "证明任务 ID", "證明任務 ID", "証明用タスクID", "증명 작업 ID", "ID de tarea de prueba");
        Add("ID", "ID", "ID", "ID", "ID", "ID");
        Add("Purpose", "用途", "用途", "目的", "목적", "Propósito");
        Add("Target", "目标", "目標", "対象", "대상", "Destino");
        Add("Browse", "浏览", "瀏覽", "参照", "찾아보기", "Examinar");
        Add("Inventory", "清点配置", "盤點設定", "設定を調査", "설정 조사", "Inventariar");
        Add("Plan Manifest", "生成清单提案", "產生資訊清單提案", "マニフェストの計画を作成", "매니페스트 계획 생성", "Planificar manifiesto");
        Add("Apply", "应用", "套用", "適用", "적용", "Aplicar");
        Add("Apply migration", "应用迁移", "套用遷移", "移行を適用", "마이그레이션 적용", "Aplicar migración");
        Add("Validate Proof", "验证证明", "驗證證明", "証明を検証", "증명 검증", "Validar prueba");
        Add("Rollback", "回滚", "還原", "ロールバック", "롤백", "Revertir");
        Add("Rollback migration", "回滚迁移", "還原遷移", "移行をロールバック", "마이그레이션 롤백", "Revertir migración");
        Add("Recover State", "恢复状态", "復原狀態", "状態を復旧", "상태 복구", "Recuperar estado");
        Add("Current state", "当前状态", "目前狀態", "現在の状態", "현재 상태", "Estado actual");
        Add("Backup", "备份", "備份", "バックアップ", "백업", "Copia de seguridad");
        Add("Restores the original backup; subsequent changes prevent overwrites. Roll back the old-entry disable proposal before the new entry.", "恢复原始备份；后续变更将阻止覆盖。先回滚旧入口停用提案，再回滚新入口。", "還原原始備份；後續變更將阻止覆寫。先還原舊入口停用提案，再還原新入口。", "元のバックアップを復元します。後続の変更がある場合は上書きしません。旧エントリの無効化提案を戻してから新エントリを戻してください。", "원본 백업을 복원합니다. 이후 변경 사항이 있으면 덮어쓰지 않습니다. 기존 진입점 비활성화 제안을 먼저 롤백한 후 새 진입점을 롤백하세요.", "Restaura la copia original; los cambios posteriores impiden sobrescribirla. Revierte la propuesta de desactivación de la entrada anterior antes de la nueva.", "恢复原始备份；后续变更将阻止覆盖。先回滚旧入口停用提案，再回滚新入口。");

        Add("Local Settings", "本机设置", "本機設定", "ローカル設定", "로컬 설정", "Configuración local");
        Add("Deadlines are handled on the next command · No scheduled tasks, file watchers, or persistent services.", "截止时间在下一次命令时处理 · 无计划任务、文件监视器或常驻服务。", "截止時間於下一次命令時處理 · 無排程工作、檔案監看器或常駐服務。", "期限は次のコマンド実行時に処理 · スケジュールタスク、ファイル監視、常駐サービスなし。", "마감 시간은 다음 명령에서 처리 · 예약 작업, 파일 감시 또는 상주 서비스 없음.", "Los plazos se procesan en el siguiente comando · Sin tareas programadas, monitores de archivos ni servicios permanentes.", "Deadline Handling: On Next Command · 无计划任务、文件 watcher 或常驻服务。");
        Add("Artifact TTL (days)", "产物保留期限（天）", "產物保留期限（天）", "成果物の保持期間（日）", "산출물 보관 기간(일)", "Retención de artefactos (días)");
        Add("Task timeout (minutes)", "任务超时（分钟）", "任務逾時（分鐘）", "タスクのタイムアウト（分）", "작업 시간 제한(분)", "Tiempo límite de tarea (minutos)");
        Add("Delivery timeout (seconds)", "投递超时（秒）", "投遞逾時（秒）", "配信のタイムアウト（秒）", "전달 시간 제한(초)", "Tiempo límite de entrega (segundos)");
        Add("Delivery attempts (max 3)", "投递尝试次数（最多 3 次）", "投遞嘗試次數（最多 3 次）", "配信の試行回数（最大3回）", "전달 시도 횟수(최대 3회)", "Intentos de entrega (máximo 3)");
        Add("Save Settings", "保存设置", "儲存設定", "設定を保存", "설정 저장", "Guardar configuración");
        Add("Preview Cleanup", "预览清理", "預覽清理", "クリーンアップをプレビュー", "정리 미리 보기", "Previsualizar limpieza");
        Add("Apply Cleanup", "执行清理", "執行清理", "クリーンアップを実行", "정리 실행", "Aplicar limpieza");
        Add("Delete expired managed artifacts", "删除已过期的托管产物", "刪除已到期的受管理產物", "期限切れの管理対象成果物を削除", "만료된 관리 산출물 삭제", "Eliminar artefactos administrados caducados");
        Add("Desktop Notifications", "桌面通知", "桌面通知", "デスクトップ通知", "데스크톱 알림", "Notificaciones de escritorio");
        Add("Only manages the Relay notification helper; does not modify host configuration.", "仅管理 Relay 通知辅助程序，不改宿主配置。", "僅管理 Relay 通知輔助程式，不變更宿主設定。", "Relayの通知ヘルパーのみ管理します。ホスト設定は変更しません。", "Relay 알림 도우미만 관리하며 호스트 설정은 변경하지 않습니다.", "Solo administra el asistente de notificaciones de Relay; no modifica la configuración del host.", "仅管理 Relay 通知辅助程序，不改宿主配置。");
        Add("Notification helper path", "通知辅助程序路径", "通知輔助程式路徑", "通知ヘルパーのパス", "알림 도우미 경로", "Ruta del asistente de notificaciones");
        Add("Notification helper", "通知辅助程序", "通知輔助程式", "通知ヘルパー", "알림 도우미", "Asistente de notificaciones");
        Add("Enable Notifications", "启用通知", "啟用通知", "通知を有効化", "알림 활성화", "Activar notificaciones");
        Add("Disable Notifications", "停用通知", "停用通知", "通知を無効化", "알림 비활성화", "Desactivar notificaciones");
        Add("Enable Relay notifications", "启用 Relay 通知", "啟用 Relay 通知", "Relay通知を有効化", "Relay 알림 활성화", "Activar notificaciones de Relay");
        Add("Subsequent completion events may launch this program to display Windows notifications.", "后续完成事件可启动该程序显示 Windows 通知。", "後續完成事件可啟動此程式顯示 Windows 通知。", "以後の完了イベントでこのプログラムを起動し、Windows通知を表示できます。", "이후 완료 이벤트가 이 프로그램을 시작해 Windows 알림을 표시할 수 있습니다.", "Los eventos de finalización posteriores pueden iniciar este programa para mostrar notificaciones de Windows.", "后续完成事件可启动该程序显示 Windows 通知。");

        Add("Approval reason", "批准理由", "核准理由", "承認理由", "승인 사유", "Motivo de aprobación");
        Add("I have reviewed the target, specific changes and impact, and approve this operation.", "我已审阅目标、具体变更和影响，批准本次操作。", "我已審閱目標、具體變更與影響，核准本次操作。", "対象、具体的な変更内容、影響を確認し、この操作を承認します。", "대상, 구체적인 변경 사항 및 영향을 검토했으며 이 작업을 승인합니다.", "He revisado el destino, los cambios concretos y su impacto, y apruebo esta operación.", "我已审阅目标、具体变更和影响，批准本次操作。");
        Add("Approve", "批准", "核准", "承認", "승인", "Aprobar");

        Add("Language", "语言", "語言", "言語", "언어", "Idioma");
        Add("Appearance", "外观", "外觀", "外観", "모양", "Apariencia");
        Add("Color palette", "配色", "配色", "配色", "색상 팔레트", "Paleta de colores");
        Add("Custom accent (#RRGGBB)", "自定义强调色 (#RRGGBB)", "自訂強調色 (#RRGGBB)", "カスタムアクセント色 (#RRGGBB)", "사용자 지정 강조색 (#RRGGBB)", "Color de acento personalizado (#RRGGBB)");
        Add("Apply Appearance", "应用外观", "套用外觀", "外観を適用", "모양 적용", "Aplicar apariencia");
        Add("Sage", "鼠尾草", "鼠尾草", "セージ", "세이지", "Salvia");
        Add("Clay", "陶土", "陶土", "クレイ", "점토", "Arcilla");
        Add("Mist", "薄雾", "薄霧", "ミスト", "안개", "Niebla");
        Add("Rose", "玫瑰", "玫瑰", "ローズ", "장미", "Rosa");
        Add("Copy Setup Prompt", "复制接入提示词", "複製接入提示詞", "設定用プロンプトをコピー", "설정 프롬프트 복사", "Copiar prompt de configuración");
        Add("Paste Agent Reply", "粘贴 Agent 回复", "貼上 Agent 回覆", "Agentの返信を貼り付け", "Agent 응답 붙여넣기", "Pegar respuesta del agente");
        Add("Parse and Fill", "解析并填入", "解析並填入", "解析して入力", "분석하여 채우기", "Analizar y rellenar");
        Add("Clear Reply", "清空回复", "清空回覆", "返信を消去", "응답 지우기", "Borrar respuesta");
        Add("Configuration Assistant", "配置助手", "設定助手", "設定アシスタント", "설정 도우미", "Asistente de configuración");
        Add("The reply only fills this form. Review it before saving.", "回复只用于填写此表单。请审阅后再保存。", "回覆僅用於填寫此表單。請審閱後再儲存。", "返信はこのフォームへの入力のみに使用します。保存前に内容を確認してください。", "응답은 이 양식만 채웁니다. 저장하기 전에 검토하세요.", "La respuesta solo rellena este formulario. Revísala antes de guardar.");
        Add("Copied setup prompt. Paste it into the Agent you want to register.", "已复制接入提示词。请粘贴到要登记的 Agent。", "已複製接入提示詞。請貼到要登記的 Agent。", "設定用プロンプトをコピーしました。登録するAgentに貼り付けてください。", "설정 프롬프트를 복사했습니다. 등록할 Agent에 붙여넣으세요.", "Prompt de configuración copiado. Pégalo en el agente que quieras registrar.");
        Add("Agent ID is a local Relay label, not an account or login.", "Agent ID 是本机 Relay 标签，不是账号或登录信息。", "Agent ID 是本機 Relay 標籤，不是帳號或登入資訊。", "Agent IDはローカルのRelayラベルで、アカウントやログイン情報ではありません。", "Agent ID는 로컬 Relay의 이름표이며 계정이나 로그인 정보가 아닙니다.", "Agent ID es una etiqueta local de Relay, no una cuenta ni un inicio de sesión.");
        Add("Settings saved", "设置已保存", "設定已儲存", "設定を保存しました", "설정 저장됨", "Configuración guardada");
        Add("Language and appearance saved", "语言和外观已保存", "語言與外觀已儲存", "言語と外観を保存しました", "언어 및 모양 저장됨", "Idioma y apariencia guardados");
        Add("Choose language and palette", "选择语言和配色", "選擇語言與配色", "言語と配色を選択", "언어 및 색상 선택", "Elige el idioma y la paleta");
        Add("Reply JSON", "回复 JSON", "回覆 JSON", "返信JSON", "응답 JSON", "JSON de respuesta");
        Add("Parse succeeded", "解析成功", "解析成功", "解析に成功しました", "분석 성공", "Análisis completado");
        Add("Common values are kept as JSON values; no commands will run.", "通用值保留为 JSON 值；不会执行命令。", "通用值保留為 JSON 值；不會執行命令。", "共通の値はJSON値として保持し、コマンドは実行しません。", "일반 값은 JSON 값으로 유지되며 명령은 실행되지 않습니다.", "Los valores comunes se conservan como valores JSON; no se ejecutará ningún comando.");
        Add("Search paths and versions read-only. Never share credentials.", "只读查找路径和版本。切勿分享凭据。", "以唯讀方式查找路徑與版本。切勿分享認證資訊。", "パスとバージョンは読み取り専用で調べます。認証情報は共有しないでください。", "경로와 버전을 읽기 전용으로 확인하세요. 자격 증명은 공유하지 마세요.", "Busca rutas y versiones en modo de solo lectura. Nunca compartas credenciales.");

        Add("Working…", "处理中…", "處理中…", "処理中…", "처리 중…", "Procesando…");
        Add("Ready", "就绪", "就緒", "準備完了", "준비됨", "Listo");
        Add("Action failed", "操作失败", "操作失敗", "操作に失敗しました", "작업 실패", "La operación ha fallado");
        Add("Ledger changed · Refresh", "账本已更新 · 请刷新", "帳本已更新 · 請重新整理", "台帳が変更されました · 更新してください", "원장 변경됨 · 새로 고침", "Registro modificado · Actualizar");
        Add("Startup recovery completed", "启动恢复完成", "啟動復原完成", "起動時の復旧が完了しました", "시작 복구 완료", "Recuperación inicial completada", "启动恢复完成");
        Add("Event updates read", "已读取事件更新", "已讀取事件更新", "イベントの更新を読み取りました", "이벤트 업데이트 읽음", "Actualizaciones de eventos leídas", "已读取事件更新");
        Add("Refreshed", "已刷新", "已重新整理", "更新しました", "새로 고침 완료", "Actualizado", "已刷新");
        Add("done", "完成", "完成", "完了", "완료", "completado");

        Add("Select a notification.", "请选择通知。", "請選取通知。", "通知を選択してください。", "알림을 선택하세요.", "Selecciona una notificación.", "请选择通知。");
        Add("Select a task.", "请选择任务。", "請選取任務。", "タスクを選択してください。", "작업을 선택하세요.", "Selecciona una tarea.", "请选择任务。");
        Add("Select a migration proposal.", "请选择迁移提案。", "請選取遷移提案。", "移行提案を選択してください。", "마이그레이션 제안을 선택하세요.", "Selecciona una propuesta de migración.", "请选择迁移提案。");
        Add("Settings must be integers.", "设置必须为整数。", "設定必須為整數。", "設定には整数を入力してください。", "설정 값은 정수여야 합니다.", "Los valores de configuración deben ser enteros.", "设置必须为整数。");
        Add("Preview cleanup first.", "请先预览清理。", "請先預覽清理。", "先にクリーンアップをプレビューしてください。", "먼저 정리를 미리 확인하세요.", "Previsualiza la limpieza primero.", "请先 Preview Cleanup。");
        Add("Cleanup candidates changed. Preview again.", "清理候选已变化，请重新预览。", "清理候選已變更，請重新預覽。", "クリーンアップ候補が変更されました。再度プレビューしてください。", "정리 대상이 변경되었습니다. 다시 미리 확인하세요.", "Los candidatos de limpieza han cambiado. Vuelve a previsualizar.", "清理候选已变化，请重新预览。");
        Add("The file has expired or is missing.", "文件已过期或缺失。", "檔案已到期或遺失。", "ファイルの期限が切れているか、見つかりません。", "파일이 만료되었거나 없습니다.", "El archivo ha caducado o no se encuentra.", "文件已过期或缺失。");
        Add("The result is unregistered, expired, or missing. Check the task evidence.", "结果尚未登记、已过期或缺失，请检查任务证据。", "結果尚未登記、已到期或遺失，請檢查任務證據。", "結果が未登録、期限切れ、または見つかりません。タスクの証跡を確認してください。", "결과가 등록되지 않았거나 만료되었거나 없습니다. 작업 증거를 확인하세요.", "El resultado no está registrado, ha caducado o no se encuentra. Revisa la evidencia de la tarea.", "结果尚未登记、已过期或缺失，请检查任务证据。");
        Add("The registered paths are invalid. Check configuration, CLI, and instruction files.", "登记路径无效；请检查配置、CLI 与指令文件。", "登記路徑無效；請檢查設定、CLI 與指令檔案。", "登録パスが無効です。設定、CLI、指示ファイルを確認してください。", "등록된 경로가 유효하지 않습니다. 설정, CLI 및 지침 파일을 확인하세요.", "Las rutas registradas no son válidas. Revisa la configuración, la CLI y los archivos de instrucciones.", "登记路径无效；请检查配置、CLI 与指令文件。");

        Add("Empty fields keep the current value.", "空字段保留当前值。", "空白欄位保留目前值。", "空欄は現在の値を保持します。", "빈 필드는 현재 값을 유지합니다.", "Los campos vacíos conservan el valor actual.");
        Add("Only non-empty values are filled. Saving and approval are separate actions.", "只填入非空值。保存和批准需要分别操作。", "只填入非空值。儲存與核准須分別操作。", "空でない値のみ入力します。保存と承認は個別の操作です。", "비어 있지 않은 값만 채웁니다. 저장과 승인은 별도 작업입니다.", "Solo se rellenan valores no vacíos. Guardar y aprobar son acciones separadas.");
        Add("No editable fields in this section.", "此区域没有可编辑字段。", "此區域沒有可編輯欄位。", "このセクションに編集可能な項目はありません。", "이 영역에는 편집 가능한 필드가 없습니다.", "No hay campos editables en esta sección.");
        Add("Copy this field prompt", "复制此字段提示词", "複製此欄位提示詞", "この項目のプロンプトをコピー", "이 필드 프롬프트 복사", "Copiar prompt de este campo");
        Add("Appearance saved", "外观已保存", "外觀已儲存", "外観を保存しました", "모양 저장됨", "Apariencia guardada");
        Add("Follow system language", "跟随系统语言", "跟隨系統語言", "システムの言語を使用", "시스템 언어 사용", "Usar idioma del sistema");
        Add("The Agent reply format is invalid. Check version, context and requested fields.", "Agent 回复格式无效。请检查版本、上下文和所需字段。", "Agent 回覆格式無效。請檢查版本、上下文與所需欄位。", "Agentの返信形式が無効です。バージョン、コンテキスト、要求した項目を確認してください。", "Agent 응답 형식이 유효하지 않습니다. 버전, 컨텍스트 및 요청 필드를 확인하세요.", "El formato de respuesta del agente no es válido. Revisa la versión, el contexto y los campos solicitados.");
        Add("Please fill in {0}.", "请填写 {0}。", "請填寫 {0}。", "{0}を入力してください。", "{0}을(를) 입력하세요.", "Completa {0}.");

        Add("Awaiting confirmation", "等待确认", "等待確認", "確認待ち", "확인 대기", "Esperando confirmación", "AwaitingConfirmation");
        Add("Awaiting manual action", "等待人工操作", "等待人工操作", "手動操作待ち", "수동 작업 대기", "Esperando acción manual", "AwaitingManual");
        Add("Dispatching", "正在投递", "正在投遞", "配信中", "전달 중", "Enviando");
        Add("Dispatched", "已投递", "已投遞", "配信済み", "전달됨", "Enviado");
        Add("Acknowledged", "已确认接收", "已確認接收", "受領確認済み", "수신 확인됨", "Recepción confirmada");
        Add("Running", "运行中", "執行中", "実行中", "실행 중", "En ejecución");
        Add("Succeeded", "已成功", "已成功", "成功", "성공", "Completado");
        Add("Failed", "已失败", "已失敗", "失敗", "실패", "Fallido");
        Add("Timed out", "已超时", "已逾時", "タイムアウト", "시간 초과", "Tiempo agotado", "TimedOut");
        Add("Delivery unknown", "投递状态未知", "投遞狀態未知", "配信状態不明", "전달 상태 불명", "Entrega de estado desconocido", "DeliveryUnknown");
        Add("Cancelled", "已取消", "已取消", "キャンセル済み", "취소됨", "Cancelado");
        Add("Copy request prompt", "复制请求提示词", "複製請求提示詞", "リクエスト用プロンプトをコピー", "요청 프롬프트 복사", "Copiar prompt de solicitud", "copy_request_prompt");
        Add("Inspect delivery evidence", "检查投递证据", "檢查投遞證據", "配信の証跡を確認", "전달 증거 확인", "Revisar evidencia de entrega", "inspect_delivery_evidence");
        Add("Inspect or retry", "检查或重试", "檢查或重試", "確認または再試行", "확인 또는 재시도", "Revisar o reintentar", "inspect_or_retry");
        Add("Copy continuation prompt", "复制继续提示词", "複製繼續提示詞", "続行用プロンプトをコピー", "계속 프롬프트 복사", "Copiar prompt de continuación", "copy_continuation_prompt");
        Add("End turn and wait", "结束回合并等待", "結束回合並等待", "ターンを終了して待機", "턴 종료 후 대기", "Finalizar turno y esperar", "end_turn_and_wait");
        Add("Inspect execution evidence", "检查执行证据", "檢查執行證據", "実行の証跡を確認", "실행 증거 확인", "Revisar evidencia de ejecución", "inspect_execution_evidence");
        Add("Inspect", "检查", "檢查", "確認", "확인", "Revisar", "inspect");
        Add("Path verified", "路径已验证", "路徑已驗證", "パス検証済み", "경로 검증됨", "Ruta verificada", "path_verified");
        Add("Unverified", "未验证", "未驗證", "未検証", "검증되지 않음", "Sin verificar", "unverified");
        Add("Invalid", "无效", "無效", "無効", "유효하지 않음", "No válido", "invalid");
        Add("Manual", "手动", "手動", "手動", "수동", "Manual", "manual");
        Add("Manual + AMQ (verified)", "手动 + AMQ（已验证）", "手動 + AMQ（已驗證）", "手動 + AMQ（検証済み）", "수동 + AMQ(검증됨)", "Manual + AMQ (verificado)", "manual + AMQ (verified)");
        Add("Unavailable / Unverified", "不可用 / 未验证", "無法使用 / 未驗證", "利用不可 / 未検証", "사용 불가 / 검증되지 않음", "No disponible / Sin verificar", "unavailable / unverified");
        Add("Passed", "已通过", "已通過", "合格", "통과", "Aprobado", "passed");
        Add("Planned", "已生成提案", "已產生提案", "計画済み", "계획됨", "Planificado", "planned");
        Add("Applied", "已应用", "已套用", "適用済み", "적용됨", "Aplicado", "applied");
        Add("Validated", "已验证", "已驗證", "検証済み", "검증됨", "Validado", "validated");
        Add("Rolled back", "已回滚", "已還原", "ロールバック済み", "롤백됨", "Revertido", "rolled_back");
        Add("Install", "安装", "安裝", "インストール", "설치", "Instalar", "install");
        Add("Disable old entry", "停用旧入口", "停用舊入口", "旧エントリを無効化", "기존 진입점 비활성화", "Desactivar entrada anterior", "disable_old");
        entries.Add("approve", entries["Approve"]);
        entries.Add("recover", entries["Recover"]);
        entries.Add("allow", entries["Allow"]);
        entries.Add("deny", entries["Deny"]);
        entries.Add("default_deny", entries["Default deny"]);
        return entries;
    }
}
