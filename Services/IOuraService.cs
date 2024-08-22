using lionheart.Model.Oura;
namespace lionheart.Services
{
    public interface IOuraService
    {
        Task SyncOuraAPI(string userID, DateOnly date, int daysPrior);
        Task<FrontendDailyOuraInfo?> GetDailyOuraInfoAsync(string userID, DateOnly date);
    }
}