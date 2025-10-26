using System.Text.Json;
using System.Text.Encodings.Web;
using MDTool.Models;

namespace MDTool.Utilities;

/// <summary>
/// Provides standardized JSON serialization for all MDTool command responses.
/// Ensures consistent output format across success and error scenarios.
/// </summary>
public static class JsonOutput
{
    /// <summary>
    /// JSON serialization options with lowerCamelCase property naming.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Creates a success JSON response with the result data.
    /// </summary>
    /// <typeparam name="T">Type of the result data</typeparam>
    /// <param name="result">The successful result data to serialize</param>
    /// <returns>JSON string with success=true and result field</returns>
    public static string Success<T>(T result)
    {
        try
        {
            var response = new { success = true, result };
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (JsonException ex)
        {
            // Fallback to simple error message if serialization fails
            return $$"""
            {
              "success": false,
              "errors": [{
                "type": "SerializationError",
                "description": "Failed to serialize result: {{ex.Message}}"
              }]
            }
            """;
        }
        catch (Exception ex)
        {
            // Catch any other serialization errors
            return $$"""
            {
              "success": false,
              "errors": [{
                "type": "SerializationError",
                "description": "Failed to serialize result: {{ex.Message}}"
              }]
            }
            """;
        }
    }

    /// <summary>
    /// Creates a failure JSON response with error details.
    /// </summary>
    /// <param name="errors">List of validation or processing errors</param>
    /// <returns>JSON string with success=false and error details</returns>
    public static string Failure(List<ValidationError> errors)
    {
        try
        {
            var response = new
            {
                success = false,
                errors = errors.Select(e => new
                {
                    type = e.Type.ToString(),
                    variable = e.Variable,
                    description = e.Description,
                    line = e.Line
                })
            };
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            // Fallback to simple error message if serialization fails
            return $$"""
            {
              "success": false,
              "errors": [{
                "type": "SerializationError",
                "description": "Failed to serialize errors: {{ex.Message}}"
              }]
            }
            """;
        }
    }

    /// <summary>
    /// Creates a failure JSON response with error details and variable context.
    /// </summary>
    /// <param name="errors">List of validation or processing errors</param>
    /// <param name="provided">Variables that were provided</param>
    /// <param name="missing">Variables that are missing</param>
    /// <returns>JSON string with success=false, errors, provided, and missing arrays</returns>
    public static string Failure(List<ValidationError> errors, List<string> provided, List<string> missing)
    {
        try
        {
            var response = new
            {
                success = false,
                errors = errors.Select(e => new
                {
                    type = e.Type.ToString(),
                    variable = e.Variable,
                    description = e.Description,
                    line = e.Line
                }),
                provided,
                missing
            };
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            // Fallback to simple error message if serialization fails
            return $$"""
            {
              "success": false,
              "errors": [{
                "type": "SerializationError",
                "description": "Failed to serialize errors: {{ex.Message}}"
              }]
            }
            """;
        }
    }

    /// <summary>
    /// Creates a JSON response from a ValidationResult.
    /// </summary>
    /// <param name="validation">The validation result to serialize</param>
    /// <returns>JSON string representing the validation result</returns>
    public static string ValidationOutput(ValidationResult validation)
    {
        if (validation.Success)
        {
            return Success("All required variables provided");
        }
        else
        {
            return Failure(validation.Errors, validation.ProvidedVariables, validation.MissingVariables);
        }
    }
}
