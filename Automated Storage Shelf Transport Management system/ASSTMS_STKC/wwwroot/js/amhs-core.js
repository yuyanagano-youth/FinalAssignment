/**
 * ============================================================================
 * amhs-core.js
 * 【決定モジュール：バックエンド連携コア】
 * ----------------------------------------------------------------------------
 * 仕様書の要求どおり、すべてのサーバー通信（fetch）をこのファイルに集約する。
 * Razor PageModel(C#)側からはDBクエリを直接書かず、ブラウザ側でこのモジュールの
 * 関数を呼び出す構成とする（フロントエンド／バックエンドの完全分離）。
 * ============================================================================
 */

// ============================================================================
// 【統合設定フラグ / Integration Configuration Flag】
// true  : モック(virtual JSON server = mock-server.js)が応答する。
//         バックエンド担当者・コンソールアプリ担当者の実装を待たずに
//         フロントエンド単体でフル機能のテストが可能。
// false : 実際の ASP.NET Core API (同一オリジン / localhost) を呼び出す。
//         結合テスト・本番リリース時にこの値を false へ切り替えるだけでよい。
// ============================================================================
const USE_MOCK_API = false;

// 実APIのベースURL。リバースプロキシ等で配信パスが変わる場合のみ変更する。
const API_BASE = "";

const AmhsCore = (() => {

    /**
     * 共通fetchラッパー。
     * USE_MOCK_API が true の場合は MockServer（仮想JSONサーバー）に処理を委譲し、
     * false の場合のみ実際の HTTP リクエストを発行する。
     *
     * @param {string} method  HTTPメソッド ("GET","POST","DELETE"等)
     * @param {string} path    APIパス (例: "/api/stockers")
     * @param {object} [body]  POST時のリクエストボディ
     */
    async function request(method, path, body) {
        if (USE_MOCK_API) {
            // ---- モック経路：仮想JSONサーバーに委譲 ----
            const res = await MockServer.handle(method, path, body);
            return finalize(res);
        }

        // ====================================================================
        // ▼▼▼ 【実API接続ポイント】 ここが本番バックエンドへの実通信箇所 ▼▼▼
        // バックエンド(SQL/API)担当者が実装したASP.NET Core APIに対し、
        // 標準のfetch()でHTTPリクエストを送信する。
        // USE_MOCK_API = false に切り替えるだけで、このブロックが有効化される。
        // ====================================================================
        const res = await fetch(`${API_BASE}${path}`, {
            method,
            headers: body ? { "Content-Type": "application/json" } : undefined,
            body: body ? JSON.stringify(body) : undefined
        });
        // ▲▲▲ 実API接続ポイントはここまで ▲▲▲

        return finalize(res);
    }

    /** fetch() / MockServer 両方の戻り値を統一形式に変換する */
    async function finalize(res) {
        let data = null;
        try {
            data = await res.json();
        } catch (_) {
            // ボディなし(204等)は無視
        }
        return { ok: res.ok, status: res.status, data };
    }

    // ------------------------------------------------------------------------
    // 【公開API：各エンドポイントに1対1対応する関数】
    // PageごとのJS（page-*.js）はこれらの関数のみを呼び出すこと。
    // ------------------------------------------------------------------------

    /** GET /api/stockers - 接続・稼働状態を含む全ストッカー取得 (F-001, F-009) */
    async function getStockers() {
        return request("GET", "/api/stockers");
    }

    /** GET /api/jobs/active?stockerId=... - アクティブジョブ一覧取得 (F-004) */
    async function getActiveJobs(stockerId) {
        const qs = stockerId ? `?stockerId=${encodeURIComponent(stockerId)}` : "";
        return request("GET", `/api/jobs/active${qs}`);
    }

    /** DELETE /api/jobs/{jobId} - ジョブ削除 (F-004) */
    async function deleteJob(jobId) {
        return request("DELETE", `/api/jobs/${encodeURIComponent(jobId)}`);
    }

    /** GET /api/inventory/shelves?stockerId=... - 棚・在庫配置取得 (F-005) */
    async function getInventoryShelves(stockerId) {
        return request("GET", `/api/inventory/shelves?stockerId=${encodeURIComponent(stockerId)}`);
    }

    /** GET /api/logs/recent?stockerId=... - 履歴ログ取得（最新10件）(F-006) */
    async function getRecentLogs(stockerId) {
        const qs = stockerId ? `?stockerId=${encodeURIComponent(stockerId)}` : "";
        return request("GET", `/api/logs/recent${qs}`);
    }

    /**
     * POST /api/equipment/command - 搬送指示／緊急停止 (F-002, F-003, F-007)
     * @param {{command:string, stockerId:string, carrierId?:string, source?:string, destination?:string}} payload
     */
    async function postEquipmentCommand(payload) {
        return request("POST", "/api/equipment/command", payload);
    }

    /**
     * POST /api/equipment/heartbeat - コンソールアプリ側の死活監視・ポーリング (E-002, E-003, E-004)
     * 本来はC#コンソールアプリが3秒ごとに送信するものだが、結合テスト前の
     * 「ポーリング通信テスト」用にブラウザ側からも疑似呼び出しできるよう公開している。
     * @param {{stockerId:string, currentOperationState:string}} payload
     */
    async function postHeartbeat(payload) {
        return request("POST", "/api/equipment/heartbeat", payload);
    }

    // ------------------------------------------------------------------------
    // 【ステータス語彙（フロントエンド側の定数）】
    // C#側 StatusVocabulary と同一文字列を厳格に維持すること。
    // ------------------------------------------------------------------------
    const Status = {
        Connection: { ONLINE: "ONLINE", OFFLINE: "OFFLINE" },
        Operation: { IDLE: "IDLE", TRAVELING: "TRAVELING", LOAD_LOCK: "LOAD_LOCK", UNLOAD_LOCK: "UNLOAD_LOCK", ALARM: "ALARM", UNKNOWN: "UNKNOWN" },
        Job: { PENDING: "PENDING", RUNNING: "RUNNING", COMPLETED: "COMPLETED", ABORTED: "ABORTED" },
        Stocker: { ACTIVE: "Active", RESERVED: "Reserved" },
        LogLevel: { INFO: "INFO", WARN: "WARN", ALARM: "ALARM" },
        Command: { TRANSFER: "TRANSFER", ESTOP: "ESTOP" }
    };

    // ------------------------------------------------------------------------
    // 【共通インターロック・フィルタ関数】
    // 複数ページで使う判定ロジックはここに集約し、画面側での判定ブレを防ぐ。
    // ------------------------------------------------------------------------

    /**
     * Rule F-007（安全インターロック）:
     * ConnectionStatusが OFFLINE、またはOperationStateが TRAVELING の場合は
     * ディスパッチ(TRANSFER)ボタンを無効化する。
     */
    function isDispatchDisabled(stocker) {
        if (!stocker) return true;
        return stocker.connectionStatus === Status.Connection.OFFLINE ||
            stocker.operationState === Status.Operation.TRAVELING;
    }

    /**
     * Rule F-005（Activeシールド）:
     * ストッカー選択ドロップダウンには status === "Active" のものだけを表示する。
     * R-002: Reserved状態のストッカーは選択肢から除外する。
     */
    function filterActiveStockers(stockers) {
        return (stockers || []).filter(s => s.status === Status.Stocker.ACTIVE);
    }

    /** 稼働状態に応じたバッジ用CSSクラス名を返す（表示の一元管理） */
    function operationBadgeClass(operationState) {
        switch (operationState) {
            case Status.Operation.IDLE: return "badge-idle";
            case Status.Operation.TRAVELING: return "badge-traveling";
            case Status.Operation.LOAD_LOCK: return "badge-load-lock";
            case Status.Operation.UNLOAD_LOCK: return "badge-unload-lock";
            case Status.Operation.ALARM: return "badge-alarm";
            default: return "badge-unknown";
        }
    }

    return {
        getStockers,
        getActiveJobs,
        deleteJob,
        getInventoryShelves,
        getRecentLogs,
        postEquipmentCommand,
        postHeartbeat,
        Status,
        isDispatchDisabled,
        filterActiveStockers,
        operationBadgeClass
    };
})();
