namespace LakerProxy.WebApi.Models
{
  public class ApiUser
  {
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
    public ApiUserRights Rights { get; set; } = ApiUserRights.None;

    public string Hostname { get; set; } = "";
    public string Server { get; set; } = "";

    /// <summary>
    /// Hostname of the single website this user is bound to; required for the
    /// UploadWebsiteData right (website zip upload only works for this website).
    /// </summary>
    public string Website { get; set; } = "";

    public string ApiKey { get; set; }

    public string AcmeAccount { get; set; } = "";

    public string Comments { get; set; } = "";



    public ApiUser Clone()
    {
      return (ApiUser)MemberwiseClone();
    }
  }
}
