using GymShop.Application.DTOs.Products;
using GymShop.Application.UseCases.Products;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(IGetCategoriesUseCase getCategories) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategoryResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await getCategories.ExecuteAsync(cancellationToken));
}
