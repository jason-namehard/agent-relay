namespace AgentRelay.Desktop;

internal static class DeliveryUiText
{
    public static void AddTo(Dictionary<string,string[]> entries)
    {
        void Add(string en,string cn,string tw,string ja,string ko,string es)=>entries[en]=[en,cn,tw,ja,ko,es];
        Add("Check Agent bridge","检查 Agent 桥接","檢查 Agent 橋接","Agent 接続を確認","Agent 브리지 확인","Revisar puente del Agent");
        Add("Sending to {0}…","正在发送给 {0}…","正在傳送給 {0}…","{0} に送信中…","{0}에 전송 중…","Enviando a {0}…");
        Add("Bridge blocked · {0}","桥接受阻 · {0}","橋接受阻 · {0}","接続がブロック · {0}","브리지 차단 · {0}","Puente bloqueado · {0}");
        Add("Delivery unconfirmed · {0}","投递未确认 · {0}","投遞未確認 · {0}","送信未確認 · {0}","전송 미확인 · {0}","Entrega sin confirmar · {0}");
        Add("Automatic delivery not verified · {0}","自动投递待验证 · {0}","自動投遞待驗證 · {0}","自動送信の検証待ち · {0}","자동 전송 검증 대기 · {0}","Envío automático sin verificar · {0}");
        Add("Paused · Bridge needs attention","已暂停 · 请检查桥接","已暫停 · 請檢查橋接","一時停止 · 接続を確認","일시 중지 · 브리지 확인 필요","Pausado · Revisa el puente");
        Add("This turn is already being sent. Wait for its result.","本轮正在发送，请等待结果，不要重复投递。","本輪正在傳送，請等待結果，不要重複投遞。","このターンは送信中です。結果をお待ちください。","이 차례는 전송 중입니다. 결과를 기다리세요.","Este turno ya se está enviando. Espera el resultado.");
        Add("This enables required routes that are paused or not yet authorized. It does not add a bridge or sign in to an Agent.","勾选后会启用本任务所需的已暂停或尚未授权方向；不会安装桥接，也不会替 Agent 登录。","勾選後會啟用本任務所需的已暫停或尚未授權方向；不會安裝橋接或替 Agent 登入。","必要な停止中・未承認の経路を有効にします。接続の追加やログインは行いません。","필요한 일시 중지·미승인 경로를 활성화합니다. 브리지 설치나 로그인은 하지 않습니다.","Activa rutas necesarias pausadas o sin autorizar. No instala puentes ni inicia sesión.");
    }
}
