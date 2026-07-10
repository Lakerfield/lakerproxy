namespace LakerProxy.WebApi.Models
{
  public class Server
  {
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string AcmeChallengeZone { get; set; } = "";

    public ServerServices Services { get; set; }

    /// <summary>
    /// AcmeAccount code
    /// </summary>
    public string AcmeAccount { get; set; } = "";

    public string Comments { get; set; } = "";



    public Server Clone()
    {
      return (Server)MemberwiseClone();
    }
  }
}
