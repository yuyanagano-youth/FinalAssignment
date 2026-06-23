
# 自動保管棚搬送管理システム (AMHS Mobile HMI Terminal)

本プロジェクトは、半導体・液晶製造工場等のクリーンルーム内で稼働する「自動保管棚搬送管理システム（AMHS: Automated Material Handling System）」を、現場のオペレーターがモバイル端末からリアルタイムに監視・操作するための高信頼性フロントエンドPWA（Progressive Web App）およびRazor Pagesアプリケーションの実装モデルです。

---

## 1. システムアーキテクチャ (Architecture & Scope)

本システムは、限られた通信帯域やネットワーク切断（Wi-Fiデッドゾーン）が発生しやすい工場環境を想定し、**App Shellアーキテクチャ**を採用しています。クライアントサイドにコアとなる表示ロジック、エラーマスタ、および状態制御ロジックを持たせ、バックエンドAPIと高速な非同期通信（Polling）を行う分離型構成です。

```mermaid
graph TD
    subgraph Client_Side [PWA Client Terminal (Mobile HMI)]
        A[Shared Layout Shell] -->|Tab Route| B[SCR-001: メイン操作盤]
        A -->|Tab Route| C[SCR-002: ストッカー一覧]
        A -->|Tab Route| D[SCR-003: 棚・在庫ステータス]
        A -->|Tab Route| E[SCR-004: ジョブ監視]
        A -->|Tab Route| F[SCR-005: 履歴ログ]
        
        B & C & D & E & F -->|Event Link| G[wwwroot/js/app.js <br> UI Rendering & Interaction]
        G -->|Core Broker| H[wwwroot/js/stkc-core.js <br> API Abstract Wrapper & Mock Engine]
    end

    subgraph Server_Side [Backend & Factory Layer]
        I[Real Server REST API <br> /api/* Enpoints]
        J[Physical Stocker / Hardware Equipment]
        I <-->|Industrial Protocol| J
    end

    H -->|3000ms Interval Polling / FETCH / POST| I


