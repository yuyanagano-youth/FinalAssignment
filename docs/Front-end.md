sequence diagram
    autonumber
    participant HMI as HMI PWA Terminal (Client)
    participant Core as stkc-core.js (Abstract Layer)
    participant API as Backend REST API (Server)
    participant DB as Factory Control DB

    Note over HMI, DB: 画面起動 (DOMContentLoaded) またはタブ切り替え
    HMI->>Core: タイマー起動 (setInterval 3000ms)
    loop 3秒ごとの定期実行
        Core->>API: GET /api/front/stockers リクエスト送信
        API->>DB: 現在の通信状態・稼働ステータス問い合わせ
        DB-->>API: 状態レコード配列返却
        alt 正常通信時 (HTTP 200 OK)
            API-->>Core: JSONデータ返却 (stockerId, operationState, connectionStatus)
            Core->>HMI: DOM要素初期化 (innerHTML="") ＆ テーブル行の動的レンダリング
            Note over HMI: 選択中の装置が「OFFLINE」の場合、<br>UI-004警告表示 ＆ 操作ボタンを無効化 (安全インターロック)
        else 通信例外発生 / タイムアウト (HTTP 500 等)
            API-->>Core: エラー応答 or 応答なし
            Core->>HMI: コンソールログへ「[UI-005/007] 接続失敗」をプッシュ ＆ 警告ポップアップ
        end
    end
