# AMHS フロントエンド実装 - プレゼンテーション資料

## 💡 導入：全体像

### プロジェクト背景
- **システム名:** 自動保管棚管理システム（AMHS）
- **対象デバイス:** Android スマートフォン（16:9 縦向き）
- **技術スタック:** ASP.NET Core Razor Pages + JavaScript + PWA

### チームの役割分担
```
┌─────────────────────────────────────────┐
│ 私（フロントエンド）                     │
│ Pages/*.cshtml, wwwroot/*, CSS, PWA    │
└────────────────────┬────────────────────┘
                     ↓ API 呼び出し
┌─────────────────────────────────────────┐
│ パートナーA（バックエンド/SQL）         │
│ Controllers/*.cs, Models, Services      │
└────────────────────┬────────────────────┘
                     ↑ JSON レスポンス
┌─────────────────────────────────────────┐
│ パートナーB（コンソール/ハートビート）   │
│ C# Console App, 3秒ごとのポーリング     │
└─────────────────────────────────────────┘
```

### 実装のポイント
- **フロント/バック完全分離**: PageModel は空。全データ取得は JavaScript の fetch()
- **`USE_MOCK_API` フラグで切り替え**: 1行変更で実装モード ↔ 本番モード
- **`sessionStorage` で永続化**: Razor Pages のページ遷移後もテストデータを保持

---

## 🎯 設計仕様書との対応

### 機能要件（F-001 ~ F-009）の実装

#### F-001: ストッカー一覧取得
```javascript
// amhs-core.js
async function getStockers() {
    return request("GET", "/api/stockers");  // GET /api/stockers
}

// page-stockers.js で使用
const res = await AmhsCore.getStockers();
```
**実装済み画面:** Stockers タブ

---

#### F-002: 搬送指示（TRANSFER）
```javascript
// amhs-core.js
async function postEquipmentCommand(payload) {
    return request("POST", "/api/equipment/command", payload);
}

// page-index.js での呼び出し
const res = await AmhsCore.postEquipmentCommand({
    command: "TRANSFER",
    stockerId: "STK001",
    carrierId: "CST-1001",
    source: "IN_PORT",
    destination: "SHELF_A1"
});
```
**実装済み画面:** Main（Index）タブ

---

#### F-003: 異常判定・バリデーション
```javascript
// mock-server.js での実装例
if (stocker.connectionStatus === "OFFLINE") {
    return badRequest({ errorCode: "ERR-001", message: "装置がオフラインです" });
}

const duplicate = db.jobs.some(j => j.carrierId === body.carrierId && ...);
if (duplicate) {
    return badRequest({ errorCode: "ERR-002", message: "Job creation failed: ..." });
}
```
**エラーコード:**
- `ERR-001`: 装置がオフライン
- `ERR-002`: キャリアID重複 / 二重保管
- `ERR-003`: 出庫元空エラー

---

#### F-004: ジョブ監視・削除
```javascript
// amhs-core.js
async function getActiveJobs(stockerId) {
    return request("GET", `/api/jobs/active?stockerId=${encodeURIComponent(stockerId)}`);
}

async function deleteJob(jobId) {
    return request("DELETE", `/api/jobs/${encodeURIComponent(jobId)}`);
}
```
**実装済み画面:** Jobs タブ

---

#### F-005: 棚・在庫（Active フィルタ）
```javascript
// amhs-core.js
function filterActiveStockers(stockers) {
    return (stockers || []).filter(s => s.status === Status.Stocker.ACTIVE);
}

// page-shelves.js での使用
const activeOnly = AmhsCore.filterActiveStockers(allStockers);
```
**実装済み画面:** Shelves タブ（R-002 対応）

---

#### F-006: 履歴ログ表示
```javascript
// amhs-core.js
async function getRecentLogs(stockerId) {
    const qs = stockerId ? `?stockerId=${encodeURIComponent(stockerId)}` : "";
    return request("GET", `/api/logs/recent${qs}`);
}
```
**実装済み画面:** History タブ

---

#### F-007: 安全インターロック（OFFLINE/TRAVELING 時に無効化）
```javascript
// amhs-core.js
function isDispatchDisabled(stocker) {
    if (!stocker) return true;
    return stocker.connectionStatus === Status.Connection.OFFLINE ||
           stocker.operationState === Status.Operation.TRAVELING;
}

// page-index.js での適用
btnDispatch.disabled = AmhsCore.isDispatchDisabled(stocker);
```
**効果:** OFFLINE または TRAVELING 状態では「搬送指示実行」ボタンが灰色無効化

---

#### F-009: ストッカー一覧（Bootstrapカード表示）
```javascript
// page-stockers.js
function renderCards(stockers) {
    return stockers.map(s => `
        <div class="amhs-card stocker-card ${s.connectionStatus === "OFFLINE" ? "is-offline" : ""}">
            <!-- カード内容 -->
        </div>
    `);
}
```
**実装済み画面:** Stockers タブ

---

### 画面設計（SCR-001 ~ SCR-005）

| 画面 | 対応仕様書 | 実装内容 |
|------|----------|--------|
| **SCR-001: Main（メイン操作）** | ディスパッチコンソール | 搬送指示・E-STOP・ポーリング監視 |
| **SCR-002: Stocker List** | ストッカー一覧 | カード形式・OFFLINE グレーアウト |
| **SCR-003: Shelves（棚・在庫）** | インベントリグリッド | 3x3グリッド・色分け |
| **SCR-004: Jobs（ジョブ監視）** | キューモニタ | PENDING/RUNNING のみ表示 |
| **SCR-005: History（履歴）** | イベントログ | 最新10件・ログレベル別色分け |

### ステータス語彙（大文字小文字の統一）

```javascript
const Status = {
    Connection: { ONLINE, OFFLINE },
    Operation: { IDLE, TRAVELING, LOAD_LOCK, UNLOAD_LOCK, ALARM, UNKNOWN },
    Job: { PENDING, RUNNING, COMPLETED, ABORTED },
    Stocker: { ACTIVE, RESERVED },
    LogLevel: { INFO, WARN, ALARM }
};
```
**重要:** C# の `StatusVocabulary.cs` と完全に同じ値を使用 → パース失敗を排除

---

## 🛠️ アーキテクチャの詳細

### 1. amhs-core.js — 決定ファイル（核）

**役割:** フロントエンドのすべての外部通信を統一管理

```javascript
const USE_MOCK_API = true;  // ← この1行で全挙動が変わる

async function request(method, path, body) {
    if (USE_MOCK_API) {
        // モード1: 仮想JSONサーバー（テスト用）
        const res = await MockServer.handle(method, path, body);
        return finalize(res);
    }
    
    // モード2: 実 HTTP API（本番用）
    const res = await fetch(`${API_BASE}${path}`, {
        method,
        headers: body ? { "Content-Type": "application/json" } : undefined,
        body: body ? JSON.stringify(body) : undefined
    });
    
    return finalize(res);
}
```

**ページ側からの呼び出し（内部を意識しない）:**
```javascript
// page-index.js
const res = await AmhsCore.postEquipmentCommand({...});
// ↑ このコードは USE_MOCK_API の値に関わらず同じ
```

**メリット:**
- バックエンド完成前後で、ページ側のコード変更なし
- テスト時はモック、本番は実 API を透過的に使い分け

---

### 2. mock-server.js — 仮想JSONサーバー

**役割:** バックエンド API を JavaScript で疑似実装

```javascript
const MockServer = (() => {
    const STORAGE_KEY = "amhsMockDb";  // ← 永続化キー
    
    // シードデータ（初期状態）
    const seedDb = { stockers, shelves, jobs, logs };
    
    // sessionStorage から復元
    function loadDb() {
        const raw = sessionStorage.getItem(STORAGE_KEY);
        if (raw) return JSON.parse(raw);
        return JSON.parse(JSON.stringify(seedDb));
    }
    
    // 変更を sessionStorage に保存
    function persist() {
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(db));
    }
    
    const db = loadDb();  // 初期化
    
    // ルーティング
    async function handle(method, path, body) {
        if (method === "GET" && path === "/api/stockers") {
            return ok(db.stockers);
        }
        if (method === "POST" && path === "/api/equipment/command") {
            return handleEquipmentCommand(body);  // バリデーション実装
        }
        // ... その他のエンドポイント ...
    }
})();
```

**重要な仕組み: sessionStorage 永続化**

```
ページ遷移前：
  dispatch 実行 → db.jobs に追加
             → persist() で sessionStorage に保存
  
ページ遷移（完全なリロード）

ページ遷移後：
  mock-server.js 再実行
  loadDb() が sessionStorage から復元
  → 新しく追加されたジョブが表示される ✓
```

---

### 3. page-*.js — 画面別ロジック

#### page-index.js（メイン画面）の例

**① 3秒ごとのポーリングループ:**
```javascript
const REFRESH_INTERVAL_MS = 3000;

document.addEventListener("DOMContentLoaded", async () => {
    // ステップ1: 初回即時実行
    await refreshStockerStatus();
    
    // ステップ2: 3秒ごと定期実行
    pollTimerId = setInterval(refreshStockerStatus, REFRESH_INTERVAL_MS);
});

async function refreshStockerStatus() {
    const res = await AmhsCore.getStockers();
    // 結果から選択中ストッカーの状態を抽出
    const current = allStockersCache.find(s => s.stockerId === selectedId);
    // バッジ・ボタン状態を更新
    applyInterlock(current);
}
```

**タイムライン:**
```
t=0秒:  ページ読込完了 → refreshStockerStatus() 実行 1回目
t=3秒:  自動実行 → refreshStockerStatus() 実行 2回目
t=6秒:  自動実行 → refreshStockerStatus() 実行 3回目
...
```

**② Dispatch ボタンのフロー:**
```javascript
btnDispatch.addEventListener("click", async () => {
    // 1. ユーザー入力検証
    if (!carrierId.value) {
        showAlert("キャリアIDを入力してください");
        return;
    }
    
    // 2. 装置状態チェック（F-007）
    if (AmhsCore.isDispatchDisabled(currentStocker)) {
        showAlert("装置がオフライン、または搬送中");
        return;
    }
    
    // 3. API 呼び出し
    const res = await AmhsCore.postEquipmentCommand({
        command: "TRANSFER",
        stockerId,
        carrierId,
        source,
        destination
    });
    
    // 4. レスポンス判定
    if (res.ok && res.data?.success) {
        showToast("ジョブの送信に成功しました");
        carrierIdInput.value = "";
    } else {
        showAlert(`[${res.data?.errorCode}] ${res.data?.message}`);
    }
});
```

---

### 4. CSS スタイル（site.css）— モバイル最適化

```css
/* モバイル（16:9 Android）優先のレイアウト */
:root {
    --app-header-h: 52px;
    --app-bottomnav-h: 64px;
}

.app-main {
    max-width: 480px;  /* ← 16:9 対応 */
    margin: 0 auto;
}

/* ステータス色 */
.badge-online   { background-color: #2e7d32; }  /* 緑: ONLINE */
.badge-offline  { background-color: #9e9e9e; }  /* グレー: OFFLINE */
.badge-alarm    { background-color: #c62828; animation: blink-alarm 1s infinite; }  /* 赤・点滅 */

/* 棚グリッド色分け */
.shelf-cell.empty    { background-color: #bdbdbd; }   /* 灰色 */
.shelf-cell.occupied { background-color: #fbc02d; }   /* 黄色 */
.shelf-cell.alarm    { background-color: #e53935; }   /* 赤 */
```

---

## 📱 PWA 実装

### なぜ PWA か？

**要件:**
- スマートフォンのホーム画面に追加可能
- オフラインでも App Shell（ナビ・レイアウト）を表示
- ネイティブアプリ風の操作感

### 3つの要素

#### 1. manifest.json（PWAマニフェスト）
```json
{
    "name": "自動保管棚管理システム",
    "start_url": "/Index",
    "display": "standalone",     // ← ブラウザUIを隠す
    "orientation": "portrait",   // ← 縦向き固定
    "icons": [...]
}
```
**効果:** ホーム画面から起動したとき、アドレスバーが表示されない

#### 2. sw.js（Service Worker）— オフラインキャッシュ
```javascript
// App Shell（HTML/CSS/JS）をプリキャッシュ
const SHELL_ASSETS = ["/", "/Index", "/css/site.css", "/js/amhs-core.js", ...];

self.addEventListener("fetch", (event) => {
    // /api/* はキャッシュせず常にネットワーク優先
    if (url.pathname.startsWith("/api/")) return;
    
    // その他はキャッシュから返す
    event.respondWith(
        caches.match(event.request) || fetch(event.request)
    );
});
```
**効果:** バックエンドが落ちていても画面フレームは表示される

#### 3. _Layout.cshtml での登録
```html
<link rel="manifest" href="~/manifest.json" />
<link rel="apple-touch-icon" href="~/icons/icon-192.png" />

<script>
    navigator.serviceWorker.register('/sw.js');
</script>
```

---

## 🔄 リアルタイムデータ更新のメカニズム

### Index 画面での完全なフロー

```
【1】ページロード
     ↓
【2】DOMContentLoaded イベント
     ↓
【3】refreshStockerStatus() を即時実行
     ├─ GET /api/stockers → stockers[] 取得
     ├─ filterActiveStockers() で Active のみ抽出
     ├─ ドロップダウン描画
     ├─ 選択中ストッカーの状態を抽出
     ├─ バッジ更新（conn / operation）
     └─ ボタン有効/無効（F-007）
     ↓
【4】startPollingLoop() で 3秒ごとの定期実行開始
     ↓
【5】ユーザーが「搬送指示実行」クリック
     ├─ handleDispatchClick() 実行
     ├─ POST /api/equipment/command を送信
     ├─ レスポンス待機
     └─ 成功/失敗を表示
     ↓
【6】画面自動更新
     └─ 次の 3秒ごとのポーリングで最新データを反映
```

### 各タブでの更新頻度

| タブ | 更新方法 | 説明 |
|------|--------|------|
| **Main** | 自動（3秒ごと） | ストッカー状態監視 |
| **Stockers** | 自動（3秒ごと） | OFFLINE/ONLINE 視覚フィードバック |
| **Jobs** | 自動（3秒ごと） | ジョブステータス更新 |
| **Shelves** | 手動変更時のみ＋ストッカー切替時 | リソース節約 |
| **History** | 手動「更新」ボタン のみ | 低優先度 |

---

## 🧪 テスト・デバッグ

### ポーリング通信テストパネル（Main タブ）

```
【ポーリング通信テスト】
├─ 「コンソール側ハートビートを1回疑似送信」
│  └─ POST /api/equipment/heartbeat を1回実行
│     → 応答ログに hasPendingJob が表示される
│
└─ ログパネル（黒背景）
   毎3秒のポーリング結果を記録：
   [14:05:23] GET /api/stockers OK (3件) 選択中:STK001 conn=ONLINE op=IDLE
   [14:05:26] GET /api/stockers OK (3件) 選択中:STK001 conn=ONLINE op=IDLE
   ...
```

**用途:** バックエンド実装前に、フロントエンド→API通信が正しく機能しているか確認

### ブラウザ DevTools でのテスト

```javascript
// コンソールで実行

// モックDB を確認
JSON.parse(sessionStorage.getItem("amhsMockDb")).jobs

// 仮想JSONサーバーをリセット
MockServer.resetToSeed()

// 手動 API 呼び出し
await AmhsCore.postEquipmentCommand({
    command: "TRANSFER",
    stockerId: "STK001",
    carrierId: "CST-9999",
    source: "IN_PORT",
    destination: "SHELF_A2"
})
```

---

## 🤝 チームメンバーとの連携

### バックエンド担当者へのお願い

**実装すべき7つのエンドポイント:**

| HTTP | パス | 返すべき JSON |
|------|------|-------------|
| GET | `/api/stockers` | `[{ stockerId, stockerName, status, connectionStatus, operationState, alarms }]` |
| GET | `/api/jobs/active?stockerId=...` | `[{ jobId, stockerId, carrierId, source, destination, status }]` |
| DELETE | `/api/jobs/{jobId}` | `{ success: true }` or HTTP 404 |
| GET | `/api/inventory/shelves?stockerId=...` | `[{ shelfName, carrierId, inTime }]` |
| GET | `/api/logs/recent?stockerId=...` | `[{ timestamp, level, message }]` |
| POST | `/api/equipment/command` | `{ success: true, jobId, message }` or `{ success: false, errorCode, message }` |
| POST | `/api/equipment/heartbeat` | `{ hasPendingJob: true/false, job: {...} }` |

**重要:** JSON フィールド名は **camelCase** で統一

### コンソール担当者へのお願い

**実装すべき内容:**
- 3秒ごとに `POST /api/equipment/heartbeat` を呼び出す
- ペイロード：`{ stockerId: "STK001", currentOperationState: "IDLE" }`
- レスポンス：`{ hasPendingJob: true, job: {...} }` を受け取り、ジョブ実行

**確認方法:** Main タブの「コンソール側ハートビートを1回疑似送信」ボタンで疑似テスト可能

---

## 📊 開発進捗イメージ

```
【現在】
├─ フロント: 完成 ✓
│  └─ Razor Pages + JavaScript + CSS + PWA すべて実装済み
│     USE_MOCK_API = true で仮想JSONサーバー で動作
│
├─ バック: 実装中
│  └─ Controllers/Models/Services 作成中
│     完成後、Program.cs で DI 登録
│
└─ コンソール: 実装中
   └─ C# ハートビート用ポーリング実装中

【統合テスト】
→ USE_MOCK_API = false に変更
→ 各タブでデータが正しく表示されるか確認
→ dispatch / E-STOP / 削除が正しく動作するか確認
```

---

## 🎓 知識トランスファー（まとめ）

### このコードベースで学べること

1. **フロント/バック分離アーキテクチャ**
   - PageModel は空、ビジネスロジックはクライアント側
   - API 契約を明確に定義

2. **フラグによる実装/本番切り替え**
   - `USE_MOCK_API` フラグで jest/storybook のような開発体験
   - 何千行のコード変更なし

3. **モックサーバー実装**
   - json-server の代わりに JavaScript で実装
   - sessionStorage で状態永続化

4. **PWA 基本実装**
   - manifest.json で app-like 体験
   - Service Worker でオフラインキャッシュ

5. **モバイル最適化**
   - CSS Grid / Flexbox
   - ボトムナビゲーション（タブUI）

