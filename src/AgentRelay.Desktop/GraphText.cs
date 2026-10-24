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
        ["single"]=["One arrow: one-way calling recipe","单箭头：有单向调用方案","單箭頭：有單向呼叫方案","片矢印：一方向の呼び出し方法あり","단일 화살표: 단방향 호출 설정","Una flecha: método en un sentido"],
        ["double"]=["Two arrows: recipes in both directions","双箭头：有双向调用方案","雙箭頭：有雙向呼叫方案","両矢印：双方向の呼び出し方法あり","양쪽 화살표: 양방향 호출 설정","Dos flechas: métodos en ambos sentidos"],
        ["none"]=["Gray dashed: no recipe in either direction","灰色虚线：双方均无调用方案","灰色虛線：雙方均無呼叫方案","灰色破線：両方向とも呼び出し方法なし","회색 점선: 양방향 호출 설정 없음","Gris discontinua: sin método en ningún sentido"],
        ["colors"]=["Green: enabled · Red: paused or not enabled. Each arrow and its adjacent half-line show that direction's state. Click a line to manage both directions; manual copy remains available.","绿色：已启用；红色：已暂停或尚未启用。箭头及其相邻半段线表示该方向的状态。点击连线可分别管理两个方向，手动复制始终可用。","綠色：已啟用；紅色：已暫停或尚未啟用。箭頭及其相鄰半段線表示該方向的狀態。點擊連線可分別管理兩個方向，手動複製始終可用。","緑：有効、赤：一時停止または未有効。矢印と隣接する半分の線がその方向の状態を示します。線をクリックして両方向を個別に管理できます。手動コピーも利用できます。","초록: 활성, 빨강: 일시 중지 또는 미활성. 화살표와 인접한 선의 절반이 해당 방향 상태를 나타냅니다. 선을 클릭해 양방향을 각각 관리할 수 있으며 수동 복사는 항상 가능합니다.","Verde: activo; rojo: pausado o sin activar. Cada flecha y su mitad de línea indican el estado de ese sentido. Haz clic en la línea para gestionar ambos sentidos; la copia manual sigue disponible."],
        ["Allowed"]=["Enabled","已启用","已啟用","有効","사용 중","Activa"],
        ["Denied"]=["Paused or not enabled","已暂停或尚未启用","已暫停或尚未啟用","一時停止または未有効","일시 중지 또는 미활성","En pausa o sin activar"],
        ["Unavailable"]=["No calling recipe","缺少调用方案","缺少呼叫方案","呼び出し方法なし","호출 설정 없음","Sin método de llamada"]
    };
}
