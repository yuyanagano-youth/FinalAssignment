/**
 * ============================================================================
 * page-shelves.js
 * 【SCR-003】棚・在庫画面 (Inventory Grid) のページロジック。
 * ============================================================================
 */
(() => {
    const REFRESH_INTERVAL_MS = 3000;
    const stockerSelect = document.getElementById("shelfStockerSelect");
    const shelfGrid = document.getElementById("shelfGrid");
    let pollTimerId = null;

    // R-003用：タップによるローカル表示切替の状態を保持する一時マップ。
    // 【注意】仕様書にはタップ操作に対応する書き込み系APIの定義が無いため、
    // ここでの色変更はあくまで画面上のデモ表示であり、サーバーには反映されない。
    // 実際にタップで棚状態を変更する運用にする場合は、バックエンド担当者と
    // 専用エンドポイント（例: PATCH /api/inventory/shelves/{shelfName}）の追加を相談すること。
    const localOverride = new Map();

    function escapeHtml(str) {
        return String(str).replace(/[&<>"']/g, (c) => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
        }[c]));
    }

    /** F-005 / R-002: status === "Active" のストッカーのみドロップダウンに表示 */
    async function loadActiveStockerOptions() {
        const res = await AmhsCore.getStockers();
        if (!res.ok) return;

        const activeOnly = AmhsCore.filterActiveStockers(res.data || []);
        const currentSelection = stockerSelect.value;

        stockerSelect.innerHTML = activeOnly.map(s =>
            `<option value="${escapeHtml(s.stockerId)}">${escapeHtml(s.stockerName)} (${escapeHtml(s.stockerId)})</option>`
        ).join("");

        if (activeOnly.some(s => s.stockerId === currentSelection)) {
            stockerSelect.value = currentSelection;
        }
    }

    /** 棚セルの状態区分（空き/占有中/Alarm）を判定する */
    function classifyShelf(shelf) {
        const overrideState = localOverride.get(shelf.shelfName);
        if (overrideState) return overrideState;
        return shelf.carrierId ? "occupied" : "empty";
    }

    function renderGrid(shelves) {
        shelfGrid.innerHTML = shelves.map(s => {
            const state = classifyShelf(s);
            const label = s.carrierId ? escapeHtml(s.carrierId) : "EMPTY";
            return `
                <div class="shelf-cell ${state}" data-shelf-name="${escapeHtml(s.shelfName)}">
                    <div class="shelf-name">${escapeHtml(s.shelfName)}</div>
                    <div>${label}</div>
                </div>
            `;
        }).join("") || `<p class="text-muted text-center mt-4">棚データがありません</p>`;

        // R-003: 各マスをタップすると色（状態）を変更できる
        shelfGrid.querySelectorAll(".shelf-cell").forEach(cell => {
            cell.addEventListener("click", () => handleCellTap(cell.dataset.shelfName));
        });
    }

    function handleCellTap(shelfName) {
        const cycle = ["empty", "occupied", "alarm"];
        const current = localOverride.get(shelfName) || null;
        const idx = current ? cycle.indexOf(current) : -1;
        const next = cycle[(idx + 1) % cycle.length];
        localOverride.set(shelfName, next);

        const cell = shelfGrid.querySelector(`[data-shelf-name="${shelfName}"]`);
        if (cell) {
            cell.classList.remove("empty", "occupied", "alarm");
            cell.classList.add(next);
        }
    }

    async function loadShelves() {
        const stockerId = stockerSelect.value;
        if (!stockerId) {
            shelfGrid.innerHTML = `<p class="text-muted text-center mt-4">ストッカーを選択してください</p>`;
            return;
        }
        const res = await AmhsCore.getInventoryShelves(stockerId);
        if (!res.ok) {
            shelfGrid.innerHTML = `<p class="text-danger text-center mt-4">状態取得失敗</p>`; // UI-009
            return;
        }
        renderGrid(res.data || []);
    }

    function startPollingLoop() {
        if (pollTimerId) clearInterval(pollTimerId);
        pollTimerId = setInterval(loadShelves, REFRESH_INTERVAL_MS);
    }

    stockerSelect.addEventListener("change", () => {
        localOverride.clear(); // ストッカー切替時はローカル上書き状態をリセット
        loadShelves();
    });

    document.addEventListener("DOMContentLoaded", async () => {
        await loadActiveStockerOptions();
        await loadShelves();
        startPollingLoop();
    });
})();
