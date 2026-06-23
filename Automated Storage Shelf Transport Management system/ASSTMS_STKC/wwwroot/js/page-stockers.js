/**
 * ============================================================================
 * page-stockers.js
 * 【SCR-002】ストッカー一覧画面 (Equipment Registry) のページロジック。
 * ============================================================================
 */
(() => {
    const REFRESH_INTERVAL_MS = 3000;
    const listContainer = document.getElementById("stockerCardList");
    const btnRefresh = document.getElementById("btnRefreshStockers");
    let pollTimerId = null;

    function escapeHtml(str) {
        return String(str).replace(/[&<>"']/g, (c) => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
        }[c]));
    }

    function renderCards(stockers) {
        listContainer.innerHTML = stockers.map(s => {
            // R-004: OFFLINEの行はカード全体をグレーアウト
            const offlineClass = s.connectionStatus === AmhsCore.Status.Connection.OFFLINE ? "is-offline" : "";
            const connBadgeClass = s.connectionStatus === AmhsCore.Status.Connection.ONLINE ? "badge-online" : "badge-offline";
            const opBadgeClass = AmhsCore.operationBadgeClass(s.operationState);
            const statusBadge = s.status === AmhsCore.Status.Stocker.ACTIVE ? "text-bg-primary" : "text-bg-secondary";

            return `
                <div class="amhs-card stocker-card ${offlineClass}">
                    <div class="d-flex justify-content-between align-items-start">
                        <div>
                            <div class="fw-bold">${escapeHtml(s.stockerName)} <span class="text-muted small">(${escapeHtml(s.stockerId)})</span></div>
                            <span class="badge ${statusBadge} mt-1">${escapeHtml(s.status)}</span>
                        </div>
                        <div class="text-end">
                            <div><span class="badge ${connBadgeClass}">${escapeHtml(s.connectionStatus)}</span></div>
                            <div class="mt-1"><span class="badge ${opBadgeClass}">${escapeHtml(s.operationState)}</span></div>
                        </div>
                    </div>
                </div>
            `;
        }).join("") || `<p class="text-muted text-center mt-4">登録されたストッカーがありません</p>`;
    }

    async function loadStockers() {
        const res = await AmhsCore.getStockers();
        if (!res.ok) {
            listContainer.innerHTML = `<p class="text-danger text-center mt-4">状態取得失敗</p>`; // UI-009
            return;
        }
        renderCards(res.data || []);
    }

    function startPollingLoop() {
        if (pollTimerId) clearInterval(pollTimerId);
        pollTimerId = setInterval(loadStockers, REFRESH_INTERVAL_MS);
    }

    btnRefresh.addEventListener("click", loadStockers);
    document.addEventListener("DOMContentLoaded", async () => {
        await loadStockers();
        startPollingLoop();
    });
})();
