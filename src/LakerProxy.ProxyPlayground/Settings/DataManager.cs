using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace LakerProxy.ProxyPlayground.Settings
{
  public class DataManager : Caliburn.Micro.PropertyChangedBase
  {
    private const string SettingsFilename = "Data.xml";
    public readonly string SettingsFullname;

    private static DataManager _current;
    public static DataManager Current
    {
      get
      {
        if (_current == null)
          _current = new DataManager();
        return _current;
      }
    }

    private DataRoot _root;
    public DataRoot Root
    {
      get { return _root; }
      set
      {
        if (Equals(value, _root)) return;
        _root = value;
        NotifyOfPropertyChange();
      }
    }


    private DataManager()
    {
      SettingsFullname = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppIdentity.Paths.Publisher,
        AppIdentity.Paths.Application,
        SettingsFilename);

      Load();
    }

    public void New()
    {
      Root = new DataRoot();
      Root.ProxyServers.Add(new ProxyServer() { Omschrijving = "localhost", Url = "http://localhost/" });
    }

    public void Load()
    {
      try
      {
        Root = null;
        if (File.Exists(SettingsFullname))
          Root = DeSerializeObject<DataRoot>(SettingsFullname);
        if (Root == null)
          New();
      }
      catch (Exception exception)
      {
        System.Diagnostics.Process.Start("notepad.exe", SettingsFullname);
        MessageBox.Show(exception.Message, $"Error while reading configuration file {SettingsFullname}", MessageBoxButton.OK, MessageBoxImage.Warning);
        New();
      }
    }

    public void Save()
    {
      try
      {
        var fileInfo = new FileInfo(SettingsFullname);
        var directory = fileInfo.Directory;
        if (directory != null && !directory.Exists)
          directory.Create();

        Root.ProxyServers = new ObservableCollection<ProxyServer>(
          Root.ProxyServers
            .OrderBy(c => c.Groep)
            .ThenBy(c => c.Omschrijving));

        SerializeObject(Root, SettingsFullname);
      }
      catch (Exception ex)
      {
        System.Windows.MessageBox.Show(ex.Message, string.Format("Save of {0} failed", SettingsFilename));
      }
    }


    /// <summary>
    /// Serializes an object.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="serializableObject"></param>
    /// <param name="fileName"></param>
    public void SerializeObject<T>(T serializableObject, string fileName)
    {
      if (serializableObject == null) { return; }

      try
      {
        XmlDocument xmlDocument = new XmlDocument();
        XmlSerializer serializer = new XmlSerializer(serializableObject.GetType());
        using (MemoryStream stream = new MemoryStream())
        {
          serializer.Serialize(stream, serializableObject);
          stream.Position = 0;
          xmlDocument.Load(stream);
          xmlDocument.Save(fileName);
          stream.Close();
        }
      }
      catch (Exception)
      {
        //Log exception here
        throw;
      }
    }


    /// <summary>
    /// Deserializes an xml file into an object list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public T DeSerializeObject<T>(string fileName)
    {
      if (string.IsNullOrEmpty(fileName)) { return default(T); }

      T objectOut = default(T);

      try
      {
        string attributeXml = string.Empty;

        XmlDocument xmlDocument = new XmlDocument();
        xmlDocument.Load(fileName);
        string xmlString = xmlDocument.OuterXml;

        using (StringReader read = new StringReader(xmlString))
        {
          Type outType = typeof(T);

          XmlSerializer serializer = new XmlSerializer(outType);
          using (XmlReader reader = new XmlTextReader(read))
          {
            objectOut = (T)serializer.Deserialize(reader);
            reader.Close();
          }

          read.Close();
        }
      }
      catch (Exception)
      {
        //Log exception here
        throw;
      }

      return objectOut;
    }

  }
}
