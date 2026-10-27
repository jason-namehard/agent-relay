namespace AgentRelay.Desktop;

internal static class ProbeVisibilityUiText
{
    public static void AddTo(Dictionary<string,string[]> entries)
    {
        void Add(string en,string cn,string tw,string ja,string ko,string es)=>entries[en]=[en,cn,tw,ja,ko,es];
        Add("Zcode may show CLI-created sessions and continued turns only after its client restarts. First try refreshing the session list or reopening the conversation. If it is still missing, save your work and restart Zcode. A stale view does not mean the call failed; do not rerun the probe just to refresh it.",
            "Zcode 的 CLI 新建会话及续接内容可能要重启客户端后才能显示。请先尝试刷新会话列表或重新打开该会话；仍看不到时，保存工作并重启 Zcode。界面未刷新不等于调用失败，不要为刷新显示而重复运行探针。",
            "Zcode 的 CLI 新建會話及續接內容可能要重啟用戶端後才會顯示。請先嘗試重新整理會話列表或重新開啟該會話；仍看不到時，儲存工作並重啟 Zcode。介面未更新不等於呼叫失敗，請勿為更新顯示而重跑探針。",
            "Zcode では CLI の新規セッションや続行内容がクライアント再起動まで表示されない場合があります。まず一覧の更新や会話の開き直しを試し、表示されなければ作業を保存して再起動してください。古い表示は呼び出し失敗を意味しません。表示更新のために再実行しないでください。",
            "Zcode는 CLI로 만든 세션과 이어진 내용을 클라이언트 재시작 후에만 표시할 수 있습니다. 먼저 목록을 새로 고치거나 대화를 다시 열고, 그래도 없으면 작업을 저장하고 Zcode를 재시작하세요. 오래된 화면은 호출 실패를 뜻하지 않으므로 새로 고치려고 탐침을 다시 실행하지 마세요.",
            "Zcode puede mostrar las sesiones creadas o continuadas por CLI solo tras reiniciar el cliente. Primero actualiza la lista o vuelve a abrir la conversación; si sigue sin aparecer, guarda el trabajo y reinicia Zcode. Una vista desactualizada no significa que falló la llamada; no repitas la prueba para actualizarla.");
        Add("The DSH desktop bridge runs inside the open DSH client, so new sessions and continued turns normally appear without restarting. Check the returned session ID and refresh or reopen that conversation if needed. Do not restart DSH while a turn is running.",
            "DSH 桌面桥接由已打开的 DSH 执行，新建会话和续接内容通常无需重启即可显示。请核对返回的会话 ID，必要时刷新或重新打开该会话。执行期间不要重启 DSH。",
            "DSH 桌面橋接由已開啟的 DSH 執行，新建會話及續接內容通常無需重啟即可顯示。請核對傳回的會話 ID，必要時重新整理或開啟該會話。執行期間請勿重啟 DSH。",
            "DSH デスクトップブリッジは起動中の DSH 内で実行するため、通常は再起動せずに新規セッションと続行内容が表示されます。返されたセッション ID を確認し、必要なら会話を更新・開き直してください。実行中は DSH を再起動しないでください。",
            "DSH 데스크톱 브리지는 열린 DSH 안에서 실행되므로 보통 재시작 없이 새 세션과 이어진 내용이 표시됩니다. 반환된 세션 ID를 확인하고 필요하면 대화를 새로 고치거나 다시 여세요. 실행 중에는 DSH를 재시작하지 마세요.",
            "El puente de escritorio ejecuta los turnos dentro de DSH abierto, por lo que normalmente aparecen sin reiniciar. Comprueba el ID devuelto y actualiza o vuelve a abrir esa conversación si hace falta. No reinicies DSH durante un turno.");
        Add("A successful machine receipt does not confirm that the host's desktop has refreshed. For new and continued sessions, refresh the session list or reopen the conversation; if needed, save your work and restart that client. A missing marker in a stale view is not a reason to run the probe again.",
            "机器回执成功不代表宿主界面已刷新。新建会话和续接内容均可先刷新列表或重新打开会话；必要时保存工作并重启该客户端。界面未刷新导致看不到标记时，不要重复运行探针。",
            "機器回執成功不代表宿主介面已更新。新建會話和續接內容均可先重新整理列表或開啟會話；必要時儲存工作並重啟用戶端。因介面未更新而看不到標記時，請勿重跑探針。",
            "マシン応答の成功はデスクトップ表示の更新を保証しません。新規・続行とも一覧を更新するか会話を開き直し、必要なら作業を保存してクライアントを再起動してください。古い表示にマーカーがなくても再実行しないでください。",
            "기계 응답 성공이 데스크톱 화면 갱신을 보장하지는 않습니다. 새 세션과 이어진 내용은 목록을 새로 고치거나 대화를 다시 열고, 필요하면 작업을 저장한 후 클라이언트를 재시작하세요. 오래된 화면에 표식이 없다는 이유로 탐침을 반복하지 마세요.",
            "Un recibo correcto no confirma que el escritorio se haya actualizado. Actualiza la lista o vuelve a abrir la conversación nueva o continuada; si hace falta, guarda el trabajo y reinicia el cliente. No repitas la prueba porque una vista desactualizada no muestre el marcador.");
        Add("The call succeeded. Confirm visibility only after you see this marker in the correct conversation. If the desktop has not refreshed, leave the confirmation unchecked; it is not a failed call.",
            "调用已成功。只有在正确会话里看到本次标记后才确认可见；宿主界面尚未刷新时，请保持未勾选，这不代表调用失败。",
            "呼叫已成功。只有在正確會話裡看到本次標記後才確認可見；宿主介面尚未更新時，請保持未勾選，這不代表呼叫失敗。",
            "呼び出しは成功しました。正しい会話で今回のマーカーを確認してから可視性を承認してください。表示が未更新なら未選択のままにしてください。呼び出し失敗ではありません。",
            "호출은 성공했습니다. 올바른 대화에서 이번 표식을 본 후에만 표시 여부를 확인하세요. 데스크톱이 아직 갱신되지 않았으면 선택하지 마세요. 호출 실패는 아닙니다.",
            "La llamada tuvo éxito. Confirma la visibilidad solo tras ver este marcador en la conversación correcta. Si el escritorio no se ha actualizado, deja la casilla sin marcar; la llamada no ha fallado.");
    }
}
