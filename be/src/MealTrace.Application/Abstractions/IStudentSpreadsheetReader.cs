using MealTrace.Application.Dtos.Students;

namespace MealTrace.Application.Abstractions;

public interface IStudentSpreadsheetReader
{
    StudentImportSheet Read(Stream file);
}

public sealed class InvalidStudentSpreadsheetException(string message) : Exception(message);
