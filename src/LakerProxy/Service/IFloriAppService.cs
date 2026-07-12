using System.Collections.Generic;
using System.Threading.Tasks;
using Lakerfield;
using Lakerfield.Storage;
using LakerProxy.Models;

namespace LakerProxy.Service
{

  public interface ILogCollection : IBaseCollection<Log>
  {
    Log New(string? message = default);
    Task Insert(Log entity, IAppTransaction? transaction = default);
    IDataObservable<Log> Observe(Log.Filter filter);
    Task<IEnumerable<Log>> FindBy(Log.Filter filter);
  }

  public interface IApiUserCollection : IBaseObservableCollection<ApiUser>, ISaveToCollection<ApiUser>, IDeleteFromCollection<ApiUser>
  {
    Task<ApiUser?> Login(string apiKey);
  }

  public interface IServerCollection : IBaseObservableCollection<Server>, ISaveToCollection<Server>
  {
    Task<Server> Find(string name);
    Task<Server> FindOrCreate(string name, ServerServices services = ServerServices.None);
  }

  public interface IWebsiteCollection : IBaseObservableCollection<Website>, ISaveToCollection<Website>, IDeleteFromCollection<Website>
  {
    Task<Website?> FindByHostname(string name);
  }

  public interface IAcmeAccountCollection : IBaseObservableCollection<AcmeAccount>, ISaveToCollection<AcmeAccount>
  {
    Task<AcmeAccount?> FindDefault();
  }

  public interface IWebsiteDataCollection : IBaseCollection<WebsiteData>, ISaveToCollection<WebsiteData>
  {
    Task<WebsiteData?> GetLatest();
    Task DeleteOldest();
  }



  public interface IRawCollection
  {
    Task<string> DbStats();
    Task<string> HostInfo();
    Task<string> MonitoringStatus();
    Task<string> Top();
    Task<string> Ping();
  }

}
