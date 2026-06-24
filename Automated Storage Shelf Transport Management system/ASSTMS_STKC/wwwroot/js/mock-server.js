/**
 * ============================================================================
 * mock-server.js
 * 【virtual JSON server / 仮想バックエンドサーバー】
 * ----------------------------------------------------------------------------
 * バックエンド(SQL/API)担当者・コンソールアプリ担当者の実装が未完成の段階でも、
 * フロントエンド単体で全画面の動作確認ができるようにするための「仮のサーバー」。
 *
 * json-server のような振る舞い（インメモリDB + ルーティング + 遅延応答）を
 * 素のJavaScriptで再現しており、amhs-core.js の USE_MOCK_API = true のときだけ
 * このモジュールが呼び出される。
 *
 * 【重要】本ファイルは開発・デモ用であり、本番ビルドには含めない、または
 * USE_MOCK_API = false の状態で配信すること。
 * ============================================================================
 */
const MockServer = (() => {

    // ============================================================================
    // 【ページ間でのモックDB永続化】
    // Razor Pagesはタブ切替ごとに完全なページ遷移（フルリロード）が発生するため、
    // 通常のJS変数(db)は遷移時にリセットされてしまう。
    // そのため sessionStorage を使い、タブ間・ページ遷移間でモックデータを保持する。
    // ブラウザタブを閉じる、またはハードリフレッシュ(Ctrl+Shift+R)すると初期データに戻る。
    // ============================================================================
    const STORAGE_KEY = "amhsMockDb";

    // ---- 仮想DB（インメモリ）：設計仕様書のレスポンスサンプルに準拠（初期シードデータ） ----
    const seedDb = {
        stockers: [
            {
                stockerId: "STK001",
                stockerName: "STK1",
                status: "Active",
                connectionStatus: "ONLINE",
                operationState: "IDLE",
                alarms: []
            },
            {
                stockerId: "STK002",
                stockerName: "STK2",
                status: "Reserved",
                connectionStatus: "OFFLINE",
                operationState: "UNKNOWN",
                alarms: [{ errorCode: "ERR-003", message: "出庫時ソース空異常" }]
            },
            {
                stockerId: "STK003",
                stockerName: "STK3",
                status: "Active",
                connectionStatus: "ONLINE",
                operationState: "TRAVELING",
                alarms: []
            },
            {
                stockerId: "STK004",
                stockerName: "STK4",
                status: "Active",
                connectionStatus: "ONLINE",
                operationState: "IDLE",
                alarms: []
            }
        ],

        shelves: {
            STK001: [
                { shelfName: "IN_PORT", carrierId: null, inTime: null },
                { shelfName: "SHELF_A1", carrierId: "CST-1001", inTime: "2026-06-16T10:00:00Z" },
                { shelfName: "SHELF_A2", carrierId: null, inTime: null },
                { shelfName: "SHELF_A3", carrierId: null, inTime: null },
                { shelfName: "OUT_PORT", carrierId: "CST-1005", inTime: "2026-06-16T11:20:00Z" }
            ],
            STK004: [
                { shelfName: "IN_PORT", carrierId: null, inTime: null },
                { shelfName: "SHELF_A1", carrierId: "CST-1004", inTime: "2026-06-16T10:00:00Z" },
                { shelfName: "SHELF_A11", carrierId: "CST-1005", inTime: "2026-06-16T10:00:00Z" },
                { shelfName: "SHELF_A2", carrierId: null, inTime: null },
                { shelfName: "SHELF_A12", carrierId: "CST-10012", inTime: "2026-06-17T10:00:00Z" },
                { shelfName: "SHELF_A3", carrierId: null, inTime: null },
                { shelfName: "SHELF_A4", carrierId: null, inTime: null },
                { shelfName: "SHELF_A9", carrierId: null, inTime: null },
                { shelfName: "SHELF_A10", carrierId: null, inTime: null },
                { shelfName: "OUT_PORT", carrierId: "CST-1006", inTime: "2026-06-16T11:20:00Z" },
                { shelfName: "OUT_PORT", carrierId: "CST-1008", inTime: "2026-06-16T11:20:00Z" },
                { shelfName: "OUT_PORT", carrierId: "CST-1015", inTime: "2026-06-16T11:20:00Z" },

                { shelfName: "SHELF_A5", carrierId: "CST-1007", inTime: "2026-06-16T11:20:00Z" }
            ],
            STK003: [
                { shelfName: "IN_PORT", carrierId: null, inTime: null },
                { shelfName: "SHELF_B1", carrierId: "CST-2002", inTime: "2026-06-22T01:00:00Z" },
                { shelfName: "SHELF_B2", carrierId: null, inTime: null },
                { shelfName: "OUT_PORT", carrierId: null, inTime: null }
            ]
        },

        jobs: [
            { jobId: "JOB1001", stockerId: "STK001", carrierId: "CST-1001", source: "IN_PORT", destination: "SHELF_A1", status: "RUNNING" },
            { jobId: "JOB1002", stockerId: "STK003", carrierId: "CST-2002", source: "IN_PORT", destination: "SHELF_B1", status: "PENDING" }
        ],

        logs: [
            { timestamp: "2026-06-22T05:15:00Z", level: "INFO", message: "搬送動作完了 (TransferCompleted)", stockerId: "STK001" },
            { timestamp: "2026-06-22T04:05:00Z", level: "ALARM", message: "出庫時ソース空異常 (ERR-003)", stockerId: "STK002" },
            { timestamp: "2026-06-22T03:40:00Z", level: "WARN", message: "通信遅延を検出しました", stockerId: "STK003" },
            { timestamp: "2026-06-21T23:00:00Z", level: "INFO", message: "ストッカー起動シーケンス完了", stockerId: "STK001" }
        ]
    };

    /**
     * sessionStorageから前回の状態を復元する。
     * 保存データが無い（＝このタブで初回アクセス）場合はseedDbをそのまま使う。
     */
    function loadDb() {
        try {
            const raw = sessionStorage.getItem(STORAGE_KEY);
            if (raw) return JSON.parse(raw);
        } catch (e) {
            console.warn("[MockServer] sessionStorageの読み込みに失敗。初期データを使用します。", e);
        }
        return JSON.parse(JSON.stringify(seedDb)); // seedDbをディープコピーして使用
    }

    /** 変更後のdbをsessionStorageへ保存する（ページ遷移後も状態を維持するため） */
    function persist() {
        try {
            sessionStorage.setItem(STORAGE_KEY, JSON.stringify(db));
        } catch (e) {
            console.warn("[MockServer] sessionStorageへの保存に失敗しました。", e);
        }
    }

    const db = loadDb();

    // 採番カウンタもジョブ件数から復元し、ページ遷移後もIDの重複を防ぐ
    let jobSeq = 1003 + db.jobs.filter(j => j.jobId.startsWith("JOB" + new Date().getFullYear())).length;

    // ---- ネットワーク遅延のシミュレーション（実通信らしさを再現） ----
    const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
    const randomLatency = () => 150 + Math.floor(Math.random() * 250); // 150〜400ms

    /**
     * ルーティング本体。method + path + (body) を受け取り、
     * 本物のfetch Responseに似せた { ok, status, json() } を返す。
     */
    async function handle(method, path, body) {
        await wait(randomLatency());

        const url = new URL(path, "https://mock.local");
        const segments = url.pathname.split("/").filter(Boolean); // ["api","stockers"] 等

        // ---- GET /api/front/stockers ----
        if (method === "GET" && url.pathname === "/api/front/stockers") {
            return ok(db.stockers);
        }

        // ---- GET /api/jobs/active?stockerId=... ----
        if (method === "GET" && url.pathname === "/api/jobs/active") {
            const stockerId = url.searchParams.get("stockerId");
            const list = db.jobs.filter(j =>
                (j.status === "PENDING" || j.status === "RUNNING") &&
                (!stockerId || j.stockerId === stockerId)
            );
            return ok(list);
        }

        // ---- DELETE /api/jobs/{jobId} ----
        if (method === "DELETE" && segments[0] === "api" && segments[1] === "jobs" && segments[2]) {
            const jobId = segments[2];
            const idx = db.jobs.findIndex(j => j.jobId === jobId);
            if (idx === -1) {
                return notFound({ success: false, message: "ジョブ削除に失敗しました" }); // UI-008
            }
            db.jobs.splice(idx, 1);
            persist();
            return ok({ success: true });
        }

        // ---- GET /api/inventory/shelves?stockerId=... ----
        if (method === "GET" && url.pathname === "/api/inventory/shelves") {
            const stockerId = url.searchParams.get("stockerId");
            return ok(db.shelves[stockerId] || []);
        }

        // ---- GET /api/logs/recent?stockerId=...  (互換: /api/History/) ----
        if (method === "GET" && (url.pathname === "/api/logs/recent" || url.pathname === "/api/History")) {
            const stockerId = url.searchParams.get("stockerId");
            let list = db.logs.slice();
            if (stockerId) list = list.filter(l => l.stockerId === stockerId);
            list = list.sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp)).slice(0, 10);
            return ok(list);
        }

        // ---- POST /api/equipment/command ----
        if (method === "POST" && url.pathname === "/api/equipment/command") {
            return handleEquipmentCommand(body);
        }

        // ---- POST /api/equipment/heartbeat（コンソールアプリ側のポーリング模擬用） ----
        if (method === "POST" && (url.pathname === "/api/equipment/heartbeat" || url.pathname === "/api/equipment/Polling")) {
            return handleHeartbeat(body);
        }

        return notFound({ success: false, message: `Mock route not found: ${method} ${path}` });
    }

    function handleEquipmentCommand(body) {
        const stocker = db.stockers.find(s => s.stockerId === body.stockerId);

        if (!stocker) {
            return badRequest({ success: false, jobId: null, errorCode: "ERR-404", message: "指定されたストッカーが見つかりません" });
        }

        // ---- ESTOP: 即時割り込み ----
        if (body.command === "ESTOP") {
            stocker.operationState = "ALARM";
            db.jobs.forEach(j => {
                if (j.stockerId === body.stockerId && (j.status === "PENDING" || j.status === "RUNNING")) {
                    j.status = "ABORTED";
                }
            });
            db.logs.unshift({ timestamp: new Date().toISOString(), level: "ALARM", message: "緊急停止(E-STOP)が実行されました", stockerId: body.stockerId });
            persist();
            return ok({ success: true, jobId: null, message: "E-STOP command executed." });
        }

        // ---- TRANSFER: F-003 バリデーション ----
        if (body.command === "TRANSFER") {
            if (stocker.connectionStatus === "OFFLINE") {
                return badRequest({ success: false, jobId: null, errorCode: "ERR-001", message: "装置がオフラインです" }); // UI-004
            }

            const duplicate = db.jobs.some(j =>
                j.carrierId === body.carrierId && (j.status === "PENDING" || j.status === "RUNNING")
            );
            if (duplicate) {
                return badRequest({ success: false, jobId: null, errorCode: "ERR-002", message: "Job creation failed: Carrier ID already in an active job (重複エラー)." });
            }

            const shelvesForStocker = db.shelves[body.stockerId] || [];
            const destShelf = shelvesForStocker.find(s => s.shelfName === body.destination);
            if (destShelf && destShelf.carrierId) {
                return badRequest({ success: false, jobId: null, errorCode: "ERR-002", message: "Job creation failed: Target shelf is already occupied (二重保管エラー)." });
            }

            const sourceShelf = shelvesForStocker.find(s => s.shelfName === body.source);
            if (sourceShelf && body.source !== "IN_PORT" && !sourceShelf.carrierId) {
                return badRequest({ success: false, jobId: null, errorCode: "ERR-003", message: "出庫時ソース空異常: 指定された搬送元にキャリアが存在しません" });
            }

            const jobId = `JOB${new Date().getFullYear()}${String(jobSeq++).padStart(6, "0")}`;
            db.jobs.push({ jobId, stockerId: body.stockerId, carrierId: body.carrierId, source: body.source, destination: body.destination, status: "PENDING" });
            // Historyタブでも確認できるよう、ジョブ生成イベントをログにも記録する
            db.logs.unshift({ timestamp: new Date().toISOString(), level: "INFO", message: `搬送ジョブを登録しました (${jobId}: ${body.source} → ${body.destination})`, stockerId: body.stockerId });
            persist();
            return ok({ success: true, jobId, message: "Job successfully queued." });
        }

        return badRequest({ success: false, jobId: null, errorCode: "ERR-400", message: "不明なコマンドです" });
    }

    /**
     * ハートビート/ポーリングのシミュレーション。
     * 本来は C# コンソールアプリが3秒ごとに叩くエンドポイントだが、
     * 「ポーリング通信テスト」用に index ページのデバッグパネルからも
     * 手動で擬似呼び出しできるようにしている。
     */
    function handleHeartbeat(body) {
        const stocker = db.stockers.find(s => s.stockerId === body.stockerId);
        if (stocker) {
            stocker.connectionStatus = "ONLINE";
            stocker.operationState = body.currentOperationState || stocker.operationState;
        }
        persist();

        const pending = db.jobs.find(j => j.stockerId === body.stockerId && j.status === "PENDING");
        if (!pending) {
            return ok({ hasPendingJob: false });
        }

        return ok({
            hasPendingJob: true,
            job: {
                jobId: pending.jobId,
                command: "TRANSFER",
                carrierId: pending.carrierId,
                source: pending.source,
                destination: pending.destination
            }
        });
    }

    // ---- fetch Response 風オブジェクトを生成するヘルパー ----
    function ok(data) { return { ok: true, status: 200, json: async () => data }; }
    function badRequest(data) { return { ok: false, status: 400, json: async () => data }; }
    function notFound(data) { return { ok: false, status: 404, json: async () => data }; }

    /**
     * 【テスト用】モックDBを初期シードデータへ強制リセットする。
     * ブラウザのコンソールから MockServer.resetToSeed() を実行すれば、
     * ページ再読込なしでテストデータをクリーンな状態に戻せる。
     */
    function resetToSeed() {
        sessionStorage.removeItem(STORAGE_KEY);
        Object.assign(db, JSON.parse(JSON.stringify(seedDb)));
        persist();
        console.log("[MockServer] モックDBを初期シードデータへリセットしました。");
    }

    return { handle, resetToSeed };
})();
