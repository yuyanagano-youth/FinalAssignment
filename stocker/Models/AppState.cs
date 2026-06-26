using stocker.Enums;

namespace stocker.Models
{
    public static class AppState
    {
        public static ConnectionStatus ConnectionStatus { get; set; } = ConnectionStatus.OFFLINE;

        public static OperationState OperationState { get; set; } = OperationState.IDLE;

        public static string? CurrentJobId { get; set; }

        public static string? AcceptedJobId { get; set; }

        public static CancellationTokenSource? CancellationTokenSource { get; set; }
    }
}
