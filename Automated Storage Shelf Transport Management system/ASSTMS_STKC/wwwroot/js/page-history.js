/**
 * ============================================================================
 * page-history.js
 * 【SCR-005】履歴ログ画面 (Event Log) のページロジック。
 * ============================================================================
 */
(() => {
    const logList = document.getElementById("logList");
    const btnRefresh = document.getElementById("btnRefreshLogs");
    const stockerSelect = document.getElementById("historyStockerSelect");

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

    /**
     * ドロップダウンを構築する。先頭に「すべて(ALL)」を追加し、
     * 続けて status==="Active" のストッカーを一覧表示する。
     */
    async function loadStockerOptions() {
        const res = await AmhsCore.getStockers();
        if (!res.ok) return;

        const activeOnly = AmhsCore.filterActiveStockers(res.data || []);
        const currentSelection = stockerSelect.value;

        const allOption = `<option value="ALL">すべて (ALL)</option>`;
        const stockerOptions = activeOnly.map(s =>
            `<option value="${escapeHtml(s.stockerId)}">${escapeHtml(s.stockerName)} (${escapeHtml(s.stockerId)})</option>`
        ).join("");

        stockerSelect.innerHTML = allOption + stockerOptions;

        // 直前の選択を保持（無ければ既定で "ALL" のまま）
        if (currentSelection && [...stockerSelect.options].some(o => o.value === currentSelection)) {
            stockerSelect.value = currentSelection;
        }
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
        const selected = stockerSelect.value;
        // "ALL" のときは stockerId を送らない（バックエンドのSQLが NULL=全件 を許容するため）
        const stockerId = selected === "ALL" ? null : selected;

        const res = await AmhsCore.getRecentLogs(stockerId);
        if (!res.ok) {
            logList.innerHTML = `<p class="text-danger text-center mt-4">状態取得失敗</p>`; // UI-009
            return;
        }
        renderLogs(res.data || []);
    }

    btnRefresh.addEventListener("click", loadLogs);
    stockerSelect.addEventListener("change", loadLogs);
    document.addEventListener("DOMContentLoaded", async () => {
        await loadStockerOptions();
        await loadLogs();
    });
})();