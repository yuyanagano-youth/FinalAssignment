/**
 * ============================================================================
 * page-index.js
 * 【SCR-001】メイン操作画面 (Dispatch Console) のページロジック。
 * AmhsCore（amhs-core.js）の関数のみを呼び出し、fetchやモック分岐は
 * このファイルには一切書かない（決定ポイントは amhs-core.js に集約）。
 * ============================================================================
 */
(() => {
    const REFRESH_INTERVAL_MS = 3000; // 5.2「リアルタイム定期監視モジュール」仕様: 3,000ms間隔
    // ※ 仕様書 4.1 では「2000ms間隔」、5.2 では「3000ms間隔」と記載が分かれていたため、
    //    本実装ではコンソール側ハートビート周期(3秒)と揃えて一貫性を取っている。
    //    実運用前にバックエンド/コンソール担当者と最終的なポーリング間隔を確認すること。

    let pollTimerId = null;
    let activeStockers = [];      // F-005 Activeシールド適用後の一覧
    let allStockersCache = [];    // 全件キャッシュ（選択中ストッカーの状態参照用）

    const stockerSelect = document.getElementById("stockerSelect");
    const connBadge = document.getElementById("connBadge");
    const opBadge = document.getElementById("opBadge");
    const alertArea = document.getElementById("alertArea");
    const carrierIdInput = document.getElementById("carrierIdInput");
    const sourceSelect = document.getElementById("sourceSelect");
    const destSelect = document.getElementById("destSelect");
    const btnDispatch = document.getElementById("btnDispatch");
    const btnEStop = document.getElementById("btnEStop");
    const pollLogPanel = document.getElementById("pollLogPanel");
    const btnSimulateHeartbeat = document.getElementById("btnSimulateHeartbeat");

    function logPoll(text, isError) {
        const line = document.createElement("div");
        line.className = "log-line" + (isError ? " err" : "");
        const ts = new Date().toLocaleTimeString("ja-JP");
        line.textContent = `[${ts}] ${text}`;
        pollLogPanel.prepend(line);
        // ログが多くなりすぎないよう上限を設ける
        while (pollLogPanel.childNodes.length > 30) {
            pollLogPanel.removeChild(pollLogPanel.lastChild);
        }
    }

    function showAlert(message) {
        alertArea.textContent = message;
        alertArea.classList.add("show");
    }
    function clearAlert() {
        alertArea.textContent = "";
        alertArea.classList.remove("show");
    }

    function showToast(message) {
        const toastEl = document.getElementById("globalToast");
        document.getElementById("globalToastBody").textContent = message;
        const toast = bootstrap.Toast.getOrCreateInstance(toastEl, { delay: 2500 });
        toast.show();
    }

    /** ドロップダウン（Activeのみ・F-005/R-002）を構築する */
    function renderStockerOptions(stockers) {
        const currentSelection = stockerSelect.value;
        stockerSelect.innerHTML = "";
        stockers.forEach(s => {
            const opt = document.createElement("option");
            opt.value = s.stockerId;
            opt.textContent = `${s.stockerName} (${s.stockerId})`;
            stockerSelect.appendChild(opt);
        });
        // 可能であれば直前の選択を保持する
        if (stockers.some(s => s.stockerId === currentSelection)) {
            stockerSelect.value = currentSelection;
        }
    }

    /** Rule F-007（安全インターロック）の見た目反映 */
    function applyInterlock(stocker) {
        if (!stocker) {
            connBadge.textContent = "---";
            opBadge.textContent = "---";
            btnDispatch.disabled = true;
            return;
        }

        connBadge.textContent = stocker.connectionStatus;
        connBadge.className = "badge " + (stocker.connectionStatus === AmhsCore.Status.Connection.ONLINE ? "badge-online" : "badge-offline");

        opBadge.textContent = stocker.operationState;
        opBadge.className = "badge " + AmhsCore.operationBadgeClass(stocker.operationState);

        // F-007: OFFLINE または TRAVELING のときはディスパッチ無効化
        btnDispatch.disabled = AmhsCore.isDispatchDisabled(stocker);

        // alarms配列にデータがある場合はメイン画面にエラー表示＋クリアボタン相当の表示
        if (stocker.alarms && stocker.alarms.length > 0) {
            const msgs = stocker.alarms.map(a => `[${a.errorCode}] ${a.message}`).join(" / ");
            showAlert(msgs);
        }
    }

    /** 選択中ストッカーの最新状態を1件取得して画面に反映する（状態監視ループ本体） */
    async function refreshStockerStatus() {
        const res = await AmhsCore.getStockers();

        if (!res.ok) {
            logPoll(`GET /api/stockers 失敗 (status=${res.status})`, true);
            return;
        }

        allStockersCache = res.data || [];
        activeStockers = AmhsCore.filterActiveStockers(allStockersCache); // F-005

        if (activeStockers.length > 0 && !stockerSelect.value) {
            renderStockerOptions(activeStockers);
        } else if (stockerSelect.options.length === 0) {
            renderStockerOptions(activeStockers);
        }

        const selectedId = stockerSelect.value;
        const current = allStockersCache.find(s => s.stockerId === selectedId);
        applyInterlock(current);

        logPoll(`GET /api/stockers OK (${allStockersCache.length}件) 選択中:${selectedId || "-"} conn=${current?.connectionStatus} op=${current?.operationState}`);
    }

    function startPollingLoop() {
        if (pollTimerId) clearInterval(pollTimerId);
        pollTimerId = setInterval(refreshStockerStatus, REFRESH_INTERVAL_MS);
    }

    /** 搬送指示送信（4.1.1 DISPATCH送信モジュール） */
    async function handleDispatchClick() {
        const stockerId = stockerSelect.value;
        const carrierId = carrierIdInput.value.trim();
        const source = sourceSelect.value;
        const destination = destSelect.value;

        // 単体チェック：キャリアID未入力（UI-001）
        if (!carrierId) {
            showAlert("キャリアIDを入力してください");
            return;
        }
        // 状態チェック：表示中のoperationStateがIDLE以外なら処理を弾く
        const current = allStockersCache.find(s => s.stockerId === stockerId);
        if (AmhsCore.isDispatchDisabled(current)) {
            showAlert("装置がオフライン、または搬送中のため指示を送信できません");
            return;
        }

        btnDispatch.disabled = true;
        const res = await AmhsCore.postEquipmentCommand({
            command: AmhsCore.Status.Command.TRANSFER,
            stockerId,
            carrierId,
            source,
            destination
        });

        if (res.ok && res.data?.success) {
            clearAlert();
            showToast("ジョブの送信に成功しました");
            carrierIdInput.value = "";
            logPoll(`POST /api/equipment/command OK jobId=${res.data.jobId}`);
        } else {
            // HTTP 400: サーバーから返却されたmessageを赤文字で即時描画（F-003）
            const msg = res.data?.message || "サーバーエラーが発生しました";
            showAlert(`[${res.data?.errorCode || "ERR"}] ${msg}`);
            logPoll(`POST /api/equipment/command 失敗: ${msg}`, true);
        }

        await refreshStockerStatus();
    }

    /** E-STOP（即時割り込み） */
    async function handleEStopClick() {
        const stockerId = stockerSelect.value;
        if (!stockerId) return;
        if (!confirm("緊急停止コマンドを送信します。よろしいですか？")) return;

        const res = await AmhsCore.postEquipmentCommand({
            command: AmhsCore.Status.Command.ESTOP,
            stockerId
        });

        if (res.ok) {
            showToast("E-STOPコマンドを送信しました");
            logPoll(`POST /api/equipment/command (ESTOP) OK`);
        } else {
            showAlert(res.data?.message || "E-STOP送信に失敗しました");
        }
        await refreshStockerStatus();
    }

    /**
     * 【ポーリング通信テスト】
     * コンソールアプリ（C#）が3秒ごとに送るハートビートを疑似的に1回発行し、
     * 仮想JSONサーバーとの往復が正しく機能していることを確認するためのテスト関数。
     */
    async function simulateConsoleHeartbeatOnce() {
        const stockerId = stockerSelect.value;
        if (!stockerId) {
            logPoll("ハートビート送信スキップ: ストッカーが選択されていません", true);
            return;
        }
        const payload = { stockerId, currentOperationState: AmhsCore.Status.Operation.IDLE };
        logPoll(`POST /api/equipment/heartbeat 送信 -> ${JSON.stringify(payload)}`);

        const res = await AmhsCore.postHeartbeat(payload);

        if (res.ok) {
            logPoll(`POST /api/equipment/heartbeat 応答 <- hasPendingJob=${res.data.hasPendingJob}` +
                (res.data.hasPendingJob ? ` job=${JSON.stringify(res.data.job)}` : ""));
        } else {
            logPoll(`POST /api/equipment/heartbeat 失敗 (status=${res.status})`, true);
        }
        await refreshStockerStatus();
    }

    // ---- イベント登録 ----
    btnDispatch.addEventListener("click", handleDispatchClick);
    btnEStop.addEventListener("click", handleEStopClick);
    btnSimulateHeartbeat.addEventListener("click", simulateConsoleHeartbeatOnce);
    stockerSelect.addEventListener("change", () => {
        const current = allStockersCache.find(s => s.stockerId === stockerSelect.value);
        applyInterlock(current);
    });

    // ---- 初期ロード：DOMContentLoaded契機でrefreshStockerStatus()を即時実行 ----
    document.addEventListener("DOMContentLoaded", async () => {
        logPoll(`USE_MOCK_API = ${USE_MOCK_API} （${USE_MOCK_API ? "仮想JSONサーバーに接続中" : "実バックエンドに接続中"}）`);
        await refreshStockerStatus();
        startPollingLoop();
    });
})();
