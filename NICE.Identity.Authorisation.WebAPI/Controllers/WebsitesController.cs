using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NICE.Identity.Authorisation.WebAPI.ApiModels;
using NICE.Identity.Authorisation.WebAPI.Services;

namespace NICE.Identity.Authorisation.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    public class WebsitesController: ControllerBase
    {
        private readonly ILogger<WebsitesController> _logger;
        private readonly IWebsitesService _websitesService;

        public WebsitesController(IWebsitesService websitesService, ILogger<WebsitesController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _websitesService = websitesService ?? throw new ArgumentNullException(nameof(websitesService));
        }
        
        /// <summary>
        /// create website
        /// </summary>
        /// <param name="website"></param>
        /// <returns></returns>
        [HttpPost("")]
        [ProducesResponseType(typeof(Website), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Consumes("application/json")]
        [Produces("application/json")]
        public IActionResult CreateWebsite([FromBody] Website website)
        {
            if (!ModelState.IsValid)
            {
                var serializableModelState = new SerializableError(ModelState);
                var modelStateJson = JsonConvert.SerializeObject(serializableModelState);
                _logger.LogError($"Invalid model for create user", modelStateJson);
                return BadRequest(new ProblemDetails {Status = 400, Title = "Invalid model for create website"});
            }

            try
            {
                var createdWebsite = _websitesService.CreateWebsite(website);
                return Created($"/website/{createdWebsite.WebsiteId.ToString()}", createdWebsite);
            }
            catch (Exception e)
            {
                return StatusCode(500, new ProblemDetails {Status = 500, Title = e.Message, Detail = e.InnerException?.Message});
            }
        }
    }
}