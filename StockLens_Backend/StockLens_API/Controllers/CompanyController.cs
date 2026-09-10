using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StockLens_BusinessLayer.DTOs;
using StockLens_BusinessLayer.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockLens_API.Controllers
{
    [ApiController]
    [Route("api/companies")]
    [Produces("application/json")]
    public class CompanyController : ControllerBase
    {
        private readonly ICompanyService _companyService;

        public CompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        /// <summary>
        /// Searches for companies by symbol or company name.
        /// </summary>
        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<CompanyDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchCompanies([FromQuery] string query, [FromQuery] int limit = 10)
        {
            var companies = await _companyService.SearchCompaniesAsync(query, limit);
            return Ok(companies);
        }
    }
}
