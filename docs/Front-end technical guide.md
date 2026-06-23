# フロントエンド担当内容

## 担当概要

本プロジェクトにおいて、私はスマートフォン向けオペレータ端末のフロントエンド開発を担当した。

主な担当範囲は以下の通りである。

* Razor Pagesによる画面作成
* Bootstrapを利用したスマートフォンUI設計
* JavaScriptによる画面制御
* REST API連携
* 3秒周期ポーリング処理
* エラーハンドリング
* PWA対応

---

# システム利用イメージ

オペレータはスマートフォンから搬送指示を実行する。

システムはストッカー状態を監視しながら、ジョブの登録・実行・完了を管理する。

```mermaid
flowchart LR

Operator[オペレータ]

Mobile[スマホ画面]

API[ストッカー制御API]

DB[(Database)]

Stocker[ストッカーシミュレータ]

Operator --> Mobile

Mobile --> API

API --> DB

Stocker --> API
```

---

# 画面遷移

```mermaid
flowchart TD

Main[メイン操作画面]

Stockers[ストッカー一覧]

Jobs[ジョブ監視]

Shelves[棚・在庫監視]

History[履歴ログ]

Main --> Stockers
Main --> Jobs
Main --> Shelves
Main --> History

Stockers --> Main
Jobs --> Main
Shelves --> Main
History --> Main
```

---

# フロントエンド構成

共通レイアウトを利用し、全画面で同じヘッダーとナビゲーションを利用する。

```mermaid
flowchart TD

Layout[_Layout.cshtml]

Index[Index.cshtml]

Stockers[Stockers.cshtml]

Jobs[Jobs.cshtml]

Shelves[Shelves.cshtml]

History[History.cshtml]

JS[amhs-core.js]

Layout --> Index
Layout --> Stockers
Layout --> Jobs
Layout --> Shelves
Layout --> History

Index --> JS
Stockers --> JS
Jobs --> JS
Shelves --> JS
History --> JS
```

---

# 搬送指示処理

オペレータが搬送指示を実行した際の流れ

```mermaid
sequenceDiagram

actor Operator

participant UI as メイン画面

participant API as Controller API

participant DB as Database

Operator->>UI: CarrierID入力

Operator->>UI: Dispatch押下

UI->>UI: 入力チェック

UI->>API: POST /api/equipment/command

API->>DB: Job登録

DB-->>API: Success

API-->>UI: Job作成完了

UI-->>Operator: 完了表示
```

---

# 状態監視ポーリング

画面更新を行わなくても、装置状態を自動監視する。

```mermaid
sequenceDiagram

participant Browser as スマートフォン画面
participant API as ストッカー制御API

loop 3秒毎

Browser->>API: GET /api/equipment/status

API-->>Browser: ConnectionStatus<br/>OperationState

Browser->>Browser: UI更新

end
```

JavaScriptでは以下のように実装している。

```javascript
setInterval(
  refreshStatus,
  3000
);
```

---

# JavaScriptの役割

画面表示だけではなく、業務ロジックも担当している。

```mermaid
flowchart TD

Status[状態取得]

Dispatch[搬送指示]

Jobs[ジョブ取得]

Inventory[棚取得]

History[履歴取得]

Safety[安全制御]

Status --> Safety
```

主な関数

* refreshStatus()
* dispatchJob()
* loadJobs()
* loadStockers()
* loadShelves()
* loadHistory()
* updateSafetyInterlock()

---

# 安全インターロック

ストッカーがOFFLINE状態の場合、誤操作防止のため操作を禁止する。

```mermaid
flowchart TD

A[Connection Status]

A -->|ONLINE| B[操作可能]

A -->|OFFLINE| C[DISPATCH無効]

A -->|OFFLINE| D[START無効]

A -->|OFFLINE| E[STOP無効]
```

---

# エラーハンドリング

フロントエンドでは入力エラーと通信エラーを処理する。

```mermaid
flowchart TD

Input[入力]

Input --> Check{入力OK?}

Check -->|No| Error1[入力エラー表示]

Check -->|Yes| API

API --> Success[正常完了]

API --> Fail[通信エラー表示]
```

---

# 実装成果

* スマートフォン向け5画面実装
* 共通レイアウト作成
* REST API連携
* JavaScript共通モジュール化
* 3秒ポーリング監視
* 安全インターロック実装
* エラーハンドリング実装
* PWA対応

---

# 学習できたこと

* Razor Pages設計
* JavaScript非同期通信(Fetch API)
* REST API連携
* ポーリング方式によるリアルタイム監視
* フロントエンドアーキテクチャ設計
* 半導体AMHSシステムの基本構造

```
```
