namespace LakerProxy.WebApi.Models
{
  public class AcmeAccount
  {
    public string Id { get; set; } = "";

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AcmeAccountTypes Type { get; set; }
    public string AcmeDiscoveryUrl { get; set; }

    public bool Default { get; set; }

    public string Mail { get; set; } = "";
  }
}
