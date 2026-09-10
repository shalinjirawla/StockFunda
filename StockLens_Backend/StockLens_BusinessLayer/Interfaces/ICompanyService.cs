using StockLens_BusinessLayer.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Interfaces
{
    public interface ICompanyService
    {
        Task<IEnumerable<CompanyDto>> SearchCompaniesAsync(string query, int limit = 10);
    }
}
