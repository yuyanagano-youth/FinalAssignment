/**
 * ============================================================================
 * page-history.js
 * 【SCR-005】履歴ログ画面 (Event Log) のページロジック。
 * ============================================================================
 */
(() => {
    const logList = document.getElementById("logList");
    const btnRefresh = document.getElementById("btnRefreshLogs");

    function escapeHtml(str) {
        return String(str).replace(/[&<>"']/g, (c) => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
        }[c]));
    }

    function levelBadgeClass(level) {
        switch (level) {
            case AmhsCore.Status.LogLevel.INFO: return "text-bg-secondary";
            case AmhsCore.Status.LogLevel.WARN: return "text-bg-warning";
            case AmhsCore.Status.LogLevel.ALARM: return "text-bg-danger";
            default: return "text-bg-secondary";
        }
    }

    function formatTimestamp(iso) {
        const d = new Date(iso);
        return d.toLocaleString("ja-JP");
    }

    function renderLogs(logs) {
        logList.innerHTML = logs.map(l => `
            <div class="amhs-card">
                <div class="d-flex justify-content-between align-items-center">
                    <span class="badge ${levelBadgeClass(l.level)}">${escapeHtml(l.level)}</span>
                    <span class="small text-muted">${escapeHtml(formatTimestamp(l.timestamp))}</span>
                </div>
                <div class="mt-2">${escapeHtml(l.message)}</div>
            </div>
        `).join("") || `<p class="text-muted text-center mt-4">履歴ログがありません</p>`;
    }

    async function loadLogs() {
        // History画面はストッカー指定なし＝全件から最新10件を取得する想定
        const res = await AmhsCore.getRecentLogs();
        if (!res.ok) {
            logList.innerHTML = `<p class="text-danger text-center mt-4">状態取得失敗</p>`; // UI-009
            return;
        }
        renderLogs(res.data || []);
    }

    btnRefresh.addEventListener("click", loadLogs);
    document.addEventListener("DOMContentLoaded", loadLogs);
})();