namespace lionheart.WellBeing;
public class ApiAccessToken{
    public Guid ObjectID { get; set; }
    public Guid UserID {get; init;}
    public string ApplicationName { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;
}