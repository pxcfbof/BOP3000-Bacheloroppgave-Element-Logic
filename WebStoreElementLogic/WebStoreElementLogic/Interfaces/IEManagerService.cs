using WebStoreElementLogic.Entities;
using System.Net.Http.Headers;
public interface iEManagerService
{
    public void addAuth();
    public Task<HttpResponseMessage> Post(string endpoint, string xml);
    public Task<bool> ProductInformation(Product product);
}