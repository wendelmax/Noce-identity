using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NICE.Identity.Authentication.Sdk.Authorisation;
using NICE.Identity.Authorisation.WebAPI.ApiModels;
using NICE.Identity.Authorisation.WebAPI.Services;

namespace NICE.Identity.Authorisation.WebAPI.Controllers
{

    [Route("api/[controller]")]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = Policies.API.UserAdministration)]
	[ApiController]
    public class ServicesController : ControllerBase
    {
        private readonly ILogger<ServicesController> _logger;
        private readonly IServicesService _servicesService;
        
        public ServicesController(IServicesService servicesService, ILogger<ServicesController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _servicesService = servicesService ?? throw new ArgumentNullException(nameof(servicesService));
        }
        
        /// <summary>
        /// create service
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        [HttpPost("")]
        [ProducesResponseType(typeof(Service), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Consumes("application/json")]
        [Produces("application/json")]
        public IActionResult CreateService(Service service)
        {
            if (!ModelState.IsValid)
            {
                var serializableModelState = new SerializableError(ModelState);
                var modelStateJson = JsonConvert.SerializeObject(serializableModelState);
                _logger.LogError($"Invalid model for create service", modelStateJson);
                return BadRequest(new ProblemDetails {Status = 400, Title = "Invalid model for create service"});
            }

            try
            {
                var createdService = _servicesService.CreateService(service);
                return Created($"/service/{createdService.ServiceId.ToString()}", createdService);
            }
            catch (Exception e)
            {
                return StatusCode(500, new ProblemDetails {Status = 500, Title = e.Message, Detail = e.InnerException?.Message});
            }
        }

        /// <summary>
        /// get list of all services
        /// </summary>
        /// <returns></returns>
        [HttpGet("")]
        [ProducesResponseType(typeof(List<Service>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public IActionResult GetServices()
        {
            try
            {
                return Ok(_servicesService.GetServices());
            }
            catch (Exception e)
            {
                return StatusCode(500, new ProblemDetails {Status = 500, Title = $"{e.Message}"});
            }
        }

        /// <summary>
        /// get service with id
        /// </summary>
        /// <param name="serviceId"></param>
        /// <returns></returns>
        [HttpGet("{serviceId}")]
        [ProducesResponseType(typeof(Service), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public IActionResult GetService(int serviceId)
        {
            try
            {
                var service = _servicesService.GetService(serviceId);
                if (service != null)
                {
                    return Ok(service);
                }
                return NotFound(new ProblemDetails {Status = 404, Title = "Service not found"});
            }
            catch (Exception e)
            {
                return StatusCode(500, new ProblemDetails {Status = 500, Title = $"{e.Message}"});
            }
        }

        /// <summary>
        /// update service with id
        /// </summary>
        /// <param name="serviceId"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        [HttpPatch("{serviceId}", Name = "UpdateServicePartial")]
        [HttpPut("{serviceId}", Name = "UpdateService")]
        [ProducesResponseType(typeof(Service), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Consumes("application/json")]
        [Produces("application/json")]
        public IActionResult UpdateService(int serviceId, Service service)
        {
            if (!ModelState.IsValid)
            {
                var serializableModelState = new SerializableError(ModelState);
                var modelStateJson = JsonConvert.SerializeObject(serializableModelState);
                _logger.LogError($"Invalid model for update service {modelStateJson}");
                return BadRequest(new ProblemDetails {Status = 400, Title = "Invalid model for update service"});
            }

            try
            {
                return Ok(_servicesService.UpdateService(serviceId, service));
            }
            catch (Exception e)
            {
                return StatusCode(500, new ProblemDetails {Status = 500, Title = $"{e.Message}"});
            }
        }
        /// <summary>
        /// delete service with id
        /// </summary>
        /// <param name="serviceId"></param>
        /// <returns></returns>
        [HttpDelete("{serviceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public IActionResult DeleteService(int serviceId)
        {
            try
            {
                _servicesService.DeleteService(serviceId);
                return Ok();
            }
            catch (Exception e)
            {
                return StatusCode(500, new ProblemDetails {Status = 500, Title = $"{e.Message}"});
            }
        }
    }
}
