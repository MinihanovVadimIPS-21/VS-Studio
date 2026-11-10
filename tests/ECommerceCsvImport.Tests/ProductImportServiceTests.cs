using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Xunit;

namespace ECommerceCsvImport.Tests;

public class ProductImportServiceTests
{
    private const string Header =
        "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number";

    private static (InMemoryProductRepository repo, ProductImportService svc) Create()
    {
        var repo = new InMemoryProductRepository();
        return (repo, new ProductImportService(repo));
    }

    [Fact]
    public void Test01_SuccessfulImport_SingleRow()
    {
        var (repo, svc) = Create();
        var lines = new[]
        {
            Header,
            "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);

        var p = repo.GetBySku("1001");
        Assert.NotNull(p);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", p!.Name);
        Assert.Equal(45990m, p.Price);
        Assert.Equal("Lenovo", p.Brand);
        Assert.Equal(24, p.WarrantyMonths);
        Assert.Equal("IdeaPad 3-15", p.ModelNumber);
    }

    [Fact]
    public void Test02_SuccessfulImport_MultipleRows()
    {
        var (repo, svc) = Create();
        var lines = new[]
        {
            Header,
            "1001,Ноутбук Lenovo,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad",
            "1002,Мышь Logitech,Мышь,1290,RUB,Периферия,10,/images/mouse.jpg,true,Logitech,12,M185"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(2, repo.Count);
    }

    [Fact]
    public void Test03_FileNotFound()
    {
        var (_, svc) = Create();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");

        var result = svc.ImportFromCsv(path);

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Файл не найден", result.Errors[0].Message);
    }

    [Fact]
    public void Test04_EmptyFile()
    {
        var (_, svc) = Create();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        File.WriteAllText(path, string.Empty);

        var result = svc.ImportFromCsv(path);

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);

        File.Delete(path);
    }

    [Fact]
    public void Test05_HeaderOnly()
    {
        var (_, svc) = Create();
        var result = svc.ImportFromLines(new[] { Header });

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);
    }

    [Fact]
    public void Test06_InvalidHeader()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            "sku,name,description,price,currency,category_name,stock,image_url,is_active",
            "1001,Товар,,100,RUB,Категория,5,/img.jpg,true"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Неверные заголовки CSV", result.Errors[0].Message);
    }

    [Fact]
    public void Test07_WrongFieldCount()
    {
        var (_, svc) = Create();
        var lines = new[] { Header, "1001,Ноутбук Lenovo" };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Неверное количество полей"));
    }

    [Fact]
    public void Test08_EmptySku()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            ",Ноутбук Lenovo,Описание,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Поле sku не может быть пустым"));
    }

    [Fact]
    public void Test09_EmptyName()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            "1001,,Описание,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Поле name не может быть пустым"));
    }

    [Fact]
    public void Test10_NegativePrice()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            "1003,Клавиатура,, -500,RUB,Периферия,7,/images/kb.jpg,true,A4Tech,24,V3M"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Поле price должно быть числом больше 0"));
    }

    [Fact]
    public void Test11_NonNumericPrice()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            "1003,Клавиатура,,abc,RUB,Периферия,7,/images/kb.jpg,true,A4Tech,24,V3M"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Поле price должно быть числом больше 0"));
    }

    [Fact]
    public void Test12_NegativeStock()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            "1004,Монитор,,29990,RUB,Компьютеры,-5,/images/m.jpg,true,Dell,24,S2722"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Остаток на складе должен быть целым числом не меньше 0"));
    }

    [Fact]
    public void Test13_InvalidIsActive()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            "1007,Webcam,,4990,RUB,Периферия,6,/images/w.jpg,yes,Logitech,24,C920"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Поле is_active должно иметь значение"));
    }

    [Fact]
    public void Test14_MissingBrand()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            "1005,Видеокарта,,24990,RUB,Компьютеры,4,/images/g.jpg,true,,36,RTX3060"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Поле brand обязательно"));
    }

    [Fact]
    public void Test15_WarrantyBelowZero()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            "1006,SSD,,5990,RUB,Компьютеры,12,/images/s.jpg,true,Samsung,-1,870EVO"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Гарантия должна быть целым числом от 0 до 60"));
    }

    [Fact]
    public void Test16_WarrantyAbove60()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            "1006,SSD,,5990,RUB,Компьютеры,12,/images/s.jpg,true,Samsung,61,870EVO"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Гарантия должна быть целым числом от 0 до 60"));
    }

    [Fact]
    public void Test17_DuplicateSku_WithinFile()
    {
        var (repo, svc) = Create();
        var lines = new[]
        {
            Header,
            "1001,Ноутбук Lenovo,Описание,45990,RUB,Компьютеры,5,/images/l.jpg,true,Lenovo,24,IdeaPad",
            "1001,Ноутбук ASUS,Описание,52990,RUB,Компьютеры,7,/images/a.jpg,true,ASUS,24,VivoBook"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Артикул товара уже существует", result.Errors[0].Message);

        var p = repo.GetBySku("1001");
        Assert.NotNull(p);
        Assert.Equal("Ноутбук Lenovo", p!.Name);
    }

    [Fact]
    public void Test18_DuplicateSku_WithExistingProduct()
    {
        var (repo, svc) = Create();
        repo.Add(new Product { Sku = "1001", Name = "Старый" });

        var lines = new[]
        {
            Header,
            "1001,Новый,Описание,52990,RUB,Компьютеры,7,/images/a.jpg,true,ASUS,24,VivoBook"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, repo.Count);
        Assert.Contains(result.Errors, e => e.Message.Contains("Артикул товара уже существует"));
    }

    [Fact]
    public void Test19_MultipleErrorsInOneRow()
    {
        var (_, svc) = Create();
        var lines = new[]
        {
            Header,
            ",,Некорректная строка,-10,RUB,Компьютеры,-5,/images/x.jpg,yes,,61,"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.True(result.Errors.Count >= 4);
    }

    [Fact]
    public void Test20_EmptyOptionalFieldsAllowed()
    {
        var (repo, svc) = Create();
        var lines = new[]
        {
            Header,
            "1011,Блок питания Corsair CV550,,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);
        var p = repo.GetBySku("1011")!;
        Assert.Equal("", p.Description);
        Assert.Equal("", p.ImageUrl);
    }

    [Fact]
    public void Test21_EmptyCurrency_DefaultsToRub()
    {
        var (repo, svc) = Create();
        var lines = new[]
        {
            Header,
            "1011,Блок питания,,4990,,Компьютеры,9,,true,Corsair,60,CV550"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("RUB", repo.GetBySku("1011")!.Currency);
    }

    [Fact]
    public void Test22_EmptyCategory_DefaultsToBezKategorii()
    {
        var (repo, svc) = Create();
        var lines = new[]
        {
            Header,
            "1011,Блок питания,,4990,RUB,,9,,true,Corsair,60,CV550"
        };

        var result = svc.ImportFromLines(lines);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("Без категории", repo.GetBySku("1011")!.CategoryName);
    }

    [Fact]
    public void Test23_Cyrillic_IsPreserved()
    {
        var (repo, svc) = Create();
        var lines = new[]
        {
            Header,
            "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15"
        };

        svc.ImportFromLines(lines);

        var p = repo.GetBySku("1001");
        Assert.NotNull(p);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", p!.Name);
        Assert.Equal("Бюджетный ноутбук для работы и учебы", p.Description);
    }
}