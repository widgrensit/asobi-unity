using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    internal interface IHttpClient
    {
        string AccessToken { get; set; }

        Task<T> Get<T>(string path, Dictionary<string, string> query = null);
        Task<T> Post<T>(string path, object body = null);
        Task<T> PostJson<T>(string path, string json);
        Task<T> Put<T>(string path, object body = null);
        Task<T> PutJson<T>(string path, string json);
        Task<string> GetRaw(string path, Dictionary<string, string> query = null);
        Task<string> PutRaw(string path, string json);
        Task<AsobiResponse> Delete(string path, object body = null, Dictionary<string, string> query = null);
    }
}
