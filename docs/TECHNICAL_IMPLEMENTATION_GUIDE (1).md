# AMHS自動保管棚管理システム - フロントエンド実装ガイド

## 目次
1. [プロジェクト全体構成](#プロジェクト全体構成)
2. [アーキテクチャ設計](#アーキテクチャ設計)
3. [設計仕様書とコード実装のマッピング](#設計仕様書とコード実装のマッピング)
4. [PWA（Progressive Web App）実装](#pwaプログレッシブウェブアップ実装)
5. [JavaScriptロジックの詳細説明](#javascriptロジックの詳細説明)
6. [通信フロー（リアルタイムデータ取得）](#通信フロー（リアルタイムデータ取得）)
7. [エラーハンドリングとバリデーション](#エラーハンドリングとバリデーション)
8. [モックサーバーの永続化メカニズム](#モックサーバーの永続化メカニズム)

---

## プロジェクト全体構成

### フォルダ構成（フロントエンド部分のみ）

```
AmhsStockerManager/
├── Pages/                          # Razor Pages（画面定義）
│   ├── Shared/
│   │   └── _Layout.cshtml          # App Shell共通レイアウト
│   ├── _ViewImports.cshtml         # タグヘルパー設定
│   ├── _ViewStart.cshtml           # ビュー開始時処理
│   ├── Index.cshtml                # SCR-001: メイン操作画面
│   ├── Index.cshtml.cs             # PageModel（空）
│   ├── Stockers.cshtml             # SCR-002: ストッカー一覧
│   ├── Stockers.cshtml.cs
│   ├── Jobs.cshtml                 # SCR-004: ジョブ監視
│   ├── Jobs.cshtml.cs
│   ├── Shelves.cshtml              # SCR-003: 棚・在庫
│   ├── Shelves.cshtml.cs
│   ├── History.cshtml              # SCR-005: 履歴ログ
│   └── History.cshtml.cs
└── wwwroot/                        # 静的アセット
    ├── js/
    │   ├── amhs-core.js            # 【決定ファイル】API通信コア
    │   ├── mock-server.js          # 仮想JSONサーバー
    │   ├── page-index.js           # Index専用ロジック
    │   ├── page-stockers.js        # Stockers専用ロジック
    │   ├── page-jobs.js            # Jobs専用ロジック
    │   ├── page-shelves.js         # Shelves専用ロジック
    │   └── page-history.js         # History専用ロジック
    ├── css/
    │   └── site.css                # モバイル最適化スタイル
    ├── icons/
    │   ├── icon-192.png            # PWAアイコン
    │   ├── icon-512.png
    │   └── icon-maskable-512.png
    ├── manifest.json               # PWAマニフェスト
    └── sw.js                       # Service Worker（オフラインキャッシュ）
```

---

## アーキテクチャ設計

### 全体図：フロントエンド/バックエンド分離

```
┌─────────────────────────────────────────────────────────────┐
│                    ブラウザ（ユーザーデバイス）                   │
├──────────────────────────┬──────────────────────────────────┤
│   Pages (Razor Pages)    │  wwwroot (Static Assets + JS)   │
│  ・Index.cshtml          │  ・html content                 │
│  ・Stockers.cshtml       │  ・amhs-core.js ★★★ 決定ポイント  │
│  ・Jobs.cshtml           │  ・page-*.js                    │
│  ・Shelves.cshtml        │  ・css / icons                  │
│  ・History.cshtml        │                                 │
│                          │  【USE_MOCK_API 切替ロジック】   │
│  (PageModel: 空)         │  true  → mock-server.js         │
│                          │  false → 実 ASP.NET Core API    │
└──────────────────────────┴──────────────────────────────────┘
                │
                │ fetch() リクエスト
                ↓
┌─────────────────────────────────────────────────────────────┐
│           【バックエンド】ASP.NET Core + SQL Server          │
│  (パートナー実装：Controllers, Models, Services)            │
│  ・StockersController      → GET /api/stockers             │
│  ・JobsController          → GET /api/jobs/active, DELETE  │
│  ・InventoryController     → GET /api/inventory/shelves    │
│  ・LogsController          → GET /api/logs/recent          │
│  ・EquipmentController     → POST /api/equipment/command   │
│                            → POST /api/equipment/heartbeat │
└─────────────────────────────────────────────────────────────┘
```

### 設計方針：なぜこの構成か？

| 項目 | 設計選択 | 理由 |
|------|--------|------|
| **PageModel空** | `OnGet()` は実装しない | データはJS側の非同期`fetch()`で、ページ遷移後も取得する |
| **API集約** | `amhs-core.js`に全通信を集約 | `USE_MOCK_API`フラグ1行で実装/本番を切り替え可能 |
| **モック永続化** | `sessionStorage`を使用 | ページ遷移（Razor Pages間の移動）後もテストデータを保持 |
| **ステータス語彙** | 厳格な文字列定数 | フロントエンド/バックエンド/コンソール間での大文字小文字ゆれを排除 |

---

## 設計仕様書とコード実装のマッピング

### 1. SCR-001: メイン操作画面（ディスパッチコンソール）

**設計仕様書対応：** F-001, F-002, F-003, F-007

#### ファイル一覧
- `Pages/Index.cshtml` — 画面レイアウト
- `Pages/Index.cshtml.cs` — PageModel（空）
- `wwwroot/js/page-index.js` — ページロジック
- `wwwroot/js/amhs-core.js` — API呼び出し

#### 実装内容

**① ストッカー選択ドロップダウン（F-005, R-002）**

```csharp
<!-- Index.cshtml -->
<select id="stockerSelect" class="form-select mb-2"></select>
```

```javascript
// page-index.js - 初期ロード時にActiveストッカーのみを取得・表示
async function refreshStockerStatus() {
    const res = await AmhsCore.getStockers();  // GET /api/stockers を呼び出し
    
    allStockersCache = res.data || [];
    activeStockers = AmhsCore.filterActiveStockers(allStockersCache);  // F-005適用
    
    if (activeStockers.length > 0) {
        renderStockerOptions(activeStockers);  // ドロップダウンを再描画
    }
}

function renderStockerOptions(stockers) {
    stockerSelect.innerHTML = "";
    stockers.forEach(s => {
        const opt = document.createElement("option");
        opt.value = s.stockerId;
        opt.textContent = `${s.stockerName} (${s.stockerId})`;
        stockerSelect.appendChild(opt);
    });
}
```

**amhs-core.js側の実装:**

```javascript
// ステータス語彙（C#のStatusVocabularyと同一）
const Status = {
    Stocker: { ACTIVE: "Active", RESERVED: "Reserved" }
};

// F-005: Activeのみをフィルタする関数
function filterActiveStockers(stockers) {
    return (stockers || []).filter(s => s.status === Status.Stocker.ACTIVE);
}
```

**② 接続状態・稼働状態バッジの表示（リアルタイム）**

```javascript
// page-index.js - 選択中ストッカーの状態を毎回表示
function applyInterlock(stocker) {
    if (!stocker) {
        connBadge.textContent = "---";
        opBadge.textContent = "---";
        return;
    }
    
    connBadge.textContent = stocker.connectionStatus;  // "ONLINE" or "OFFLINE"
    connBadge.className = "badge " + 
        (stocker.connectionStatus === AmhsCore.Status.Connection.ONLINE ? 
            "badge-online" : "badge-offline");
    
    opBadge.textContent = stocker.operationState;  // "IDLE", "TRAVELING", "ALARM" etc.
    opBadge.className = "badge " + AmhsCore.operationBadgeClass(stocker.operationState);
}
```

**③ 搬送指示（TRANSFER）の送信（F-002, F-003）**

```javascript
// page-index.js
async function handleDispatchClick() {
    const stockerId = stockerSelect.value;
    const carrierId = carrierIdInput.value.trim();
    const source = sourceSelect.value;
    const destination = destSelect.value;
    
    // 単体チェック（UI-001）
    if (!carrierId) {
        showAlert("キャリアIDを入力してください");
        return;
    }
    
    // 状態チェック（F-007: 安全インターロック）
    const current = allStockersCache.find(s => s.stockerId === stockerId);
    if (AmhsCore.isDispatchDisabled(current)) {
        showAlert("装置がオフライン、または搬送中のため指示を送信できません");
        return;
    }
    
    // API呼び出し：POST /api/equipment/command
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
        carrierIdInput.value = "";  // フォーム初期化
    } else {
        // サーバーから返却されたエラーメッセージ（F-003）
        const msg = res.data?.message || "サーバーエラーが発生しました";
        showAlert(`[${res.data?.errorCode || "ERR"}] ${msg}`);
    }
}
```

**amhs-core.js側:**

```javascript
// POST /api/equipment/command のラッパー関数
async function postEquipmentCommand(payload) {
    return request("POST", "/api/equipment/command", payload);
}

// 実装：USE_MOCK_API フラグで分岐
async function request(method, path, body) {
    if (USE_MOCK_API) {
        // 仮想JSONサーバー（mock-server.js）へ委譲
        const res = await MockServer.handle(method, path, body);
        return finalize(res);
    }
    
    // ▼▼▼ 【実API接続ポイント】 ここが本番バックエンドへの実通信箇所 ▼▼▼
    const res = await fetch(`${API_BASE}${path}`, {
        method,
        headers: body ? { "Content-Type": "application/json" } : undefined,
        body: body ? JSON.stringify(body) : undefined
    });
    // ▲▲▲ 実API接続ポイントはここまで ▲▲▲
    
    return finalize(res);
}
```

**④ 安全インターロック（F-007）**

```javascript
// amhs-core.js
function isDispatchDisabled(stocker) {
    if (!stocker) return true;
    return stocker.connectionStatus === Status.Connection.OFFLINE ||
           stocker.operationState === Status.Operation.TRAVELING;
}
```

条件：OFFLINE か TRAVELING のときはボタン無効化

```javascript
// page-index.js
btnDispatch.disabled = AmhsCore.isDispatchDisabled(stocker);
```

**⑤ E-STOP（緊急停止）**

```javascript
// page-index.js
async function handleEStopClick() {
    const stockerId = stockerSelect.value;
    if (!confirm("緊急停止コマンドを送信します。よろしいですか？")) return;
    
    const res = await AmhsCore.postEquipmentCommand({
        command: AmhsCore.Status.Command.ESTOP,  // "ESTOP"
        stockerId
    });
    
    if (res.ok) {
        showToast("E-STOPコマンドを送信しました");
    }
}
```

**⑥ 3秒ごとの定期監視ポーリング（5.2「リアルタイム定期監視モジュール」）**

```javascript
// page-index.js
const REFRESH_INTERVAL_MS = 3000;  // 3秒間隔

function startPollingLoop() {
    if (pollTimerId) clearInterval(pollTimerId);
    pollTimerId = setInterval(refreshStockerStatus, REFRESH_INTERVAL_MS);
}

document.addEventListener("DOMContentLoaded", async () => {
    await refreshStockerStatus();  // 初回即時実行
    startPollingLoop();            // その後3秒ごとに実行
});
```

このループは以下を行う：
- `GET /api/stockers` を呼び出し
- レスポンスから選択中ストッカーの最新状態を抽出
- 接続状態・稼働状態バッジを更新
- インターロック判定（ボタン有効/無効）を再計算

**⑦ ポーリング通信テストパネル**

```javascript
// page-index.js
function logPoll(text, isError) {
    const line = document.createElement("div");
    line.className = "log-line" + (isError ? " err" : "");
    const ts = new Date().toLocaleTimeString("ja-JP");
    line.textContent = `[${ts}] ${text}`;
    pollLogPanel.prepend(line);
}

// refreshStockerStatus() の最後に
logPoll(`GET /api/stockers OK (${allStockersCache.length}件) 選択中:${selectedId} conn=${current?.connectionStatus} op=${current?.operationState}`);
```

このパネルは以下を記録：
- 各ポーリング実行時刻
- リクエスト/レスポンスの状態
- ストッカー接続状態・稼働状態
- コンソール側ハートビート送信結果

---

### 2. SCR-002: ストッカー一覧（装置レジストリ）

**設計仕様書対応：** F-009

#### ファイル
- `Pages/Stockers.cshtml` / `Stockers.cshtml.cs`
- `wwwroot/js/page-stockers.js`

#### 実装内容

**① Bootstrapカード形式での表示**

```javascript
// page-stockers.js
function renderCards(stockers) {
    listContainer.innerHTML = stockers.map(s => {
        const offlineClass = s.connectionStatus === AmhsCore.Status.Connection.OFFLINE 
            ? "is-offline" : "";
        
        return `
            <div class="amhs-card stocker-card ${offlineClass}">
                <div class="d-flex justify-content-between align-items-start">
                    <div>
                        <div class="fw-bold">${escapeHtml(s.stockerName)}</div>
                        <span class="badge ${s.status === "Active" ? "text-bg-primary" : "text-bg-secondary"}">
                            ${escapeHtml(s.status)}
                        </span>
                    </div>
                    <div class="text-end">
                        <span class="badge ${s.connectionStatus === "ONLINE" ? "badge-online" : "badge-offline"}">
                            ${escapeHtml(s.connectionStatus)}
                        </span>
                        <span class="badge ${AmhsCore.operationBadgeClass(s.operationState)}">
                            ${escapeHtml(s.operationState)}
                        </span>
                    </div>
                </div>
            </div>
        `;
    }).join("");
}
```

**② R-004: OFFLINE時のグレーアウト（視覚的な警告）**

```css
/* site.css */
.stocker-card.is-offline {
    opacity: 0.55;
    filter: grayscale(40%);
}
```

JavaScriptでこのクラスを条件付与：

```javascript
const offlineClass = s.connectionStatus === AmhsCore.Status.Connection.OFFLINE 
    ? "is-offline" : "";
```

**③ 定期監視（3秒ごと）**

```javascript
// page-stockers.js
const REFRESH_INTERVAL_MS = 3000;

function startPollingLoop() {
    if (pollTimerId) clearInterval(pollTimerId);
    pollTimerId = setInterval(loadStockers, REFRESH_INTERVAL_MS);
}

document.addEventListener("DOMContentLoaded", async () => {
    await loadStockers();
    startPollingLoop();
});
```

---

### 3. SCR-004: ジョブ監視（キュー監視）

**設計仕様書対応：** F-004

#### ファイル
- `Pages/Jobs.cshtml` / `Jobs.cshtml.cs`
- `wwwroot/js/page-jobs.js`

#### 実装内容

**① アクティブジョブの表示（PENDING / RUNNING のみ）**

```javascript
// page-jobs.js
async function loadJobs() {
    const res = await AmhsCore.getActiveJobs();  // GET /api/jobs/active
    
    if (!res.ok) {
        jobList.innerHTML = `<p class="text-danger">状態取得失敗</p>`;
        return;
    }
    
    renderJobs(res.data || []);
}

function renderJobs(jobs) {
    jobList.innerHTML = jobs.map(j => `
        <div class="amhs-card d-flex justify-content-between">
            <div>
                <div class="fw-bold">${escapeHtml(j.jobId)} 
                    <span class="badge ${jobBadgeClass(j.status)}">${escapeHtml(j.status)}</span>
                </div>
                <div class="small text-muted">${escapeHtml(j.source)} → ${escapeHtml(j.destination)}</div>
            </div>
            <button class="btn btn-sm btn-outline-danger btn-delete-job" 
                    data-job-id="${escapeHtml(j.jobId)}">
                <i class="bi bi-trash3"></i>
            </button>
        </div>
    `).join("");
}
```

**② ジョブステータスバッジの色分け**

```javascript
function jobBadgeClass(status) {
    switch (status) {
        case AmhsCore.Status.Job.PENDING:   return "badge-job-pending";    // 黄色
        case AmhsCore.Status.Job.RUNNING:   return "badge-job-running";    // 青
        case AmhsCore.Status.Job.COMPLETED: return "badge-job-completed";  // 緑
        case AmhsCore.Status.Job.ABORTED:   return "badge-job-aborted";    // 赤
        default: return "bg-secondary";
    }
}
```

**③ ジョブ削除（DELETE）**

```javascript
// page-jobs.js
async function handleDelete(jobId) {
    if (!confirm(`ジョブ ${jobId} を削除します。よろしいですか？`)) return;
    
    const res = await AmhsCore.deleteJob(jobId);  // DELETE /api/jobs/{jobId}
    
    if (!res.ok) {
        alert("ジョブ削除に失敗しました");  // UI-008
    }
    
    await loadJobs();  // 一覧を再読込
}
```

---

### 4. SCR-003: 棚・在庫（インベントリグリッド）

**設計仕様書対応：** F-005

#### ファイル
- `Pages/Shelves.cshtml` / `Shelves.cshtml.cs`
- `wwwroot/js/page-shelves.js`

#### 実装内容

**① F-005: Activeストッカーのみをドロップダウンに表示（R-002）**

```javascript
// page-shelves.js
async function loadActiveStockerOptions() {
    const res = await AmhsCore.getStockers();
    
    const activeOnly = AmhsCore.filterActiveStockers(res.data || []);
    
    stockerSelect.innerHTML = activeOnly.map(s =>
        `<option value="${s.stockerId}">${s.stockerName} (${s.stockerId})</option>`
    ).join("");
}
```

**② 色分けグリッド表示（SCR-003仕様：空き=灰色 / 占有中=黄色 / Alarm=赤）**

```javascript
// page-shelves.js
function classifyShelf(shelf) {
    const overrideState = localOverride.get(shelf.shelfName);
    if (overrideState) return overrideState;  // R-003: ローカル上書き状態
    return shelf.carrierId ? "occupied" : "empty";
}

function renderGrid(shelves) {
    shelfGrid.innerHTML = shelves.map(s => {
        const state = classifyShelf(s);
        return `
            <div class="shelf-cell ${state}" data-shelf-name="${s.shelfName}">
                <div class="shelf-name">${s.shelfName}</div>
                <div>${s.carrierId || "EMPTY"}</div>
            </div>
        `;
    }).join("");
}
```

**CSS（site.css）での色定義:**

```css
:root {
    --shelf-empty:    #bdbdbd;   /* 灰色 */
    --shelf-occupied: #fbc02d;   /* 黄色 */
    --shelf-alarm:    #e53935;   /* 赤   */
}

.shelf-cell.empty    { background-color: var(--shelf-empty);    }
.shelf-cell.occupied { background-color: var(--shelf-occupied); }
.shelf-cell.alarm    { background-color: var(--shelf-alarm);    }
```

**③ R-003: タップによるローカル表示切替（デモ用）**

```javascript
// page-shelves.js
const localOverride = new Map();  // 画面上のみの一時上書き状態

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

// ストッカー切替時にローカル状態をリセット
stockerSelect.addEventListener("change", () => {
    localOverride.clear();
    loadShelves();
});
```

**注記:** このタップ操作はローカル画面上のみの変更であり、サーバーには送信されません。設計仕様書にはタップ対応の書き込み系APIが無いため、デモ表示に留めています。

---

### 5. SCR-005: 履歴ログ（イベントログ）

**設計仕様書対応：** F-006

#### ファイル
- `Pages/History.cshtml` / `History.cshtml.cs`
- `wwwroot/js/page-history.js`

#### 実装内容

**① ログの取得と表示**

```javascript
// page-history.js
async function loadLogs() {
    // 全ストッカー分の最新10件を取得（stockerId省略）
    const res = await AmhsCore.getRecentLogs();
    
    if (!res.ok) {
        logList.innerHTML = `<p class="text-danger">状態取得失敗</p>`;
        return;
    }
    
    renderLogs(res.data || []);
}

function renderLogs(logs) {
    logList.innerHTML = logs.map(l => `
        <div class="amhs-card">
            <div class="d-flex justify-content-between">
                <span class="badge ${levelBadgeClass(l.level)}">${l.level}</span>
                <span class="small text-muted">${formatTimestamp(l.timestamp)}</span>
            </div>
            <div class="mt-2">${escapeHtml(l.message)}</div>
        </div>
    `).join("");
}
```

**② ログレベル別の色分け**

```javascript
function levelBadgeClass(level) {
    switch (level) {
        case AmhsCore.Status.LogLevel.INFO:  return "text-bg-secondary";  // グレー
        case AmhsCore.Status.LogLevel.WARN:  return "text-bg-warning";    // 黄色
        case AmhsCore.Status.LogLevel.ALARM: return "text-bg-danger";     // 赤
        default: return "text-bg-secondary";
    }
}
```

**③ 手動更新ボタン（自動ポーリングなし）**

```javascript
// page-history.js - 【注】このページは自動ポーリングなし
btnRefresh.addEventListener("click", loadLogs);

document.addEventListener("DOMContentLoaded", loadLogs);  // 初回1回のみ
```

**理由:** 履歴ログは優先度が低く、ユーザーが明示的に「更新」ボタンをクリックしたときだけ再取得する仕様。

---

## PWA（Progressive Web App）実装

### 1. manifest.json（PWAマニフェスト）

```json
{
    "name": "自動保管棚管理システム",
    "short_name": "AMHS Console",
    "description": "Automated Storage and Transfer Management System - スマートフォン操作コンソール",
    "start_url": "/Index",
    "scope": "/",
    "display": "standalone",        // ← ネイティブアプリ風（ブラウザUIなし）
    "orientation": "portrait",      // ← 縦向きのみ（16:9 Android対応）
    "background_color": "#0f1115",
    "theme_color": "#0d1b2a",
    "icons": [
        {
            "src": "/icons/icon-192.png",
            "sizes": "192x192",
            "type": "image/png",
            "purpose": "any"
        },
        {
            "src": "/icons/icon-512.png",
            "sizes": "512x512",
            "type": "image/png",
            "purpose": "any"
        },
        {
            "src": "/icons/icon-maskable-512.png",
            "sizes": "512x512",
            "type": "image/png",
            "purpose": "maskable"   // ← 型抜きアイコン（iOS等での表示最適化）
        }
    ]
}
```

**各フィールドの意味：**

| フィールド | 値 | 意味 |
|-----------|-----|------|
| `display` | `standalone` | ホーム画面から起動したとき、ブラウザのURL/ツールバーを隠す |
| `orientation` | `portrait` | 常に縦向きに固定（横向き回転を禁止） |
| `start_url` | `/Index` | アプリ起動時のエントリーポイント |
| `scope` | `/` | このアプリの適用範囲（ルート以下すべて） |

### 2. _Layout.cshtml での PWA登録

```html
<!-- _Layout.cshtml のヘッド部分 -->
<link rel="manifest" href="~/manifest.json" />
<link rel="apple-touch-icon" href="~/icons/icon-192.png" />
<link rel="icon" type="image/png" href="~/icons/icon-192.png" />
<meta name="theme-color" content="#0d1b2a" />
```

### 3. sw.js（Service Worker）— オフラインキャッシュ

```javascript
const CACHE_NAME = "amhs-shell-cache-v1";

// 事前キャッシュ対象（App Shellの静的アセット）
const SHELL_ASSETS = [
    "/",
    "/Index", "/Stockers", "/Jobs", "/Shelves", "/History",
    "/css/site.css",
    "/js/amhs-core.js", "/js/mock-server.js",
    "/js/page-*.js",
    "/manifest.json",
    "/icons/icon-192.png"
];

// インストール時：App Shellをキャッシュ
self.addEventListener("install", (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_ASSETS))
    );
});

// フェッチ時：キャッシュ優先 + オフライン時の代替
self.addEventListener("fetch", (event) => {
    const url = new URL(event.request.url);
    
    // /api/* はキャッシュせず常にネットワーク優先（リアルタイム性重視）
    if (url.pathname.startsWith("/api/")) {
        return;
    }
    
    event.respondWith(
        caches.match(event.request).then((cached) => {
            if (cached) return cached;  // キャッシュから返す
            
            return fetch(event.request).catch(() => {
                // オフライン時かつナビゲーション → App Shell返す
                if (event.request.mode === "navigate") {
                    return caches.match("/");
                }
            });
        })
    );
});
```

**ロジック説明：**

1. **インストール時**：初回アクセス時に App Shell（HTML/CSS/JS）をキャッシュ
2. **フェッチ時**：
   - `/api/*` → 常にネットワーク優先（最新のデータ取得）
   - その他 → キャッシュあればそれを、なければ取得、オフラインならApp Shellを代わりに返す

この設計により、バックエンドが落ちていても App Shell は表示できます。

### 4. _Layout.cshtml でのService Worker登録

```html
<script>
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', () => {
            navigator.serviceWorker.register('/sw.js')
                .then(reg => console.log('[PWA] Service Worker registered:', reg.scope))
                .catch(err => console.error('[PWA] Service Worker registration failed:', err));
        });
    }
</script>
```

---

## JavaScriptロジックの詳細説明

### 1. amhs-core.js — 決定ファイル（リンピン）

このファイルは**ただ1つの役割**を持つ：フロントエンドのすべての外部通信（API呼び出し）の入り口を統一する。

#### ① USE_MOCK_API フラグの仕組み

```javascript
const USE_MOCK_API = true;  // ← この1行で全挙動が変わる

async function request(method, path, body) {
    if (USE_MOCK_API) {
        // 仮想JSONサーバー（インメモリ、テスト用）
        const res = await MockServer.handle(method, path, body);
        return finalize(res);
    }
    
    // 実APIへのfetch（本番運用時）
    const res = await fetch(`${API_BASE}${path}`, {
        method,
        headers: body ? { "Content-Type": "application/json" } : undefined,
        body: body ? JSON.stringify(body) : undefined
    });
    
    return finalize(res);
}
```

**流れ：**
- `true` → `mock-server.js` の `MockServer.handle()` へ処理を委譲
- `false` → 実 HTTP `fetch()` を発行

**メリット：** ページ側（`page-index.js` など）は `AmhsCore.getStockers()` を呼び出すだけで、内部がどこからデータを取得しているかを意識しない。

#### ② 公開API（各ページが呼び出す関数）

```javascript
// 【読み取り系】
async function getStockers()                        // GET /api/stockers
async function getActiveJobs(stockerId?)            // GET /api/jobs/active?stockerId=...
async function getInventoryShelves(stockerId)       // GET /api/inventory/shelves?stockerId=...
async function getRecentLogs(stockerId?)            // GET /api/logs/recent?stockerId=...

// 【書き込み系】
async function postEquipmentCommand(payload)        // POST /api/equipment/command
async function postHeartbeat(payload)               // POST /api/equipment/heartbeat
async function deleteJob(jobId)                     // DELETE /api/jobs/{jobId}

// 【ヘルパー】
function isDispatchDisabled(stocker)                // F-007: ディスパッチ無効化判定
function filterActiveStockers(stockers)             // F-005: Activeフィルタ
function operationBadgeClass(operationState)        // ステータスバッジCSSクラス
```

#### ③ ステータス語彙の集約

```javascript
const Status = {
    Connection: { ONLINE: "ONLINE", OFFLINE: "OFFLINE" },
    Operation: { IDLE: "IDLE", TRAVELING: "TRAVELING", LOAD_LOCK: "LOAD_LOCK", 
                 UNLOAD_LOCK: "UNLOAD_LOCK", ALARM: "ALARM", UNKNOWN: "UNKNOWN" },
    Job: { PENDING: "PENDING", RUNNING: "RUNNING", COMPLETED: "COMPLETED", ABORTED: "ABORTED" },
    Stocker: { ACTIVE: "Active", RESERVED: "Reserved" },
    LogLevel: { INFO: "INFO", WARN: "WARN", ALARM: "ALARM" },
    Command: { TRANSFER: "TRANSFER", ESTOP: "ESTOP" }
};
```

**なぜここに集約するか：**
- C#のバックエンド（`StatusVocabulary.cs`）と同一の値を使う
- ページ側で文字列をハードコーディングしない（タイポ防止）
- バックエンド変更時は `amhs-core.js` のみ修正すれば、ページ側は自動で対応

---

### 2. mock-server.js — 仮想JSONサーバー

バックエンドが完成する前でも、フロントエンド単体でテストできるように、インメモリDBとルーティングを提供します。

#### ① sessionStorageでの永続化

```javascript
const STORAGE_KEY = "amhsMockDb";

// ページ遷移時に前回の状態を復元
function loadDb() {
    try {
        const raw = sessionStorage.getItem(STORAGE_KEY);
        if (raw) return JSON.parse(raw);
    } catch (e) {
        console.warn("[MockServer] sessionStorageの読み込みに失敗");
    }
    // 保存データがなければシードから生成
    return JSON.parse(JSON.stringify(seedDb));
}

// 変更後は常にsessionStorageに保存
function persist() {
    try {
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(db));
    } catch (e) {
        console.warn("[MockServer] sessionStorageへの保存に失敗");
    }
}

const db = loadDb();  // 初期化時に復元
```

**なぜこれが必要か：**
- Razor Pages は タブ切替ごとに完全なページ遷移（フルリロード）が発生
- `script` タグが再度実行されるため、通常のJS変数は初期化されてしまう
- `sessionStorage` は同じブラウザタブ内で永続化される

**ライフサイクル：**
```
初回アクセス
  ↓
loadDb() → sessionStorage にデータがない → seedDb をコピー使用
  ↓
dispatch → db.jobs に追加 → persist() で sessionStorage に保存
  ↓
「Jobs」タブをクリック（完全なページ遷移）
  ↓
Jobs.cshtml が読み込まれる → mock-server.js 再実行 → loadDb() → sessionStorage から復元
  ↓
新しく追加されたジョブが Jobs タブに表示される ✓
```

#### ② ルーティング（handle 関数）

```javascript
async function handle(method, path, body) {
    await wait(randomLatency());  // 150〜400ms のランダム遅延で実通信を模擬
    
    const url = new URL(path, "https://mock.local");
    
    // GET /api/stockers
    if (method === "GET" && url.pathname === "/api/stockers") {
        return ok(db.stockers);
    }
    
    // GET /api/jobs/active?stockerId=...
    if (method === "GET" && url.pathname === "/api/jobs/active") {
        const stockerId = url.searchParams.get("stockerId");
        const list = db.jobs.filter(j =>
            (j.status === "PENDING" || j.status === "RUNNING") &&
            (!stockerId || j.stockerId === stockerId)
        );
        return ok(list);
    }
    
    // ... その他のエンドポイント ...
}
```

#### ③ エラーハンドリング（バリデーション）

```javascript
function handleEquipmentCommand(body) {
    const stocker = db.stockers.find(s => s.stockerId === body.stockerId);
    
    if (!stocker) {
        return badRequest({ errorCode: "ERR-404", message: "指定されたストッカーが見つかりません" });
    }
    
    if (body.command === "TRANSFER") {
        // F-007: 接続状態チェック
        if (stocker.connectionStatus === "OFFLINE") {
            return badRequest({ errorCode: "ERR-001", message: "装置がオフラインです" });
        }
        
        // F-003: キャリアIDの重複チェック
        const duplicate = db.jobs.some(j =>
            j.carrierId === body.carrierId && (j.status === "PENDING" || j.status === "RUNNING")
        );
        if (duplicate) {
            return badRequest({ errorCode: "ERR-002", message: "Job creation failed: Carrier ID already in an active job (重複エラー)" });
        }
        
        // 二重保管チェック（搬送先が既に占有されている）
        const destShelf = shelvesForStocker.find(s => s.shelfName === body.destination);
        if (destShelf && destShelf.carrierId) {
            return badRequest({ errorCode: "ERR-002", message: "Job creation failed: Target shelf is already occupied (二重保管エラー)" });
        }
        
        // 出庫元チェック（搬送元が空である）
        const sourceShelf = shelvesForStocker.find(s => s.shelfName === body.source);
        if (sourceShelf && body.source !== "IN_PORT" && !sourceShelf.carrierId) {
            return badRequest({ errorCode: "ERR-003", message: "出庫時ソース空異常: 指定された搬送元にキャリアが存在しません" });
        }
        
        // すべてのチェック合格 → ジョブを生成
        const jobId = `JOB${new Date().getFullYear()}${String(jobSeq++).padStart(6, "0")}`;
        db.jobs.push({ jobId, stockerId: body.stockerId, carrierId: body.carrierId, 
                      source: body.source, destination: body.destination, status: "PENDING" });
        db.logs.unshift({ timestamp: new Date().toISOString(), level: "INFO", 
                        message: `搬送ジョブを登録しました (${jobId}: ...)`, stockerId: body.stockerId });
        persist();
        return ok({ success: true, jobId });
    }
}
```

このエラーコード（ERR-001, ERR-002, ERR-003）は設計仕様書 § 4.3「エラーコード設計」に対応しており、フロントエンド側の `page-index.js` でこれを読み込んで画面に表示します。

---

### 3. page-index.js — メイン操作画面のロジック

#### ① ポーリングループの仕組み

```javascript
// 【フェーズ1】ページ読込完了（DOMContentLoaded）
document.addEventListener("DOMContentLoaded", async () => {
    logPoll(`USE_MOCK_API = ${USE_MOCK_API} ...`);
    await refreshStockerStatus();  // 【フェーズ2】即時実行
    startPollingLoop();            // 【フェーズ3】定期実行開始
});

// 【フェーズ2】1回のポーリング実行
async function refreshStockerStatus() {
    const res = await AmhsCore.getStockers();
    
    allStockersCache = res.data || [];
    activeStockers = AmhsCore.filterActiveStockers(allStockersCache);
    
    if (activeStockers.length > 0) {
        renderStockerOptions(activeStockers);
    }
    
    const selectedId = stockerSelect.value;
    const current = allStockersCache.find(s => s.stockerId === selectedId);
    applyInterlock(current);
    
    logPoll(`GET /api/stockers OK (${allStockersCache.length}件) 選択中:${selectedId || "-"} conn=${current?.connectionStatus} op=${current?.operationState}`);
}

// 【フェーズ3】定期実行の開始
function startPollingLoop() {
    if (pollTimerId) clearInterval(pollTimerId);
    pollTimerId = setInterval(refreshStockerStatus, REFRESH_INTERVAL_MS);  // 3秒ごと
}
```

**タイムライン（初回アクセス時）：**
```
t=0秒        ページ読み込み完了
             → DOMContentLoaded イベント発火
             → refreshStockerStatus() 即時実行
             → ストッカーオプション表示
             → startPollingLoop() で 3秒ごとの定期実行開始

t=3秒        refreshStockerStatus() 自動実行
t=6秒        refreshStockerStatus() 自動実行
...（以降3秒ごと）
```

#### ② ディスパッチボタンのフロー

```javascript
btnDispatch.addEventListener("click", handleDispatchClick);

async function handleDispatchClick() {
    // 【ステップ1】ユーザー入力の検証
    if (!carrierIdInput.value.trim()) {
        showAlert("キャリアIDを入力してください");
        return;  // ← ここで終了、API呼び出しなし
    }
    
    // 【ステップ2】装置の稼働状態チェック（F-007）
    const current = allStockersCache.find(s => s.stockerId === stockerSelect.value);
    if (AmhsCore.isDispatchDisabled(current)) {
        showAlert("装置がオフライン、または搬送中のため指示を送信できません");
        return;  // ← ここで終了
    }
    
    // 【ステップ3】API呼び出し
    btnDispatch.disabled = true;  // UI操作を防ぐ
    const res = await AmhsCore.postEquipmentCommand({
        command: "TRANSFER",
        stockerId: stockerSelect.value,
        carrierId: carrierIdInput.value,
        source: sourceSelect.value,
        destination: destSelect.value
    });
    
    // 【ステップ4】レスポンス判定
    if (res.ok && res.data?.success) {
        // ✓ 成功
        clearAlert();
        showToast("ジョブの送信に成功しました");
        carrierIdInput.value = "";  // フォーム初期化
        logPoll(`POST /api/equipment/command OK jobId=${res.data.jobId}`);
    } else {
        // ✗ 失敗（バリデーションエラー等）
        const msg = res.data?.message || "サーバーエラーが発生しました";
        showAlert(`[${res.data?.errorCode || "ERR"}] ${msg}`);
        logPoll(`POST /api/equipment/command 失敗: ${msg}`, true);
    }
    
    // 【ステップ5】表示更新
    await refreshStockerStatus();  // 最新データを取得・表示
    btnDispatch.disabled = false;
}
```

**エラーハンドリングのマップ：**

| エラーコード | メッセージ | 原因 | ユーザーアクション |
|-----------|----------|------|-----------------|
| ERR-001 | 装置がオフラインです | ストッカーが OFF LINE | ストッカーが復帰するまで待つ |
| ERR-002 | キャリアID重複 / 二重保管エラー | キャリアが既に別ジョブに、または搬送先が占有中 | 異なるキャリアID/搬送先を選択 |
| ERR-003 | 出庫時ソース空異常 | 搬送元の棚が空 | 棚を選択し直す |
| ERR-004 | (未定義) | | |

#### ③ コンソール側ハートビートの模擬送信

```javascript
async function simulateConsoleHeartbeatOnce() {
    const stockerId = stockerSelect.value;
    if (!stockerId) {
        logPoll("ハートビート送信スキップ: ストッカーが選択されていません", true);
        return;
    }
    
    // コンソール側から送信されるペイロードを模擬
    const payload = { 
        stockerId, 
        currentOperationState: AmhsCore.Status.Operation.IDLE 
    };
    logPoll(`POST /api/equipment/heartbeat 送信 -> ${JSON.stringify(payload)}`);
    
    // ハートビート API 呼び出し
    const res = await AmhsCore.postHeartbeat(payload);
    
    if (res.ok) {
        if (res.data.hasPendingJob) {
            logPoll(`POST /api/equipment/heartbeat 応答 <- hasPendingJob=true job=${res.data.job.jobId}`);
        } else {
            logPoll(`POST /api/equipment/heartbeat 応答 <- hasPendingJob=false`);
        }
    } else {
        logPoll(`POST /api/equipment/heartbeat 失敗 (status=${res.status})`, true);
    }
}
```

このボタンをクリックすることで、コンソール側がする 3秒ごとのポーリング挙動を手動で1回再現でき、結合テスト前に通信が正しく機能しているか確認できます。

---

## 通信フロー（リアルタイムデータ取得）

### 全体フロー図

```
【ブラウザ】
┌────────────────────────────────────┐
│  Index.cshtml（メイン操作画面）      │
│  ↓                                 │
│  page-index.js                     │
│  ├─ refreshStockerStatus()        │
│  │  毎3秒 → AmhsCore.getStockers()│
│  │           ↓                     │
│  └─ handleDispatchClick()         │
│     ユーザーがボタンクリック →     │
│     AmhsCore.postEquipmentCommand()│
│             ↓                      │
└────────────────────────────────────┘
        ↓ fetch() / MockServer.handle()
┌────────────────────────────────────┐
│  amhs-core.js                      │
│  ├─ USE_MOCK_API = true → mock    │
│  └─ USE_MOCK_API = false → 実API  │
│             ↓                      │
└────────────────────────────────────┘
        ↓ (mock時) / fetch()(実API時)
┌────────────────────────────────────┐
│  mock-server.js  または  ASP.NET Core API │
│  ├─ GET /api/stockers            │
│  ├─ POST /api/equipment/command   │
│  ├─ GET /api/jobs/active         │
│  └─ ... など                      │
│             ↓                      │
└────────────────────────────────────┘
        ↓ JSON レスポンス返却
┌────────────────────────────────────┐
│  amhs-core.js                      │
│  → finalize()して統一形式に変換     │
│  { ok: boolean, status, data }    │
│             ↓                      │
└────────────────────────────────────┘
        ↓ 呼び出し元に返却
┌────────────────────────────────────┐
│  page-index.js / page-jobs.js etc. │
│  → res.ok ? "成功処理" : "エラー表示"│
│  → DOM を更新（画面表示を変える）   │
│             ↓                      │
└────────────────────────────────────┘
        ↓ レンダリング
┌────────────────────────────────────┐
│  ユーザーが見る画面                  │
│  ・バッジ更新（接続状態・稼働状態） │
│  ・ジョブリスト更新                 │
│  ・エラー表示                       │
└────────────────────────────────────┘
```

### 具体例：Index 画面で dispatch ボタン クリック時の流れ

```
【1】ユーザーがボタンクリック
     ↓
【2】btnDispatch.addEventListener("click", handleDispatchClick)
     ↓
【3】handleDispatchClick() 実行
     ├─ キャリアID入力チェック
     ├─ 装置稼働状態チェック（F-007）
     ├─ btnDispatch.disabled = true
     │  ↓
     └─ AmhsCore.postEquipmentCommand({...})
        ↓
【4】amhs-core.js の postEquipmentCommand()
     ↓
     await request("POST", "/api/equipment/command", payload)
     ↓
【5】request() 関数
     ├─ USE_MOCK_API = true の場合：
     │  await MockServer.handle("POST", "/api/equipment/command", payload)
     │  ↓
     │  【6】mock-server.js
     │  ├─ await wait(randomLatency())  [150~400ms待機]
     │  ├─ handleEquipmentCommand(body) 実行
     │  ├─ バリデーション
     │  │  ├─ stockerId 存在チェック
     │  │  ├─ connectionStatus チェック (OFFLINE→ERR-001)
     │  │  ├─ carrierId 重複チェック (ERR-002)
     │  │  ├─ shelf 占有チェック (ERR-002)
     │  │  └─ source 空チェック (ERR-003)
     │  ├─ すべてOK → db.jobs.push(...)
     │  ├─ db.logs.unshift(...)
     │  ├─ persist()  [sessionStorageに保存]
     │  └─ ok({success: true, jobId: "JOB202600001", message: "..."})
     │     ↓
     └─ 返却：{ ok: true, status: 200, json: async () => {...} }
        ↓
【7】finalize() で統一形式に変換
     ↓
【8】呼び出し元の handleDispatchClick() で受け取り
     ↓
【9】res.ok && res.data?.success をチェック
     ├─ true → toast表示 + carrierIdInput.value = ""
     │  ↓
     └─ false → alert表示（[${errorCode}] ${message}）
        ↓
【10】await refreshStockerStatus()
      ↓
      AmhsCore.getStockers() → 最新データ取得
      ↓
      Stockers キャッシュ更新
      ↓
      画面表示更新（バッジ色・インターロック状態など）
      ↓
【11】btnDispatch.disabled = false  [再度クリック可能に]
```

---

## エラーハンドリングとバリデーション

### バリデーション層の3段階

```
┌─────────────────────────────────────────┐
│  【フロントエンド：page-index.js】        │
│  ユーザー入力の即時チェック              │
│  ├─ 空文字列チェック                    │
│  ├─ 装置稼働状態チェック（F-007）        │
│  └─ ボタンの有効/無効制御               │
└──────────────────┬──────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│  【バックエンド：mock-server.js/API】    │
│  サーバー側の業務ロジック検証             │
│  ├─ ERR-001: OFFLINE チェック           │
│  ├─ ERR-002: キャリアID重複チェック     │
│  ├─ ERR-002: 棚占有チェック             │
│  ├─ ERR-003: 出庫元空チェック           │
│  └─ → JSON エラーレスポンス返却         │
└──────────────────┬──────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│  【フロントエンド：page-index.js】        │
│  レスポンス判定とユーザー通知            │
│  ├─ res.ok = true → 成功処理            │
│  └─ res.ok = false → alert/showAlert()  │
└─────────────────────────────────────────┘
```

### エラーメッセージの対応表（設計仕様書 § 6 エラー設計）

**UI-001**: キャリアID未入力エラー
```javascript
if (!carrierId) {
    showAlert("キャリアIDを入力してください");
}
```

**UI-004**: オフラインエラー（F-007）
```javascript
if (stocker.connectionStatus === "OFFLINE") {
    showAlert("装置がオフラインです");
    btnDispatch.disabled = true;
}
```

**UI-008**: ジョブ削除失敗
```javascript
if (!res.ok) {
    alert("ジョブ削除に失敗しました");  // UI-008
}
```

**UI-009**: データ取得失敗
```javascript
if (!res.ok) {
    logList.innerHTML = `<p class="text-danger">状態取得失敗</p>`;  // UI-009
}
```

**API側エラーコード:**

| コード | メッセージ | 対応箇所 | 処理 |
|-------|----------|--------|------|
| ERR-001 | 装置がオフラインです | `handleEquipmentCommand()` | HTTP 400 badRequest |
| ERR-002 | キャリアID重複 / 二重保管エラー | `handleEquipmentCommand()` | HTTP 400 badRequest |
| ERR-003 | 出庫時ソース空異常 | `handleEquipmentCommand()` | HTTP 400 badRequest |
| ERR-004 | (予約) | | |

---

## モックサーバーの永続化メカニズム

### 「なぜpageを遷移するとデータが消える」という問題の解決

#### 問題状況

```
1. Index タブで dispatch → db.jobs に新しいジョブが追加
2. Jobs タブをクリック（Razor Pages ページ遷移）
3. ページが完全にリロードされる
4. mock-server.js の <script> が再度実行される
5. const db = {...} が初期化される【ここでジョブが消える】
6. Jobs タブでgetActiveJobs()を呼び出す
7. 空の db.jobs を返す【新しいジョブが見えない】
```

#### 解決法：sessionStorage の活用

```javascript
// 【初期化時】
function loadDb() {
    try {
        const raw = sessionStorage.getItem("amhsMockDb");
        if (raw) {
            return JSON.parse(raw);  // ← 前回の状態を復元
        }
    } catch (e) {
        console.warn("[MockServer] sessionStorageの読み込みに失敗");
    }
    // 保存データがなければシードから新規作成
    return JSON.parse(JSON.stringify(seedDb));
}

const db = loadDb();  // ← 初回 or 復元
```

```javascript
// 【変更後】
function persist() {
    sessionStorage.setItem("amhsMockDb", JSON.stringify(db));
}

// handleEquipmentCommand() 内で
db.jobs.push({...});
persist();  // ← sessionStorage に保存
```

#### 修正後のフロー

```
1. Index タブで dispatch → db.jobs に追加
2. persist() で sessionStorage["amhsMockDb"] に保存
3. Jobs タブをクリック（ページ遷移）
4. mock-server.js 再実行
5. loadDb() が sessionStorage["amhsMockDb"] から復元 ← ← ← ここ！
6. db が前回と同じ state を保持
7. getActiveJobs() が新しく追加されたジョブを返す ✓
```

#### sessionStorage のライフサイクル

```
同じブラウザタブ内での複数ページ遷移 → 永続化される ✓
ブラウザタブを閉じる              → クリア
Ctrl+Shift+R（ハードリフレッシュ）  → クリア
```

---

## まとめ：全体の統合

### 設計仕様書の主要要件の実装確認

| 要件 | ファイル | 実装内容 |
|------|--------|--------|
| **F-001** | `page-stockers.js`, `amhs-core.js` | `getStockers()` で全ストッカー取得・表示 |
| **F-002** | `page-index.js`, `amhs-core.js` | `postEquipmentCommand()` で TRANSFER 命令送信 |
| **F-003** | `mock-server.js`, `page-index.js` | バリデーション実装 + エラーメッセージ表示 |
| **F-004** | `page-jobs.js`, `amhs-core.js` | `getActiveJobs()` でアクティブジョブ表示・削除 |
| **F-005** | `page-shelves.js`, `amhs-core.js` | `filterActiveStockers()` で Active のみフィルタ |
| **F-006** | `page-history.js`, `amhs-core.js` | `getRecentLogs()` で最新10件ログ表示 |
| **F-007** | `page-index.js`, `amhs-core.js` | `isDispatchDisabled()` で OFFLINE/TRAVELING 時に無効化 |
| **F-009** | `page-stockers.js`, `amhs-core.js` | Bootstrapカード形式でストッカー一覧表示 |
| **SCR-001~005** | `Pages/*.cshtml`, `wwwroot/js/page-*.js` | 5つの画面すべて実装完了 |
| **PWA** | `manifest.json`, `sw.js`, `_Layout.cshtml` | オフラインキャッシュ + ホーム画面追加対応 |

### テスト検証ポイント

1. **Index ページ上で**
   - Stocker dropdown が Active のみ表示される（F-005）
   - Connection/Operation バッジが 3秒ごと更新される
   - dispatch ボタンが OFFLINE/TRAVELING 時に無効化される（F-007）
   - dispatch 成功時は Jobs タブに即座にジョブが表示される（sessionStorage永続化）
   - エラー時は alert で ERR-xxx メッセージが表示される（F-003）

2. **Jobs ページ上で**
   - PENDING/RUNNING ジョブのみが表示される
   - delete ボタンで削除可能

3. **Shelves ページ上で**
   - Active ストッカーのみをドロップダウンに表示（R-002）
   - グリッドが空き/占有で色分け表示される

4. **History ページ上で**
   - 最新10件のログが時系列逆順で表示される

---

**本ドキュメントは、デジタル化による知識移転、およびチームメンバー・トレーナーへの説明資料として活用してください。**
