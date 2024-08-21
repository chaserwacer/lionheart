using lionheart.Model.Oura;
namespace lionheart.Services
{
    public interface IOuraService
    {
        Task SyncOuraAPI(string userID, DateOnly date, int daysPrior);
        Task<DailyOuraInfo?> GetDailyOuraInfoAsync(string userID, DateOnly date);
    }
}