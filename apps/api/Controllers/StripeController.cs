using GymForYou.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace GymForYou.Api.Controllers;

[ApiController]
[Route("stripe")]
public class StripeController : ControllerBase
{
    private readonly IStripeService _stripe;

    public StripeController(IStripeService stripe)
    {
        _stripe = stripe;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        Event stripeEvent;
        try
        {
            stripeEvent = _stripe.VerifyAndConstructEvent(payload, signature);
        }
        catch (StripeException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Stripe signature",
                Detail = ex.Message,
                Status = 400,
                Instance = HttpContext.Request.Path
            });
        }

        await _stripe.HandleWebhookAsync(stripeEvent);
        return Ok();
    }
}
