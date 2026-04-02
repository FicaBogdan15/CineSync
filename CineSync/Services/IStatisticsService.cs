using CineSync.ViewModels;

namespace CineSync.Services
{
    public interface IStatisticsService
    {
        Task<StatisticsDashboardViewModel> GetDashboardAsync();
    }
}
