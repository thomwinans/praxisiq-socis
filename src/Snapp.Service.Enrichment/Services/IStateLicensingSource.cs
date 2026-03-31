using Snapp.Service.Enrichment.Models;

namespace Snapp.Service.Enrichment.Services;

public interface IStateLicensingSource
{
    Task<List<StateLicensingRecord>> GetLicensesAsync();
}
