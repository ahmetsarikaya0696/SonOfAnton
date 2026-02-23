using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Engine.Expressions;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Inline expression fonksiyon listesi ve doğrulama API'si
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ExpressionController : ControllerBase
{
    /// <summary>
    /// Kullanılabilir tüm inline fonksiyonları döner (autocomplete için)
    /// </summary>
    [HttpGet("functions")]
    public ActionResult<IReadOnlyList<InlineFunction>> GetFunctions()
    {
        return Ok(FunctionRegistry.Instance.GetAllFunctions());
    }

    /// <summary>
    /// Fonksiyonları kategoriye göre gruplar
    /// </summary>
    [HttpGet("functions/grouped")]
    public ActionResult<Dictionary<string, List<InlineFunction>>> GetFunctionsGrouped()
    {
        var grouped = FunctionRegistry.Instance.GetAllFunctions()
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
        return Ok(grouped);
    }

    /// <summary>
    /// Bir ifadeyi doğrular, geçerli olup olmadığını döner
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<ExpressionValidationResult> Validate([FromBody] ExpressionValidateRequest request)
    {
        var (isValid, error) = ExpressionEvaluator.Validate(request.Expression);
        return Ok(new ExpressionValidationResult
        {
            IsValid = isValid,
            Error = error
        });
    }
}

public class ExpressionValidateRequest
{
    public string Expression { get; set; } = string.Empty;
}

public class ExpressionValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
}
