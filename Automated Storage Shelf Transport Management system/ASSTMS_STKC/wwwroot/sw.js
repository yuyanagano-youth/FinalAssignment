/**
 * ============================================================================
 * sw.js
 * 【PWA: Service Worker】
 * ----------------------------------------------------------------------------
 * App Shell（レイアウト・CSS・JS・アイコン）を事前キャッシュし、
 * オフライン時や通信不安定時でも画面の枠組みが表示できるようにする。
 * ※ /api/* へのリクエストはキャッシュ対象外とし、常に最新データを取得する
 *    （在庫・ジョブ状態などのリアルタイム性を損なわないため）。
 * ============================================================================
 */

const CACHE_NAME = "amhs-shell-cache-v1";

// App Shellを構成する静的アセット一覧（初回インストール時にキャッシュする）
const SHELL_ASSETS = [
    "/",
    "/Index",
    "/Stockers",
    "/Jobs",
    "/Shelves",
    "/History",
    "/css/site.css",
    "/js/amhs-core.js",
    "/js/mock-server.js",
    "/js/page-index.js",
    "/js/page-stockers.js",
    "/js/page-jobs.js",
    "/js/page-shelves.js",
    "/js/page-history.js",
    "/manifest.json",
    "/icons/icon-192.png",
    "/icons/icon-512.png"
];

// ---- インストール時: App Shellアセットをプリキャッシュ ----
self.addEventListener("install", (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_ASSETS))
    );
    self.skipWaiting();
});

// ---- 有効化時: 古いキャッシュバージョンを削除 ----
self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches.keys().then((keys) =>
            Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k)))
        )
    );
    self.clients.claim();
});

// ---- フェッチ時: API通信はキャッシュせず常にネットワーク優先、それ以外はCache-First ----
self.addEventListener("fetch", (event) => {
    const url = new URL(event.request.url);

    // /api/ 配下はリアルタイム性が重要なため、Service Workerでは介入しない
    if (url.pathname.startsWith("/api/")) {
        return; // ブラウザの標準fetchに委ねる
    }

    event.respondWith(
        caches.match(event.request).then((cached) => {
            if (cached) return cached;
            return fetch(event.request).catch(() =>
                // オフライン時、ナビゲーション要求にはApp Shell(トップ)を返す
                event.request.mode === "navigate" ? caches.match("/") : undefined
            );
        })
    );
});
