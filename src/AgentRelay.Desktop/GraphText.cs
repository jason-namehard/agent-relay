namespace AgentRelay.Desktop;

internal static class GraphText
{
    public static string State(string display,string language)=>Legend(display,language);
    public static string Legend(string key,string language)
    {
        var index=language switch{"en"=>0,"zh-CN"=>1,"zh-TW"=>2,"ja"=>3,"ko"=>4,"es"=>5,_=>throw new ArgumentOutOfRangeException(nameof(language))};
        return Labels[key][index];
    }
    private static readonly IReadOnlyDictionary<string,string[]> Labels=new Dictionary<string,string[]>
    {
        ["cli-missing"]=["No CLI calling method registered.","尚未登记 CLI 调用方案。","尚未登記 CLI 呼叫方案。","CLI 呼び出し方法未登録。","CLI 호출 설정이 없습니다.","Sin método CLI registrado."],
        ["cli-failed"]=["CLI method registered; the latest probe failed. Inspect the test result.","已登记 CLI 调用方案，最近一次探针失败；请查看测试结果。","已登記 CLI 呼叫方案，最近一次探針失敗；請查看測試結果。","CLI 登録済み。直近のテスト失敗。","CLI 설정됨. 최근 테스트 실패.","CLI registrado; falló la última prueba."],
        ["cli-registered"]=["CLI method registered but not yet verified. You can send by hand; automatic turns start after a probe or one real handoff succeeds.","CLI 调用方案已登记，尚未验证。可手动发送；探针通过或一次真实交接成功后才会自动投递。","CLI 呼叫方案已登記，尚未驗證。可手動傳送；探針通過或一次真實交接成功後才會自動投遞。","CLI 登録済み・未検証。手動送信は可能で、プローブか実際の引き継ぎ 1 回の成功後に自動送信します。","CLI 등록됨, 미검증. 수동 전송은 가능하며 프로브나 실제 전달 1회 성공 후 자동 전송합니다.","CLI registrado sin verificar. Puedes enviar a mano; los turnos automáticos empiezan tras una prueba o un envío real correcto."],
        ["cli-ready"]=["CLI method verified and session visibility confirmed. Enable a direction to deliver files automatically.","CLI 调用已验证且会话可见性已确认；启用连接方向后可自动投递文件提示。","CLI 呼叫已驗證且工作階段可見性已確認；啟用連線方向後可自動投遞檔案提示。","CLI 検証・表示確認済み。方向を有効にすると自動配信。","CLI 검증 및 세션 확인됨. 연결 활성화 시 자동 전달.","CLI verificado y sesión confirmada; activa una dirección para la entrega automática."],
        ["node-cli-missing"]=["No bridge","无桥接","無橋接","ブリッジなし","브리지 없음","Sin puente"],
        ["node-cli-failed"]=["Last test failed","最近测试失败","最近測試失敗","直近のテスト失敗","최근 테스트 실패","Falló la última prueba"],
        ["node-cli-registered"]=["Not yet verified","尚未验证","尚未驗證","未検証","미검증","Sin verificar"],
        ["node-cli-ready"]=["Verified","已验证","已驗證","検証済み","검증됨","Verificado"],
        ["single"]=["One arrow: one-way calling recipe","单箭头：有单向调用方案","單箭頭：有單向呼叫方案","片矢印：一方向の呼び出し方法あり","단일 화살표: 단방향 호출 설정","Una flecha: método en un sentido"],
        ["double"]=["Two arrows: recipes in both directions","双箭头：有双向调用方案","雙箭頭：有雙向呼叫方案","両矢印：双方向の呼び出し方法あり","양쪽 화살표: 양방향 호출 설정","Dos flechas: métodos en ambos sentidos"],
        ["none"]=["Gray dashed: no recipe in either direction","灰色虚线：双方均无调用方案","灰色虛線：雙方均無呼叫方案","灰色破線：両方向とも呼び出し方法なし","회색 점선: 양방향 호출 설정 없음","Gris discontinua: sin método en ningún sentido"],
        ["colors"]=["Green: enabled · Red: paused or not enabled. Each arrow and its adjacent half-line show that direction's state. Click a line to manage both directions; manual copy remains available.","绿色：已启用；红色：已暂停或尚未启用。箭头及其相邻半段线表示该方向的状态。点击连线可分别管理两个方向，手动复制始终可用。","綠色：已啟用；紅色：已暫停或尚未啟用。箭頭及其相鄰半段線表示該方向的狀態。點擊連線可分別管理兩個方向，手動複製始終可用。","緑：有効、赤：一時停止または未有効。矢印と隣接する半分の線がその方向の状態を示します。線をクリックして両方向を個別に管理できます。手動コピーも利用できます。","초록: 활성, 빨강: 일시 중지 또는 미활성. 화살표와 인접한 선의 절반이 해당 방향 상태를 나타냅니다. 선을 클릭해 양방향을 각각 관리할 수 있으며 수동 복사는 항상 가능합니다.","Verde: activo; rojo: pausado o sin activar. Cada flecha y su mitad de línea indican el estado de ese sentido. Haz clic en la línea para gestionar ambos sentidos; la copia manual sigue disponible."],
        ["Allowed"]=["Enabled","已启用","已啟用","有効","사용 중","Activa"],
        ["Denied"]=["Paused or not enabled","已暂停或尚未启用","已暫停或尚未啟用","一時停止または未有効","일시 중지 또는 미활성","En pausa o sin activar"],
        ["Unavailable"]=["No calling recipe","缺少调用方案","缺少呼叫方案","呼び出し方法なし","호출 설정 없음","Sin método de llamada"]
    };
}
