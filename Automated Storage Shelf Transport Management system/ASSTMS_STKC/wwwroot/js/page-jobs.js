/**
 * ============================================================================
 * page-jobs.js
 * 【SCR-004】ジョブ監視画面 (Queue Monitor) のページロジック。
 * ============================================================================
 */
(() => {
    const REFRESH_INTERVAL_MS = 3000;
    const jobList = document.getElementById("jobList");
    const btnRefresh = document.getElementById("btnRefreshJobs");
    let pollTimerId = null;

    function escapeHtml(str) {
        return String(str).replace(/[&<>"']/g, (c) => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
        }[c]));
    }

    function jobBadgeClass(status) {
        switch (status) {
            case AmhsCore.Status.Job.PENDING: return "badge-job-pending";
            case AmhsCore.Status.Job.RUNNING: return "badge-job-running";
            case AmhsCore.Status.Job.COMPLETED: return "badge-job-completed";
            case AmhsCore.Status.Job.ABORTED: return "badge-job-aborted";
            default: return "bg-secondary";
        }
    }

    function renderJobs(jobs) {
        jobList.innerHTML = jobs.map(j => `
            <div class="amhs-card d-flex justify-content-between align-items-center">
                <div>
                    <div class="fw-bold">${escapeHtml(j.jobId)} <span class="badge ${jobBadgeClass(j.status)}">${escapeHtml(j.status)}</span></div>
                    <div class="small text-muted">${escapeHtml(j.stockerId)} / Carrier: ${escapeHtml(j.carrierId)}</div>
                    <div class="small text-muted">${escapeHtml(j.source)} → ${escapeHtml(j.destination)}</div>
                </div>
                <button class="btn btn-sm btn-outline-danger btn-delete-job" data-job-id="${escapeHtml(j.jobId)}">
                    <i class="bi bi-trash3"></i>
                </button>
            </div>
        `).join("") || `<p class="text-muted text-center mt-4">アクティブなジョブはありません</p>`;

        jobList.querySelectorAll(".btn-delete-job").forEach(btn => {
            btn.addEventListener("click", () => handleDelete(btn.dataset.jobId));
        });
    }

    async function loadJobs() {
        const res = await AmhsCore.getActiveJobs(); // 全ストッカー分を取得（stockerId省略）
        if (!res.ok) {
            jobList.innerHTML = `<p class="text-danger text-center mt-4">状態取得失敗</p>`; // UI-009
            return;
        }
        renderJobs(res.data || []);
    }

    async function handleDelete(jobId) {
        if (!confirm(`ジョブ ${jobId} を削除します。よろしいですか？`)) return;

        const res = await AmhsCore.deleteJob(jobId);
        if (!res.ok) {
            alert("ジョブ削除に失敗しました"); // UI-008
        }
        await loadJobs();
    }

    function startPollingLoop() {
        if (pollTimerId) clearInterval(pollTimerId);
        pollTimerId = setInterval(loadJobs, REFRESH_INTERVAL_MS);
    }

    btnRefresh.addEventListener("click", loadJobs);
    document.addEventListener("DOMContentLoaded", async () => {
        await loadJobs();
        startPollingLoop();
    });
})();
