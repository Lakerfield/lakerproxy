using System.Collections.Generic;

namespace LakerProxy.WebApi.Models
{
  /// <summary>
  /// RFC 7807
  /// https://tools.ietf.org/html/rfc7807
  /// </summary>
  public class ApiError
  {
    public string Type { get; set; }
    public string Title { get; set; }
    public int Status { get; set; }
    public string Detail { get; set; }
    public string Instance { get; set; }

    public List<ApiErrorContext> Context { get; set; }

    //{
    //  "type": "/errors/incorrect-user-pass",
    //  "title": "Incorrect username or password.",
    //  "status": 403,
    //  "detail": "Authentication failed due to incorrect username or password.",
    //  "instance": "/login/log/abc123"
    //}

    public ApiError(
      string instance,
      string type,
      string title,
      int status = 500,//StatusCodes.Status500InternalServerError,
      string detail = null)
    {
      Type = type;
      Title = title;
      Status = status;
      Detail = string.IsNullOrWhiteSpace(detail) ? title : detail;
      Instance = instance;

      Context = new List<ApiErrorContext>();
    }
  }

  public class ApiErrorContext
  {
    public string Name { get; set; }
    public string Value { get; set; }

    public ApiErrorContext(string name, string value)
    {
      Name = name;
      Value = value;
    }
  }

}
