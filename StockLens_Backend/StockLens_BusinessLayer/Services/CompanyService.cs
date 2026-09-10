using AutoMapper;
using StockLens_BusinessLayer.DTOs;
using StockLens_BusinessLayer.Interfaces;
using StockLens_DataLayer.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly IMapper _mapper;

        public CompanyService(ICompanyRepository companyRepository, IMapper mapper)
        {
            _companyRepository = companyRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CompanyDto>> SearchCompaniesAsync(string query, int limit = 10)
        {
            var companies = await _companyRepository.SearchCompaniesAsync(query, limit);
            return _mapper.Map<IEnumerable<CompanyDto>>(companies);
        }
    }
}
