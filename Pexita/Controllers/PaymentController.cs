﻿using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Pexita.Services;
using Pexita.Services.Interfaces;
using static Pexita.Utility.Exceptions.PaymentException;

namespace Pexita.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IValidator<PaymentRequest> _validator;

        public PaymentController(IPaymentService paymentService, IValidator<PaymentRequest> validator)
        {
            _paymentService = paymentService;
            _validator = validator;
        }

        [HttpGet("Payments")]
        public IActionResult GetPayments()
        {
            return Ok(_paymentService.GetPayments());
        }

        [HttpGet("Payments/get/{id:int}")]
        public IActionResult GetPayment(int id)
        {
            return Ok(_paymentService.GetPayment(id));
        }

        [HttpPost("Payments/new")]
        public async Task<IActionResult> CreateNewPaymentRequest([FromBody] PaymentRequest request)
        {
            try
            {
                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                await _validator.ValidateAndThrowAsync(request);
                string paymentLink = await _paymentService.SendPaymentRequest(request);
                return Redirect(paymentLink);

            }

            catch (Exception e)
            {
                return BadRequest($"{e.InnerException}: {e.Message}");
            }

        }

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
        [HttpPost("Orders/toggleSent")]
        public async Task<IActionResult> ToggleSent([FromBody] int id)
        {
            try
            {
                await _paymentService.ToggleOrderToSent(id);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}
