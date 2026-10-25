namespace AgentRelay.Desktop;

internal static class AgentEntryUiText
{
    public static void AddTo(Dictionary<string, string[]> entries)
    {
        void Add(string en, string cn, string tw, string ja, string ko, string es) => entries.Add(en, [en, cn, tw, ja, ko, es]);
        Add("New Agent", "新建 Agent", "新增 Agent", "Agent を作成", "Agent 만들기", "Nuevo agente");
        Add("Delete Agent", "删除 Agent", "刪除 Agent", "Agent を削除", "Agent 삭제", "Eliminar agente");
        Add("Agent CLI test", "Agent CLI 测试", "Agent CLI 測試", "Agent CLI テスト", "Agent CLI 테스트", "Probar CLI del agente");
        Add("One-click setup", "一键配置", "一鍵設定", "ワンクリック設定", "원클릭 설정", "Configuración con un clic");
        Add("Use one name per Agent. Configure it directly with a prompt, or enter the fields yourself.",
            "每个 Agent 只需一个名称。用一键配置让 Agent 直接登记，也可按字段手动填写。", "每個 Agent 只需一個名稱。可一鍵設定直接登記，或按欄位手動填寫。",
            "Agent ごとに名前は一つです。ワンクリック設定か手動入力で登録します。", "Agent마다 이름 하나만 사용합니다. 원클릭 설정이나 직접 입력으로 등록하세요.", "Un nombre por agente. Regístralo con un prompt o introduce los campos manualmente.");
        Add("Setup prompt copied. Paste it into a new conversation with the Agent you want to register. It will inspect its calling method and fill Relay directly. This creates a new node and does not overwrite the selected Agent.",
            "配置提示词已复制。请粘贴到想要登记的 Agent 的新对话窗口，它会调查调用方式并直接配置 Relay。本次新建节点，不会覆盖当前选中的 Agent。",
            "設定提示詞已複製。請貼到要登記的 Agent 的新對話視窗，它會調查呼叫方式並直接設定 Relay。本次新增節點，不會覆蓋目前選取的 Agent。",
            "設定プロンプトをコピーしました。登録する Agent の新しい会話に貼り付けてください。呼び出し方法を調べて Relay に直接登録します。選択中の Agent は上書きしません。",
            "설정 프롬프트를 복사했습니다. 등록할 Agent의 새 대화에 붙여 넣으세요. 호출 방법을 조사해 Relay에 직접 등록합니다. 선택한 Agent를 덮어쓰지 않습니다.",
            "Prompt copiado. Pégalo en una conversación nueva con el agente que quieras registrar. Investigará su método de llamada y configurará Relay directamente. No sobrescribe el agente seleccionado.");
        Add("One-click setup is for a new node. To change an existing node, select it and use Edit Agent. A configured CLI is ready to test; it is not a failed connection.",
            "一键配置用于新建节点；修改已有节点请选中后编辑。已配置 CLI 表示可以发起测试，不代表测试失败。", "一鍵設定用於新增節點；修改既有節點請選取後編輯。已設定 CLI 表示可發起測試，不代表測試失敗。",
            "ワンクリック設定は新規登録用です。既存の Agent は選択して編集してください。CLI 設定済みはテスト可能を意味します。", "원클릭 설정은 새 노드용입니다. 기존 노드는 선택 후 편집하세요. CLI 설정 완료는 테스트 준비 상태입니다.", "La configuración con un clic crea nodos nuevos. Para cambiar uno, selecciónalo y edítalo. Una CLI configurada está lista para probar.");
        Add("Handoff rules and node details", "交接规则与节点详情", "交接規則與節點詳情", "引き継ぎルールとノード詳細", "전달 규칙 및 노드 정보", "Reglas de envío y detalles del nodo");
        Add("CLI configured · Ready to test", "CLI 已配置 · 待测试", "CLI 已設定 · 待測試", "CLI 設定済み · テスト可能", "CLI 설정됨 · 테스트 준비", "CLI configurada · Lista para probar");
        Add("CLI not configured", "CLI 未配置", "CLI 未設定", "CLI 未設定", "CLI 미설정", "CLI sin configurar");
        Add("CLI program recorded · Add calling arguments", "已记录 CLI 程序 · 待补调用参数", "已記錄 CLI 程式 · 待補呼叫參數", "CLI 登録済み · 引数が必要", "CLI 프로그램 기록됨 · 인수 필요", "Programa CLI registrado · Faltan argumentos");
        Add("CLI probe passed · Confirm visibility", "CLI 探针通过 · 待确认会话可见", "CLI 探針通過 · 待確認會話可見", "CLI 応答確認済み · 会話表示を確認", "CLI 프로브 통과 · 세션 표시 확인 필요", "Sonda CLI aprobada · Confirma visibilidad");
        Add("CLI probe failed · Retry available", "CLI 探针失败 · 可重试", "CLI 探針失敗 · 可重試", "CLI テスト失敗 · 再試行可能", "CLI 프로브 실패 · 재시도 가능", "Sonda CLI fallida · Puedes reintentar");
        Add("CLI calling method (optional)", "CLI 调用方式（选填）", "CLI 呼叫方式（選填）", "CLI 呼び出し方法（任意）", "CLI 호출 방법 (선택)", "Método de llamada CLI (opcional)");
        Add("Record a program alone, or add arguments containing {prompt} to save a testable calling method. Put one argument on each line.",
            "可以仅记录程序；要发起测试，还需填写包含 {prompt} 的参数或标准输入模板。参数每行一个。", "可以僅記錄程式；要測試還需填寫含 {prompt} 的參數或標準輸入模板。參數每行一個。",
            "実行ファイルのみの登録も可能です。テストには {prompt} を含む引数または標準入力が必要です。引数は1行に1つ記入します。", "프로그램만 기록할 수 있습니다. 테스트하려면 {prompt}가 포함된 인수나 표준 입력을 추가하세요. 인수는 줄마다 하나씩 입력하세요.", "Puedes registrar solo el programa. Para probarlo, añade argumentos o entrada estándar con {prompt}. Un argumento por línea.");
        Add("CLI arguments (one per line)", "CLI 调用参数（每行一个）", "CLI 呼叫參數（每行一個）", "CLI 引数（1行に1つ）", "CLI 인수 (줄당 하나)", "Argumentos CLI (uno por línea)");
        Add("Standard input template (optional)", "标准输入模板（选填）", "標準輸入模板（選填）", "標準入力テンプレート（任意）", "표준 입력 템플릿 (선택)", "Plantilla de entrada estándar (opcional)");
        Add("CLI output format", "CLI 输出格式", "CLI 輸出格式", "CLI 出力形式", "CLI 출력 형식", "Formato de salida CLI");
        Add("CLI working directory", "CLI 工作目录（选填）", "CLI 工作目錄（選填）", "CLI 作業ディレクトリ", "CLI 작업 디렉터리", "Directorio de trabajo CLI");
        Add("CLI version (optional)", "CLI 版本（选填）", "CLI 版本（選填）", "CLI バージョン（任意）", "CLI 버전 (선택)", "Versión CLI (opcional)");
        Add("Enter a CLI program before its arguments.", "请先填写 CLI 程序路径，再填写参数。", "請先填寫 CLI 程式路徑，再填寫參數。", "引数の前に CLI の実行ファイルを指定してください。", "인수보다 먼저 CLI 프로그램을 입력하세요.", "Introduce un programa CLI antes de sus argumentos.");
        Add("CLI arguments must include {prompt}.", "调用参数或标准输入模板必须包含 {prompt}。", "呼叫參數或標準輸入模板必須包含 {prompt}。", "引数または標準入力には {prompt} が必要です。", "인수 또는 표준 입력에 {prompt}가 필요합니다.", "Los argumentos o la entrada estándar deben incluir {prompt}.");
        Add("A PATH command needs calling arguments; to record only a program, use its full path.", "使用 PATH 命令时请补充调用参数；如仅记录程序，请使用完整路径。", "使用 PATH 命令時請補充呼叫參數；如僅記錄程式，請使用完整路徑。", "PATH コマンドには引数を指定してください。実行ファイルのみ記録する場合は絶対パスを使用します。", "PATH 명령에는 호출 인수가 필요합니다. 프로그램만 기록하려면 전체 경로를 사용하세요.", "Un comando PATH necesita argumentos. Para registrar solo el programa, usa su ruta absoluta.");
    }
}
