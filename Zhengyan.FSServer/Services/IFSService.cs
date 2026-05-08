using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
namespace Zhengyan.FSServer.Services;

public interface IFSService
{
    Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken);
    Task<string> UploadFileAsync(IFormFile file, CancellationToken cancellationToken);
}