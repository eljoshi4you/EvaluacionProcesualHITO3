// MODERNIZACION DE UN SISTEMA DE FACTURACION LEGADO

// Utilizando el patron API Gateway/Proxy Seguro mediante Minimal APIs y la libreria FluentValidation que es el estandar de la industria en .NET
// para realizar validaciones empresariales robustas y limpias.

// JUSTIFICACION:
// Inmutabilidad con record
// Separacion de Responsabilidades(SRP)
// Prevencion de Inyeccion Automatizada 

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Registrar FluentValidation en el contenedor de dependencias
builder.Services.AddValidatorsFromAssemblyContaining<FacturaInputValidator>();

var app = builder.Build();


// 1. MODELO DE DATOS SEGURO (DTO)

public record FacturaInput(string ClienteId, decimal Monto, string CodigoProducto);


// 2. VALIDADOR ESTRICTO (Escudo contra Inyecciones y Desbordamientos)

public class FacturaInputValidator : AbstractValidator<FacturaInput>
{
    public FacturaInputValidator()
    {
        // Validación de ClienteId: Longitud exacta y Lista Blanca (letras, números y guiones)
        RuleFor(x => x.ClienteId)
            .NotEmpty().WithMessage("El ClienteId es obligatorio.")
            .Length(5, 12).WithMessage("El ClienteId debe tener entre 5 y 12 caracteres.")
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("El ClienteId solo permite letras mayúsculas, números y guiones (Previene SQLi/XSS).");

        // Validación de Monto: Evita montos negativos o ataques de desbordamiento en el sistema viejo
        RuleFor(x => x.Monto)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a 0.")
            .LessThan(1000000).WithMessage("El monto excede el límite permitido para el sistema legado.");

        // Validación de Código de Producto: Estrictamente alfanumérico
        RuleFor(x => x.CodigoProducto)
            .NotEmpty().WithMessage("El código de producto es obligatorio.")
            .Length(4, 10).WithMessage("El código debe tener entre 4 y 10 caracteres.")
            .Matches(@"^[a-zA-Z0-9]+$").WithMessage("El código de producto debe ser estrictamente alfanumérico.");
    }
}


// 3. ENDPOINT SEGURO (Fachada de Seguridad)

app.MapPost("/api/v1/facturar", async (
    FacturaInput model,
    IValidator<FacturaInput> validator,
    IHttpClientFactory httpClientFactory) =>
{
    // Ejecutar la validación estricta antes de que toque cualquier otra lógica
    var validationResult = await validator.ValidateAsync(model);

    if (!validationResult.IsValid)
    {
        // Si hay datos maliciosos o erróneos, se frena la petición de inmediato
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    // Mapeo seguro de datos al formato que entiende el sistema antiguo
    var datosParaSistemaLegado = new
    {
        legacy_cust_id = model.ClienteId,
        legacy_amount = model.Monto,
        legacy_prod_code = model.CodigoProducto
    };

    try
    {
        // En un entorno real, aquí se enviarían los datos sanitizados mediante HTTPS, mTLS o VPN interna
        // var client = httpClientFactory.CreateClient();
        // var response = await client.PostAsJsonAsync("http://sistema-viejo.local", datosParaSistemaLegado);
        // response.EnsureSuccessStatusCode();

        return Results.Ok(new
        {
            Status = "Validación y Sanitización Exitosa",
            Mensaje = "Los datos han sido purgados de cualquier amenaza y preparados para el sistema legado.",
            DatosEnviados = datosParaSistemaLegado
        });
    }
    catch (Exception)
    {
        return Results.Problem("Error crítico al intentar comunicar de forma segura con el sistema interno.", statusCode: 500);
    }
});

app.Run();

