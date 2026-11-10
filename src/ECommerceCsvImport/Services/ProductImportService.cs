using System.Globalization;
using ECommerceCsvImport.Models;

namespace ECommerceCsvImport.Services;

public class ProductImportService : IProductImportService
{
    private const string DefaultCurrency = "RUB";
    private const string DefaultCategory = "Без категории";
    private const int WarrantyMin = 0;
    private const int WarrantyMax = 60;

    private static readonly string[] RequiredHeaders =
    {
        "sku", "name", "description", "price", "currency", "category_name",
        "stock", "image_url", "is_active", "brand", "warranty_months", "model_number"
    };

    private readonly IProductRepository _repository;

    public ProductImportService(IProductRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ImportResult ImportFromCsv(string filePath)
    {
        var result = new ImportResult();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Файл не найден" });
            return result;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (IOException)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Файл не найден" });
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Файл не найден" });
            return result;
        }

        return ImportFromLines(lines);
    }

    public ImportResult ImportFromLines(IEnumerable<string> lines)
    {
        var result = new ImportResult();

        if (lines is null)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });
            return result;
        }

        var allLines = lines.ToList();
        int headerIndex = -1;
        
        for (int i = 0; i < allLines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(allLines[i])) { headerIndex = i; break; }
        }

        if (headerIndex < 0)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });
            return result;
        }

        var headerFields = CsvLineParser.Parse(allLines[headerIndex]);
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerFields.Count; i++)
        {
            var name = headerFields[i].Trim();
            if (!string.IsNullOrEmpty(name) && !columnMap.ContainsKey(name))
                columnMap[name] = i;
        }

        var missingHeaders = RequiredHeaders.Where(h => !columnMap.ContainsKey(h)).ToList();
        if (missingHeaders.Count > 0)
        {
            result.Errors.Add(new ImportError
            {
                RowNumber = headerIndex + 1,
                Message = "Неверные заголовки CSV"
            });
            return result;
        }

        int expectedFields = headerFields.Count;
        var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = headerIndex + 1; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            result.TotalRows++;
            int rowNumber = i + 1;

            var values = CsvLineParser.Parse(line);
            if (values.Count != expectedFields)
            {
                result.Errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    Message = "Неверное количество полей в строке"
                });
                continue;
            }

            string GetValue(string col) =>
                columnMap.TryGetValue(col, out int idx) && idx < values.Count
                    ? values[idx] : string.Empty;

            var rowErrors = new List<ImportError>();

            var sku = GetValue("sku").Trim();
            var name = GetValue("name").Trim();
            var priceRaw = GetValue("price").Trim();
            var stockRaw = GetValue("stock").Trim();
            var activeRaw = GetValue("is_active").Trim();
            var brand = GetValue("brand").Trim();
            var warrantyRaw = GetValue("warranty_months").Trim();

            if (string.IsNullOrWhiteSpace(sku))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле sku не может быть пустым" });

            if (string.IsNullOrWhiteSpace(name))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле name не может быть пустым" });

            if (!decimal.TryParse(priceRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле price должно быть числом больше 0" });

            if (!int.TryParse(stockRaw, out var stock) || stock < 0)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Остаток на складе должен быть целым числом не меньше 0" });

            if (!TryParseBool(activeRaw, out var isActive))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле is_active должно иметь значение true, false, 1 или 0" });

            if (string.IsNullOrWhiteSpace(brand))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Поле brand обязательно для варианта «Электроника»" });

            if (!int.TryParse(warrantyRaw, out var warranty) || warranty < WarrantyMin || warranty > WarrantyMax)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = "Гарантия должна быть целым числом от 0 до 60 месяцев" });

            if (rowErrors.Count > 0)
            {
                result.Errors.AddRange(rowErrors);
                continue;
            }

            bool isDuplicate = _repository.GetBySku(sku) is not null || importedSkus.Contains(sku);
            if (isDuplicate)
            {
                result.Errors.Add(new ImportError
                {
                    RowNumber = rowNumber,
                    Message = "Артикул товара уже существует"
                });
                continue;
            }

            var product = new Product
            {
                Sku = sku,
                Name = name,
                Description = GetValue("description").Trim(),
                Price = price,
                Currency = NormalizeCurrency(GetValue("currency")),
                CategoryName = NormalizeCategory(GetValue("category_name")),
                StockQuantity = stock,
                ImageUrl = GetValue("image_url").Trim(),
                IsActive = isActive,
                Brand = brand,
                WarrantyMonths = warranty,
                ModelNumber = GetValue("model_number").Trim()
            };

            _repository.Add(product);
            importedSkus.Add(sku);
            result.ImportedCount++;
        }

        if (result.TotalRows == 0 && result.Errors.Count == 0)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });
        }

        return result;
    }

    private static bool TryParseBool(string value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        if (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1") { result = true; return true; }
        if (v.Equals("false", StringComparison.OrdinalIgnoreCase) || v == "0") { result = false; return true; }
        return false;
    }

    private static string NormalizeCurrency(string value) =>
        string.IsNullOrWhiteSpace(value) ? DefaultCurrency : value.Trim().ToUpperInvariant();

    private static string NormalizeCategory(string value) =>
        string.IsNullOrWhiteSpace(value) ? DefaultCategory : value.Trim();
}