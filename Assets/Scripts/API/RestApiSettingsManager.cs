using FM.Core.Data;
using FM.Unity;

namespace API
{
    public struct RestApiSettings : ISettings
    {
        public int Port;
        
        public void InitializeDefault()
        {
            Port = 9123;
        }
    }
    
    public class RestApiSettingsManager : BaseXmlSettingsManager<RestApiSettings>
    {
        public RestApiSettingsManager()
            : base(new UnityApplicationLocationProvider(), "RestApiSettings")
        {
        }
    }
}