namespace LakerProxy.WebApi.Models
{
  public class ApiUser
  {
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
    public ApiUserRights Rights { get; set; } = ApiUserRights.None;

    public string Hostname { get; set; } = "";
    public string Server { get; set; } = "";

    public string ApiKey { get; set; }

    public string AcmeAccount { get; set; } = "";

    public string Comments { get; set; } = "";



    public ApiUser Clone()
    {
      return (ApiUser)MemberwiseClone();
    }
  }
}
