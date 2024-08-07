﻿using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pexita.Services;
using Pexita.Services.Interfaces;

namespace Pexita.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IValidator<PaymentRequest> _validator;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PaymentController(IPaymentService paymentService, IValidator<PaymentRequest> validator, IHttpContextAccessor httpContextAccessor)
        {
            _paymentService = paymentService;
            _validator = validator;
            _httpContextAccessor = httpContextAccessor;
        }

        [Authorize(Policy = "AllUsers")]
        [HttpGet("Payments")]
        public IActionResult GetPayments()
        {
            return Ok(_paymentService.GetPayments());
        }

        [Authorize(Policy = "AllUsers")]
        [HttpGet("Payments/get/{id:int}")]
        public IActionResult GetPayment(int id)
        {
            return Ok(_paymentService.GetPayment(id));
        }

        [Authorize(Policy = "AllUsers")]
        [HttpPost("Payments/new")]
        public async Task<IActionResult> CreateNewPaymentRequest([FromBody] PaymentRequest request)
        {
            string requestingUsername = _httpContextAccessor.HttpContext!.User?.Identity?.Name!;
            try
            {
                // add a check so that a user can only check their own payments not other peoples'.
                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                await _validator.ValidateAndThrowAsync(request);
                string paymentLink = await _paymentService.SendPaymentRequest(request, requestingUsername);
                return Redirect(paymentLink);

            }

            catch (Exception e)
            {
                return BadRequest($"{e.InnerException}: {e.Message}");
            }

        }
        [Authorize]
        [HttpPost("Payments/callback")]
        public IActionResult PaymentOutcome([FromBody] PaymentOutcomeValidationResponse response)
        {
            try
            {
                var result = _paymentService.PaymentOutcomeValidation(response);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest($"{e.InnerException}: {e.Message}");
            }

        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("Orders/toggleSent")]
        public async Task<IActionResult> ToggleSent([FromBody] int id)
        {
            string requestingUsername = _httpContextAccessor.HttpContext!.User?.Identity?.Name!;
            try
            {
                await _paymentService.ToggleOrderToSent(id, requestingUsername);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
