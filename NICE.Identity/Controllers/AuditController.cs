﻿using Microsoft.AspNetCore.Mvc;
using NICE.Identity.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NICE.Identity.Controllers
{
	[Route("api/[controller]")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly IdentityContext _context;

        public AuditController(IdentityContext context)
        {
            _context = context;
        }

        // GET: api/Audit
        [HttpGet]
        public IEnumerable<Audit> GetAudit()
        {
            return _context.Audit;
        }

        // GET: api/Audit/1
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAudit([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var audit = await _context.Audit.FindAsync(id);

            if (audit == null)
            {
                return NotFound();
            }

            return Ok(audit);
        }
    }
}