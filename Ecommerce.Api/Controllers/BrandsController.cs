using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BrandsController : Controller
    {
      
        private readonly IBrandRepository _brandRepository;

        public BrandsController(IBrandRepository brandRepository)
        {
            _brandRepository = brandRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Brand>>> GetAll()
        {
            var brands = await _brandRepository.GetAllActiveAsync();
            return Ok(brands);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<Brand>> GetById(long id)
        {
            var brand = await _brandRepository.GetByIdAsync(id);

            if (brand is null)
                return NotFound();

            return Ok(brand);
        }
    }
}
